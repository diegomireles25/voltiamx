(function () {
  // ---- Colores (tokens de DESIGN.md) ----
  var C = {
    white: 0xffffff,  // Paper White
    carbon: 0x050505, // Carbon Black (un punto sobre el negro puro para que se lean los reflejos)
    yellow: 0xfff100  // Voltage Yellow
  };
  // ---- Tiempos (ms) ----
  var BAR_STAGGER = 150, BAR_DUR = 800, BAR_INICIO = 200;
  var BOLT_START = 900, BOLT_DUR = 900, READY = 2000, INTRO_CAM = 2400;

  var contenedor = document.getElementById('escena');

  // Aviso a la app (WebView2): la página XAML hace aparecer el botón al recibir 'escena-lista'
  function avisar(mensaje) {
    try { if (window.chrome && window.chrome.webview) window.chrome.webview.postMessage(mensaje); } catch (e) {}
  }

  var renderer;
  try {
    renderer = new THREE.WebGLRenderer({ antialias: true });
  } catch (e) {
    avisar('escena-error');
    return;
  }
  renderer.setClearColor(0xe5e5e5, 1);
  renderer.outputEncoding = THREE.sRGBEncoding;
  renderer.shadowMap.enabled = true;
  renderer.shadowMap.type = THREE.PCFSoftShadowMap;
  contenedor.appendChild(renderer.domElement);

  // ---- Entorno de estudio (reflejos): paneles de luz generados en código, sin archivos ----
  function crearEntorno() {
    var pm = new THREE.PMREMGenerator(renderer);
    var env = new THREE.Scene();
    env.add(new THREE.Mesh(
      new THREE.BoxGeometry(30, 30, 30),
      new THREE.MeshBasicMaterial({ color: 0xb4b4b4, side: THREE.BackSide })
    ));
    function panel(w, h, x, y, z, intensidad) {
      var m = new THREE.Mesh(
        new THREE.PlaneGeometry(w, h),
        new THREE.MeshBasicMaterial({ color: new THREE.Color(intensidad, intensidad, intensidad), side: THREE.DoubleSide })
      );
      m.position.set(x, y, z);
      m.lookAt(0, 0, 0);
      env.add(m);
    }
    panel(12, 6, -8, 10, 8, 6);   // luz principal
    panel(8, 4, 10, 6, 6, 3);     // relleno
    panel(14, 3, 0, 12, -8, 4);   // luz de borde
    var tex = pm.fromScene(env, 0.04).texture;
    pm.dispose();
    return tex;
  }

  var scene = new THREE.Scene();
  var conEntorno = false;
  try { scene.environment = crearEntorno(); conEntorno = true; } catch (e) { conEntorno = false; }

  scene.add(new THREE.HemisphereLight(0xffffff, 0xaaaaaa, conEntorno ? 0.25 : 0.9));
  var luz = new THREE.DirectionalLight(0xffffff, conEntorno ? 1.5 : 1.3);
  luz.position.set(-5, 9, 6);
  luz.castShadow = true;
  luz.shadow.mapSize.set(2048, 2048);
  var sc = luz.shadow.camera;
  sc.left = -9; sc.right = 9; sc.top = 9; sc.bottom = -4; sc.near = 1; sc.far = 30;
  luz.shadow.bias = -0.0004;
  luz.shadow.normalBias = 0.03;
  scene.add(luz);

  // ---- Materiales ----
  // El renderer convierte a sRGB al final: los colores de marca se pasan a lineal para que se vean igual que su hex
  function marca(hex) { return new THREE.Color(hex).convertSRGBToLinear(); }
  var matBlanco = new THREE.MeshStandardMaterial({ color: C.white, roughness: 0.55, metalness: 0, envMapIntensity: 0.8 });
  var matNegro = new THREE.MeshStandardMaterial({ color: marca(C.carbon), roughness: 0.28, metalness: 0.1, envMapIntensity: 1.0 });
  // Amarillo satinado: poco reflejo blanco para que no se lave
  var matAmarillo = new THREE.MeshStandardMaterial({
    color: marca(C.yellow), roughness: 0.38, metalness: 0,
    emissive: marca(C.yellow), emissiveIntensity: 0.1, envMapIntensity: 0.45
  });

  // Caja con aristas biseladas (las aristas vivas no existen en objetos reales)
  function cajaBiselada(w, h, d, b, seg) {
    var sh = new THREE.Shape();
    var hw = (w - 2 * b) / 2, hd = (d - 2 * b) / 2;
    sh.moveTo(-hw, -hd); sh.lineTo(hw, -hd); sh.lineTo(hw, hd); sh.lineTo(-hw, hd); sh.lineTo(-hw, -hd);
    var g = new THREE.ExtrudeGeometry(sh, {
      depth: h - 2 * b, bevelEnabled: true, bevelThickness: b, bevelSize: b, bevelOffset: 0, bevelSegments: seg || 3
    });
    g.rotateX(-Math.PI / 2);
    g.translate(0, b, 0);
    return g;
  }
  function sombrear(m, emite, recibe) { m.castShadow = emite; m.receiveShadow = recibe; return m; }

  // ---- Suelo que solo recibe sombras (se ve sobre el lienzo gris) ----
  var suelo = new THREE.Mesh(new THREE.PlaneGeometry(60, 60), new THREE.ShadowMaterial({ opacity: 0.2 }));
  suelo.rotation.x = -Math.PI / 2;
  suelo.position.y = -0.22;
  suelo.receiveShadow = true;
  scene.add(suelo);

  // ---- Plataforma ----
  var plataforma = sombrear(new THREE.Mesh(cajaBiselada(11.4, 0.22, 2.2, 0.05, 3), matBlanco), true, true);
  plataforma.position.y = -0.22;
  plataforma.scale.x = 0.0001;
  scene.add(plataforma);

  // ---- Barras: consumo por bimestre, la última (negra) es la más alta ----
  var alturas = [1.0, 1.5, 1.2, 2.0, 2.4, 3.2];
  var barras = alturas.map(function (h, i) {
    var ultima = i === alturas.length - 1;
    var m = sombrear(new THREE.Mesh(cajaBiselada(1.1, h, 1.1, 0.045, 3), ultima ? matNegro : matBlanco), true, true);
    m.position.x = (i - 2.5) * 1.7;
    m.scale.y = 0.0001;
    scene.add(m);
    return m;
  });

  // ---- Rayo extruido, con bisel y barniz ----
  var forma = new THREE.Shape();
  forma.moveTo(0.25, 1);
  forma.lineTo(-0.55, -0.1);
  forma.lineTo(-0.08, -0.1);
  forma.lineTo(-0.3, -1);
  forma.lineTo(0.6, 0.15);
  forma.lineTo(0.1, 0.15);
  forma.lineTo(0.25, 1);
  var gRayo = new THREE.ExtrudeGeometry(forma, {
    depth: 0.35, bevelEnabled: true, bevelThickness: 0.07, bevelSize: 0.06, bevelSegments: 4
  });
  gRayo.translate(0, 0, -0.175);
  var rayo = sombrear(new THREE.Mesh(gRayo, matAmarillo), true, false);
  var RAYO_X = -0.9, RAYO_Y = 3.95, RAYO_ESCALA = 1.45;
  rayo.position.set(RAYO_X, RAYO_Y, 0);
  rayo.scale.setScalar(0.0001);
  scene.add(rayo);

  // ---- Cámara ----
  var camara = new THREE.PerspectiveCamera(28, 1, 0.1, 100);
  var objetivo = new THREE.Vector3(0, 2.4, 0);
  var distancia = 20, ancho = 1, alto = 1;
  var AZ = 0.55, EL = 0.38;
  var mx = 0, my = 0, px = 0, py = 0;
  var introP = 0;

  function encuadrar() {
    var aspect = ancho / alto;
    camara.aspect = aspect;
    camara.updateProjectionMatrix();
    var tanV = Math.tan(THREE.MathUtils.degToRad(camara.fov / 2));
    var mitadAlto = 3.6, mitadAncho = 6.2;
    distancia = Math.max(mitadAlto / tanV, mitadAncho / (tanV * aspect)) + 3;
  }
  function ponerCamara() {
    var e = 1 - Math.pow(1 - introP, 3);
    var d = distancia * (1 + 0.12 * (1 - e));         // se acerca al entrar
    var az = AZ + 0.28 * (1 - e) + px * 0.07;         // y gira hasta su sitio
    var el = EL + py * 0.05;
    camara.position.set(
      objetivo.x + d * Math.cos(el) * Math.sin(az),
      objetivo.y + d * Math.sin(el),
      objetivo.z + d * Math.cos(el) * Math.cos(az)
    );
    camara.lookAt(objetivo);
  }

  function ajustar() {
    var r = contenedor.getBoundingClientRect();
    ancho = Math.max(1, Math.floor(r.width));
    alto = Math.max(1, Math.floor(r.height));
    renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 2));
    renderer.setSize(ancho, alto, false);
    encuadrar();
    ponerCamara();
    if (reducido) dibujar(1e6);
  }

  // ---- Easing ----
  function clamp01(x) { return Math.max(0, Math.min(1, x)); }
  function outCubic(p) { return 1 - Math.pow(1 - p, 3); }
  function outBack(p, c1) { var c3 = c1 + 1; return 1 + c3 * Math.pow(p - 1, 3) + c1 * Math.pow(p - 1, 2); }

  // ---- Estado ----
  var reducido = window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
  var t0 = performance.now(), listo = false, raf = 0;

  function dibujar(t) {
    plataforma.scale.x = Math.max(outCubic(clamp01(t / 500)), 0.0001);

    for (var i = 0; i < barras.length; i++) {
      var p = clamp01((t - BAR_INICIO - i * BAR_STAGGER) / BAR_DUR);
      barras[i].scale.y = Math.max(outBack(p, 0.8), 0.0001); // asienta con un leve rebote
    }

    var pr = clamp01((t - BOLT_START) / BOLT_DUR);
    var idle = Math.max(0, t - (BOLT_START + BOLT_DUR));
    rayo.scale.setScalar(Math.max(outBack(pr, 1.2) * RAYO_ESCALA, 0.0001));
    rayo.rotation.y = outCubic(pr) * Math.PI * 2 + (reducido ? 0.5 : idle * 0.0006);
    rayo.rotation.z = reducido ? 0.04 : Math.sin(t * 0.0013) * 0.07 * (idle > 0 ? 1 : 0);
    rayo.rotation.x = reducido ? 0 : Math.sin(t * 0.0009) * 0.05 * (idle > 0 ? 1 : 0);
    rayo.position.y = RAYO_Y + (1 - outCubic(pr)) * 2.2 + (reducido ? 0 : Math.sin(t * 0.002) * 0.12);
    matAmarillo.emissiveIntensity = reducido ? 0.1 : 0.08 + 0.04 * Math.sin(t * 0.003);

    introP = reducido ? 1 : clamp01(t / INTRO_CAM);
    ponerCamara();
    renderer.render(scene, camara);
  }

  function bucle(ahora) {
    var t = ahora - t0;
    px += (mx - px) * 0.06;
    py += (my - py) * 0.06;
    dibujar(t);
    if (!listo && t >= READY) mostrarAccion();
    raf = requestAnimationFrame(bucle);
  }

  function mostrarAccion() { listo = true; avisar('escena-lista'); }

  function iniciar() {
    cancelAnimationFrame(raf);
    listo = false;
    if (reducido) {
      dibujar(1e6);
      mostrarAccion();
    } else {
      t0 = performance.now();
      raf = requestAnimationFrame(bucle);
    }
  }

  // ---- Eventos ----
  window.addEventListener('mousemove', function (e) {
    mx = (e.clientX / window.innerWidth) * 2 - 1;
    my = (e.clientY / window.innerHeight) * 2 - 1;
  });
  new ResizeObserver(ajustar).observe(contenedor);

  ajustar();
  iniciar();
})();

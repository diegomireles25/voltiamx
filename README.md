# VoltiaMX

App de escritorio para Windows que ayuda a entender, predecir y reducir el recibo de luz de la CFE, en casa, en un negocio o en propiedades en renta, sin necesidad de internet.

**Estado: AVANCE (fase 5 de 6), no es una versión final.**

## Qué ya funciona

- Onboarding de 7 pasos con escena 3D de bienvenida.
- Perfiles y módulos dinámicos: la navegación se arma según el uso que elijas.
- Dashboard con datos reales o de demostración.
- Captura de lecturas del medidor con cierre automático de periodos en la fecha de corte.
- Historial de consumo y gasto con gráficas.
- Presupuesto: un tope de pago y cuántos kWh al día puedes gastar para no pasarte.
- Predictor del recibo con su desglose por rangos y alerta de riesgo DAC.
- Tarifas editables (rangos, precios, cargo fijo y límite DAC) y validación contra tu recibo.
- Aparatos con su consumo estimado y ranking real contra lo que mide tu medidor.
- Simulador de ahorro: menos horas de uso o un equipo más eficiente, con pesos según tu tarifa y recuperación de la inversión.
- Metas de ahorro: reducir tu consumo contra el periodo anterior, con progreso, historial y racha.
- En `VoltiaMX.Core`: cálculo por rangos de tarifa, proyección del periodo, riesgo DAC con el promedio de los últimos 12 meses y presupuesto inverso.
- Pruebas automáticas (`dotnet test`).
- Base de datos local SQLite.

## Qué falta

- OCR del recibo.
- Notificaciones.
- Inquilinos.
- Simulador solar.
- Exportar a Excel y PDF.
- Respaldo.

Las secciones que aún no existen aparecen en la app como "Próximamente" o con sus botones deshabilitados.

## Cómo ejecutarla

Requisitos:

- Windows 10 (versión 1809 o posterior) o Windows 11, x64.
- Visual Studio 2026 con desarrollo de aplicaciones WinUI (Windows App SDK) y el SDK de .NET 10.
- Modo de desarrollador activado en Windows (Configuración > Sistema > Para programadores).
- Microsoft Edge WebView2 Runtime para la escena 3D de bienvenida. Viene incluido en Windows 11; si no está instalado, la bienvenida se muestra sin la escena y el resto de la app funciona igual.

Pasos:

1. Abre `VoltiaMX.sln` en Visual Studio 2026.
2. Elige la configuración **Debug | x64** y el perfil de inicio **VoltiaMX (Package)**.
3. Ejecuta con F5.

Desde la línea de comandos:

```bash
dotnet build VoltiaMX -p:Platform=x64
```

```bash
dotnet test
```

## Aviso sobre tarifas y cifras

Las tarifas, rangos, cargos y cifras que trae la app son **datos de ejemplo** para probarla. **No son datos oficiales de la CFE.** Verifica siempre las tarifas vigentes en tu recibo o con la CFE antes de tomar decisiones con estos cálculos. Las tarifas se guardan como datos en la base local, no en el código.

## Estructura

```text
VoltiaMX/              App WinUI 3: vistas, ViewModels, servicios de UI, estilos y recursos
  Assets/Escena3D/     Escena 3D de la bienvenida (HTML local con Three.js)
  Assets/Fonts/        Fuentes de la interfaz y sus licencias
  Strings/es-MX/       Textos de la interfaz (Resources.resw)
  Styles/              Colores, tipografía, formas y estilos de controles
VoltiaMX.Core/         Modelos, base de datos (EF Core + SQLite) y servicios de cálculo
VoltiaMX.Tests/        Pruebas automáticas (xUnit) de VoltiaMX.Core
docs/                  Sistema visual (DESIGN.md), mockup del Dashboard y prompts por fase
```

## Créditos de terceros

- **Three.js r128**: copyright 2010-2021 three.js authors, licencia MIT. Ver `VoltiaMX/Assets/Escena3D/LICENSE-three.txt`.
- **Barlow Condensed**, **Inter** y **JetBrains Mono**: licencia SIL Open Font License 1.1. Ver los textos de licencia en `VoltiaMX/Assets/Fonts/`.

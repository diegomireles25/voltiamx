# Fase 2: Onboarding y navegación dinámica

Lee CLAUDE.md y docs/DESIGN.md. Implementa la FASE 2 (Onboarding) en el proyecto VoltiaMX (WinUI 3) y las partes necesarias en VoltiaMX.Core. Modo plan primero.

Paquetes en VoltiaMX: CommunityToolkit.Mvvm y Microsoft.Extensions.DependencyInjection/Hosting (si ya están instalados, no los dupliques).

En VoltiaMX.Core:
- Models/PerfilUsuario.cs (Id, Nombre, OnboardingCompletado, ModoAvanzado)
- Models/PerfilModulo.cs (Id, PerfilId, Modulo [enum ModuloApp: Lecturas, Predictor, Presupuesto, Aparatos, Solar, Inquilinos, Metas, Reportes], Activo)
- Models/Objetivo.cs (Id, Tipo [enum TipoObjetivo: EntenderRecibo, PagarMenos, NoPasarmeDePresupuesto, RepartirInquilinos], PresupuestoMax nullable)
- Agrega los DbSets, una migración nueva y el servicio PerfilService (guardar perfil, activar módulos según los usos elegidos).
- Regla de módulos por uso: Casa → Lecturas, Predictor, Presupuesto, Aparatos. Negocio → Lecturas, Predictor, Aparatos, Reportes. Rentas → Lecturas, Inquilinos, Reportes. Solar → Solar, Reportes. Si eligen varios, se unen. Modo avanzado desbloquea además Aparatos, Solar e Inquilinos. Escribe pruebas para esta regla.

En VoltiaMX (app):
- App.xaml.cs: configurar el host con DI (DbContext, servicios, ViewModels) y al iniciar decidir: si OnboardingCompletado es false abre OnboardingWindow, si no abre ShellWindow.
- Views/Onboarding/: OnboardingPage con un Frame interno y las páginas de los pasos: BienvenidaPage, UsoPage (tarjetas grandes multi-selección: Mi casa, Mi negocio, Propiedades en renta, Cotizar paneles solares), UbicacionPage (estado y municipio, con opción "No sé mi tarifa"), ReciboPage (tres caminos: escanear [deja el botón deshabilitado con el texto "Próximamente"], capturar manual, saltar), ObjetivoPage, PresupuestoPage (monto máximo opcional y switch de notificaciones) y ListoPage (resumen).
- ViewModels/Onboarding/OnboardingViewModel.cs: lista de pasos, comandos Siguiente, Atrás y Omitir, y guardado final con PerfilService. Máximo 7 pasos, todos omitibles.
- Views/ShellWindow.xaml: NavigationView cuyos items se generan desde los módulos activos del perfil (no fijos). Incluye siempre Dashboard y Configuración.
- Views/DashboardPage.xaml con contenido que cambia según el perfil (casa: pago estimado y semáforo DAC; negocio: gasto por periodo; rentas: resumen por propiedad; solar: cotizaciones recientes). Por ahora con datos de ejemplo y estados vacíos útiles.
- Una opción "Cargar datos de demo" cuando el usuario omita el recibo.
- Configuración: poder repetir el onboarding y cambiar entre modo simple y avanzado.

Restricciones: x:Bind, textos en Resources.resw, ViewModels sin referencias a UI. Usa ÚNICAMENTE los estilos y recursos de VoltiaMX/Styles/*.xaml (CardStyle, InvertedCardStyle, MintTagStyle, PrimaryButtonStyle, HairlineRowStyle, estilos de Typography, etc.); si falta un estilo, créalo ahí en vez de definirlo dentro de la vista. Sigue docs/DESIGN.md y usa el skill de diseño para las decisiones de composición.

Aplicación visual por pantalla:
- BienvenidaPage: titular en estilo DisplayXL en mayúsculas ("ENTIENDE TU RECIBO DE LUZ"), subtítulo Body en Slate, botón primario "Empezar".
- Indicador de paso en cada página: MintTagStyle con texto mono ("PASO 2/5"), uno solo por pantalla.
- UsoPage: tarjetas grandes con CardStyle; al seleccionarse cambian a InvertedCardStyle (fondo negro, texto blanco).
- ListoPage: resumen dentro de una TopArcCard negra con título en Display.
- DashboardPage: usa docs/mockup-dashboard.png como referencia de composición (si el archivo existe). La cifra del pago estimado en DisplayXL (80px si la ventana mide menos de 1200px); semáforo DAC con MintTagStyle / YellowTagStyle / RiskHighTagStyle; tarjeta del presupuesto inverso con InvertedCardStyle; etiquetas de periodo en Caption mono.
- Evita el aspecto de plantilla: solo 2 tarjetas en el Dashboard (el historial y el presupuesto inverso); el estado (DAC, avisos, meta) y el ranking de aparatos van directo sobre el lienzo en filas separadas por línea negra de 1px (HairlineRowStyle); una sola barra de progreso por pantalla; pocas pastillas.

Al terminar: compila con dotnet build VoltiaMX -p:Platform=x64, corre dotnet test, y dime cualquier paso manual que necesite hacer en Visual Studio (por ejemplo, ejecutar la app para probar el flujo).

# Fase 0: Esqueleto de la solución

Lee CLAUDE.md y docs/DESIGN.md. Vamos a hacer la FASE 0: crear desde cero el esqueleto de la solución VoltiaMX. Usa modo plan: muéstrame el plan y espera mi aprobación antes de escribir nada.

Primero verifica el entorno: dotnet --info (necesito .NET 10 SDK), la presencia de Visual Studio 2026 con vswhere, y la carga de trabajo de Windows App SDK. Si falta algo, dime exactamente qué instalar y detente.

1) Crea VoltiaMX.sln en la raíz con tres proyectos:
- VoltiaMX: app WinUI 3 empaquetada (MSIX), target net10.0-windows10.0.19041.0, plataformas x64 y ARM64, última versión estable de Microsoft.WindowsAppSDK. Intenta primero con la plantilla de línea de comandos: dotnet new install VijayAnand.WinUITemplates y luego la plantilla winui de ese paquete. Si no funciona, escribe tú los archivos a mano: VoltiaMX.csproj, App.xaml, App.xaml.cs, MainWindow.xaml, MainWindow.xaml.cs, Package.appxmanifest, app.manifest, Properties/launchSettings.json y los Assets mínimos del manifiesto.
- VoltiaMX.Core: classlib net10.0.
- VoltiaMX.Tests: proyecto xunit net10.0.

2) Referencias: VoltiaMX -> VoltiaMX.Core y VoltiaMX.Tests -> VoltiaMX.Core.

3) Paquetes NuGet (versiones estables más recientes):
- VoltiaMX.Core: Microsoft.EntityFrameworkCore.Sqlite y Microsoft.EntityFrameworkCore.Design.
- VoltiaMX: CommunityToolkit.Mvvm y Microsoft.Extensions.Hosting.
- No agregues LiveCharts2, QuestPDF ni ClosedXML todavía; llegan en sus fases.

4) Fuentes: crea VoltiaMX/Assets/Fonts/ y descarga (licencia OFL) los archivos estáticos de Barlow Condensed Bold desde github.com/google/fonts, Inter Regular y Medium desde los releases oficiales de github.com/rsms/inter, y JetBrains Mono Regular desde los releases oficiales de github.com/JetBrains/JetBrainsMono, junto con sus archivos de licencia. No uses las fuentes variables de google/fonts. Márcalos como Content con copia al output en el .csproj. Si Inter solo está disponible como fuente variable, o no hay red, dime qué archivos debo conseguir yo y no inventes nombres de archivo.

5) Estructura de carpetas vacía con .gitkeep: VoltiaMX/Views, VoltiaMX/ViewModels, VoltiaMX/Styles, VoltiaMX/Helpers, VoltiaMX.Core/Models, VoltiaMX.Core/Data, VoltiaMX.Core/Services.

6) Ejecuta dotnet new gitignore, agrega los archivos y haz el primer commit con el mensaje "Esqueleto inicial de VoltiaMX". No hagas push.

7) Verificación: dotnet build VoltiaMX.Core, dotnet test y dotnet build VoltiaMX -p:Platform=x64. Si la app WinUI no compila con dotnet build, prueba con el MSBuild de Visual Studio (ubícalo con vswhere). Si sigue fallando, muéstrame el error completo y no lo tapes.

Al terminar dame: archivos creados, versiones de paquetes elegidas, resultado de las tres compilaciones y qué debo comprobar yo abriendo la solución en Visual Studio (F5 debe mostrar una ventana vacía).

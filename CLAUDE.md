# VoltiaMX

App de escritorio WinUI 3 (C#) para entender, predecir y reducir el recibo de luz de la CFE (México). Sirve para casa, negocio, propiedades en renta e instaladores solares. Se adapta al usuario mediante un onboarding.

## Estructura
- VoltiaMX/            App WinUI 3 (Views, ViewModels, Services de UI, Assets)
- VoltiaMX.Core/       Models, Data (EF Core + SQLite), Services (cálculos), sin dependencias de WinUI
- VoltiaMX.Tests/      xUnit, pruebas de Core
- docs/                DESIGN.md (sistema visual), mockup-dashboard.png y mockup-dashboard.html (referencia visual) y docs/prompts/ (prompts por fase)

## Stack
- .NET 10, WinUI 3, Windows App SDK
- MVVM con CommunityToolkit.Mvvm (ObservableObject, RelayCommand, source generators)
- Microsoft.Extensions.DependencyInjection y Hosting
- EF Core + SQLite
- LiveCharts2 (gráficas), QuestPDF (PDF), ClosedXML (Excel)
- Windows.Media.Ocr (OCR sin internet), notificaciones con Windows App SDK

## Reglas
- Toda la lógica de negocio y cálculo vive en VoltiaMX.Core y debe tener pruebas en VoltiaMX.Tests.
- Los ViewModels no referencian tipos de UI (ni Page, ni Window, ni Dispatcher directo).
- Las Views no contienen lógica; solo bindings (preferir x:Bind).
- Las tarifas y rangos son DATOS editables en la base de datos, nunca constantes en el código.
- NO inventes precios reales de la CFE. Los datos semilla son de ejemplo y deben marcarse con el comentario "// EJEMPLO: verificar con tarifas vigentes de CFE".
- DAC depende del consumo promedio de los últimos 12 meses contra un límite por tarifa (editable), no de un solo periodo.
- Usa "Lugar" (con tipo Casa, Negocio, Propiedad), no "Domicilio".
- Textos de la interfaz en español de México, centralizados en Resources.resw.
- Funciona 100% sin internet. Nada de llamadas de red obligatorias.
- Async/await en acceso a datos. Nombres de clases y propiedades de dominio en español, igual que los de la base de datos.
- Mantén las páginas con estados vacíos útiles ("Agrega tu primera lectura").

## Comandos
- Compilar Core y pruebas: dotnet build VoltiaMX.Core y dotnet test
- Compilar la app: dotnet build VoltiaMX -p:Platform=x64
- Antes de dar una tarea por terminada: compila y corre las pruebas.

## Flujo de trabajo
- Una fase a la vez. Al terminar cada fase, resumir qué se hizo y qué falta.
- No instalar paquetes fuera de la lista del stack sin preguntar.
- Los prompts de cada fase están en docs/prompts/. Si no existen, pídemelos antes de improvisar.

## Diseño visual
- Fuente de verdad: docs/DESIGN.md (sistema "brutalist editorial": lienzo #e5e5e5, tarjetas #ffffff, bloques #000000, acento mint #d1ffca, amarillo #fff100 solo en microacentos).
- Referencia de composición del Dashboard: docs/mockup-dashboard.png (imagen) y docs/mockup-dashboard.html (código con los valores exactos de tamaños, colores y espaciado). En caso de duda entre ambos, gana el HTML. Las cifras del mockup son ilustrativas.
- Es una app de ESCRITORIO WinUI 3: se traduce el sistema, no se copia el CSS. Adaptaciones obligatorias:
  - Tema fijo Light (RequestedTheme="Light" en App.xaml). Fondo sólido, sin Mica ni Acrylic.
  - Cero sombras, cero gradientes. Sobrescribir los estilos por defecto de WinUI que agreguen elevación o bordes en tarjetas y botones.
  - Fuentes: Barlow Condensed Bold (display, siempre MAYÚSCULAS, interlineado 0.9), Inter 400/500 (cuerpo), JetBrains Mono 400 (etiquetas 12px). Cargarlas desde Assets/Fonts con ms-appx. Nunca usar SuisseIntl.
  - CharacterSpacing de WinUI va en milésimas de em: -0.03em = -30.
  - WinUI no tiene text-transform: los títulos van en mayúsculas directamente en Resources.resw o con UppercaseConverter.
  - Radios: tarjetas 24-32, tarjeta grande 64, tags 8, nav pill 48, botones 8.
  - La pastilla de navegación usa radio 16 y la pestaña activa radio 10: decisión del proyecto que tiene prioridad sobre el valor de 48 px de docs/DESIGN.md. Padding de tarjeta 24. Escala de espaciado base 8.
  - Las etiquetas (tags) son rectangulares con esquinas de radio 8, NO pastillas: esta decisión del proyecto tiene prioridad sobre el valor de 64px de docs/DESIGN.md.
  - Semáforo DAC: Bajo = tag mint, Medio = tag amarillo, Alto = etiqueta negra con texto amarillo. No introducir rojo.
  - El número de 130px solo se usa para la cifra principal del Dashboard; bajar a 80px si la ventana mide menos de 1200px de ancho.
- Todos los colores, radios, espaciados y estilos viven en VoltiaMX/Styles/*.xaml como recursos. Prohibido hardcodear colores o tamaños en las Views.
- Evitar el patrón "plantilla": no repetir la misma tarjeta blanca redondeada para todo. Usar pocas tarjetas, filas separadas por líneas negras de 1px sobre el lienzo, pocas etiquetas y una sola barra de progreso por pantalla.
- Para decisiones de diseño (jerarquía, composición, estados), usar el skill de diseño instalado; para implementación, solo XAML de WinUI 3.

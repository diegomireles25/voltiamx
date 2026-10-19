# Fase 1.5: Sistema de diseño

Lee CLAUDE.md y docs/DESIGN.md. Vamos a implementar el SISTEMA DE DISEÑO de VoltiaMX en el proyecto VoltiaMX (WinUI 3). No toques VoltiaMX.Core ni VoltiaMX.Tests. Usa modo plan: muéstrame el plan y espera mi aprobación antes de escribir código.

Antes de planear, invoca el skill de diseño que tengas disponible (por ejemplo frontend-design) y úsalo SOLO para decisiones de jerarquía, composición, estados y accesibilidad. Es un skill pensado para web: no generes HTML/CSS ni Tailwind, todo se implementa en XAML de WinUI 3. Si algo del skill contradice docs/DESIGN.md o CLAUDE.md, gana docs/DESIGN.md.

Las fuentes ya están en VoltiaMX/Assets/Fonts/: BarlowCondensed-Bold.ttf, Inter-Regular.ttf, Inter-Medium.ttf, JetBrainsMono-Regular.ttf. Verifica los nombres de familia reales y configura su Build Action como Content con copia al output.

1) Crea VoltiaMX/Styles/Colors.xaml (ResourceDictionary) con los 10 colores de DESIGN.md como Color y SolidColorBrush (CarbonBlack, PaperWhite, WarmCanvas, MistGray, Ash, Smoke, Slate, Graphite, MintChip, VoltageYellow). Sobrescribe en el diccionario de tema Light los recursos de WinUI necesarios para que los controles estándar usen esta paleta: ApplicationPageBackgroundThemeBrush, TextFillColorPrimaryBrush, TextFillColorSecondaryBrush, y los de TextBox, ComboBox, ToggleSwitch, CheckBox, RadioButton, CalendarDatePicker y NavigationView.

2) Crea VoltiaMX/Styles/Typography.xaml con FontFamily para Display (Barlow Condensed Bold), Body (Inter) y Mono (JetBrains Mono), y estos estilos de TextBlock siguiendo la escala de DESIGN.md: DisplayXL (130), Display (80), HeadingLg (48), Heading (40), HeadingSm (28), Subheading (18), Body (16), BodySm (14), Caption (12, mono). Display, DisplayXL y HeadingLg: MAYÚSCULAS por contenido, interlineado 0.9 (LineHeight en px = 0.9 x tamaño), CharacterSpacing -30. Heading con CharacterSpacing -20. Indica el cálculo de LineHeight de cada estilo en un comentario.

3) Crea VoltiaMX/Styles/Shapes.xaml con CornerRadius (RadiusButton 8, RadiusCard 24, RadiusCardLg 32, RadiusCardXl 64, RadiusTag 64, RadiusNavPill 48, RadiusTopArc 64,64,0,0), escala de espaciado (8, 16, 24, 40, 64, 80, 96) y Thickness de padding de tarjeta (24).

4) Crea VoltiaMX/Styles/Controls.xaml con estos estilos, sin sombras, sin gradientes y sin ThemeShadow:
- CardStyle (Border blanco, RadiusCard, padding 24, sin borde)
- InvertedCardStyle (negro, texto blanco, RadiusCardLg)
- TopArcCardStyle (RadiusTopArc, variantes negra y blanca)
- MintTagStyle y YellowTagStyle (Border pastilla con TextBlock en estilo Caption)
- RiskHighTagStyle (pastilla negra con texto amarillo)
- PrimaryButtonStyle (fondo negro, texto blanco, RadiusButton, Padding 24,16, Inter Medium 16, con estados PointerOver y Pressed usando Graphite y sin sombra)
- GhostButtonStyle (transparente, borde 1.5 Slate, texto Slate)
- TextLinkButtonStyle (sin fondo ni borde, subrayado en hover)
- InputStyle para TextBox (fondo blanco, RadiusButton, borde 1 Ash, borde 2 Carbon al enfocar)
- NavPillStyle para el NavigationView en modo Top (contenedor blanco, RadiusNavPill, altura de barra 88px, items en Inter Medium 16 color Slate, seleccionado en Carbon)
- HairlineRowStyle: fila separada por una línea negra de 1px (para listas sobre el lienzo, sin tarjeta)
- Estilos de foco visibles y accesibles en todos los controles interactivos.
Usa lightweight styling (claves de recurso de los controles) siempre que sea posible, y plantillas completas solo cuando no haya otra forma de quitar la elevación o el borde por defecto.

5) Crea VoltiaMX/Helpers/UppercaseConverter.cs (IValueConverter) para texto dinámico en mayúsculas.

6) Edita VoltiaMX/App.xaml: agrega RequestedTheme="Light" en Application y los cuatro diccionarios (Colors, Typography, Shapes, Controls) en Application.Resources > MergedDictionaries, después de XamlControlsResources.

7) Configura la ventana principal para el estilo: ExtendsContentIntoTitleBar con la barra de título en WarmCanvas (colores del AppWindow.TitleBar), sin SystemBackdrop (fondo sólido), tamaño mínimo 1100x700.

8) Crea VoltiaMX/Views/DesignGalleryPage.xaml, una página SOLO de depuración (visible únicamente con #if DEBUG), que muestre todos los tokens y componentes: paleta de colores con su nombre y hex, toda la escala tipográfica, tarjetas (estándar, invertida, top-arc), tags (mint, amarillo, riesgo alto), botones (primario, ghost, link, deshabilitado), TextBox, ToggleSwitch, ComboBox, filas con línea (HairlineRowStyle) y la NavigationView en pastilla. Es mi herramienta para verificar el resultado a ojo, porque tú no puedes ver la interfaz.

Restricciones: no hardcodees colores, tamaños ni radios fuera de Styles/*.xaml; nada de librerías de UI de terceros; todos los textos visibles en Resources.resw.

Al terminar: compila con dotnet build VoltiaMX -p:Platform=x64, resume los archivos creados, lista los supuestos que hiciste (sobre todo nombres de familia de fuente y claves de recurso) y dime qué debo revisar manualmente en DesignGalleryPage al ejecutar la app desde Visual Studio.

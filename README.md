# VoltiaMX

App de escritorio para Windows que ayuda a entender, predecir y reducir el recibo de luz de la CFE, en casa, en un negocio o en propiedades en renta, sin necesidad de internet.

**Estado: AVANCE (versión 1.1), no es una versión final.**

## Qué ya funciona

- Onboarding corto de 3 preguntas (uso, ubicación y tarifa, recibo opcional) con escena 3D de bienvenida.
- Experiencia simplificada: el menú muestra Inicio, hasta dos módulos principales según tu perfil y Más.
- Inicio con el pago estimado del recibo, la comparación con tu recibo anterior, la tarifa alta (DAC), el historial y lo que más gasta.
- Captura de lecturas del medidor con cierre automático de cada recibo en el día de corte.
- Historial de consumo y gasto con gráficas.
- "Cómo se calcula tu recibo": el desglose por tramos de precio y la cercanía a la tarifa alta (DAC).
- Tarifas editables (tramos, precios, cargo fijo y límite de la tarifa alta) y validación contra tu recibo.
- Aparatos con su consumo aproximado y lo que más gasta contra lo que mide tu medidor.
- Simulador de ahorro: menos horas de uso o un equipo más eficiente, con pesos según tu tarifa y recuperación de la inversión.
- Metas de ahorro: gastar menos que el recibo anterior, con progreso, historial y racha.
- Escanear el recibo (foto o PDF) con reconocimiento de texto local, sin internet: revisas los datos leídos y guardas la lectura.
- Notificaciones de Windows con los avisos del día de corte, la tarifa alta y la meta (verificación con la versión empaquetada pendiente).
- Exportar a Excel y PDF: el reporte del recibo en PDF y todos tus datos en Excel, generados en tu equipo.
- Respaldo y restauración en un solo archivo, con copia automática antes de actualizar la base de datos y antes de restaurar.
- Inquilinos: reparto del recibo según el medidor propio de cada uno (solo lo medido o proporcional), con un recibo en PDF por inquilino y un resumen.
- Simulador solar: una estimación de paneles, generación, ahorro y recuperación con supuestos editables, exportable a PDF.
- En `VoltiaMX.Core`: cálculo por tramos de la tarifa, el ritmo de consumo del recibo y la tarifa alta (DAC) con el promedio de los últimos 12 meses.
- Pruebas automáticas (`dotnet test`).
- Base de datos local SQLite.

## Qué falta

- Verificación con un recibo real.
- Tarifas oficiales de CFE (hoy son datos de ejemplo editables).
- Empaquetado e instalación en otro equipo.

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
- **QuestPDF 2026.9.1** (reporte en PDF): QuestPDF Community License, gratuita para individuos, organizaciones sin fines de lucro, proyectos open source y organizaciones con ingresos brutos anuales menores a USD 1,000,000; no aplica a empresas que cotizan en bolsa ni a entidades del sector público. Se declara en el código con `QuestPDF.Settings.License = LicenseType.Community` (`VoltiaMX.Core/Services/Reportes/GeneradorPdf.cs`). Si quien publica la app deja de cumplir esas condiciones, necesita una licencia comercial. Ver https://www.questpdf.com/license.
- **ClosedXML 0.105.1** (exportación a Excel): licencia MIT.

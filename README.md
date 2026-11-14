# VoltiaMX

App de escritorio para Windows que explica el recibo de luz de la CFE a hogares en México: cuánto vas a pagar, qué aparatos gastan más y cuántos paneles solares necesitarías. Funciona sin internet y todo se guarda en tu equipo.

**Versión 1.1.**

## Qué hace

- Primer uso corto con escena 3D de bienvenida: eliges tu tarifa y agregas tu último recibo.
- Cuatro pestañas: **Inicio**, **Mis recibos**, **Aparatos** y **Configuración**.
- Inicio con el pago estimado del periodo, la comparación con tu recibo anterior, la cercanía a la tarifa alta (DAC), qué gasta más y una sugerencia para ahorrar.
- Escaneo del recibo (foto o PDF) con lectura de texto local, sin internet. Lee fecha del periodo, tarifa, importe, consumo en kWh y tramos; tú revisas antes de guardar.
- Validación del recibo contra tu tarifa, con aviso si no coinciden.
- Historial de recibos con consumo y gasto.
- Aparatos con su costo aproximado al mes. Soporta varios iguales (por ejemplo tres aires), con las mismas horas o cada uno distinto, y permite editar y eliminar.
- Tarifas editables: tramos, precios, cargo fijo y límite de la tarifa alta.
- Simulador solar: paneles, espacio, generación, ahorro y recuperación, con supuestos editables.
- Exportar a Excel y PDF, y respaldo y restauración en un solo archivo.
- Lenguaje sencillo: cifras en pesos y marcadas como "aproximado" cuando son estimaciones.

El proyecto incluye además módulos que no se muestran en esta versión: modo avanzado, metas de ahorro, simulador de ahorro, reparto entre inquilinos y notificaciones de Windows.

## Manual de usuario

Está en `docs/Manual-de-usuario-VoltiaMX.docx`.

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

Las cifras de la app son estimaciones. Tu recibo de la CFE es el dato oficial. Los precios de las tarifas se guardan como datos en la base local, no en el código, y pueden no coincidir con los vigentes o con los de tu temporada. Verifica siempre tu tarifa en el recibo y, si hace falta, corrígela en Configuración > Tu tarifa.

## Estructura

```text
VoltiaMX/              App WinUI 3: vistas, ViewModels, servicios de UI, estilos y recursos
  Assets/Escena3D/     Escena 3D de la bienvenida (HTML local con Three.js)
  Assets/Fonts/        Fuentes de la interfaz y sus licencias
  Strings/es-MX/       Textos de la interfaz (Resources.resw)
  Styles/              Colores, tipografía, formas y estilos de controles
VoltiaMX.Core/         Modelos, base de datos (EF Core + SQLite) y servicios de cálculo
VoltiaMX.Tests/        Pruebas automáticas (xUnit) de VoltiaMX.Core, más de 800
docs/                  Sistema visual (DESIGN.md), manual de usuario y prompts por fase
```

## Créditos de terceros

- **Three.js r128**: copyright 2010-2021 three.js authors, licencia MIT. Ver `VoltiaMX/Assets/Escena3D/LICENSE-three.txt`.
- **Barlow Condensed**, **Inter** y **JetBrains Mono**: licencia SIL Open Font License 1.1. Ver los textos de licencia en `VoltiaMX/Assets/Fonts/`.
- **QuestPDF 2026.9.1** (reporte en PDF): QuestPDF Community License, gratuita para individuos, organizaciones sin fines de lucro, proyectos open source y organizaciones con ingresos brutos anuales menores a USD 1,000,000; no aplica a empresas que cotizan en bolsa ni a entidades del sector público. Se declara en el código con `QuestPDF.Settings.License = LicenseType.Community` (`VoltiaMX.Core/Services/Reportes/GeneradorPdf.cs`). Si quien publica la app deja de cumplir esas condiciones, necesita una licencia comercial. Ver https://www.questpdf.com/license.
- **PdfPig 0.1.16** (lectura de texto de PDF): licencia Apache-2.0.
- **ClosedXML 0.105.1** (exportación a Excel): licencia MIT.

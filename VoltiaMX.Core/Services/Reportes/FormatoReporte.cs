using System.Globalization;

namespace VoltiaMX.Core.Services;

/// <summary>
/// Formatos de los documentos en español de México: "12 sep 2026", "$1,572.20", "1,049.20".
/// </summary>
public static class FormatoReporte
{
    public const string FormatoPesosExcel = "$#,##0.00";

    /// <summary>
    /// Precio por kWh: de 2 a 4 decimales ($1.25, $0.982).
    /// </summary>
    public const string FormatoPrecioExcel = "$#,##0.00##";

    public const string FormatoFechaExcel = "d mmm yyyy";

    public const string FormatoKwhExcel = "#,##0.00";

    // Abreviaturas fijas: las de .NET para es-MX cambian según la versión de ICU ("sept.", "sep.").
    private static readonly string[] Meses = ["ene", "feb", "mar", "abr", "may", "jun", "jul", "ago", "sep", "oct", "nov", "dic"];

    public static string Fecha(DateOnly fecha) => $"{fecha.Day} {Meses[fecha.Month - 1]} {fecha.Year}";

    public static string Fecha(DateTime fecha) => Fecha(DateOnly.FromDateTime(fecha));

    public static string Pesos(decimal monto) => monto.ToString("$#,##0.00;-$#,##0.00", CultureInfo.InvariantCulture);

    public static string Precio(decimal precio) => precio.ToString("$#,##0.00##", CultureInfo.InvariantCulture);

    public static string Kwh(decimal kwh) => kwh.ToString("#,##0.00", CultureInfo.InvariantCulture);

    public static string Entero(decimal valor) => valor.ToString("#,##0", CultureInfo.InvariantCulture);

    public static string Porcentaje(decimal fraccion) => (fraccion * 100m).ToString("0.#", CultureInfo.InvariantCulture) + "%";
}

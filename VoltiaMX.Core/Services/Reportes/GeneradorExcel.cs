using ClosedXML.Excel;

namespace VoltiaMX.Core.Services;

/// <summary>
/// "Mis datos" en Excel: Resumen, Lecturas, Periodos, Tarifa y Aparatos. Números como números, fechas
/// como fechas y pesos con formato. Los textos del documento llegan en <c>texto(clave)</c>.
/// </summary>
public static class GeneradorExcel
{
    // Un texto de la persona que empieza así se tomaría como fórmula al editar la celda en Excel.
    private static readonly char[] InicioDeFormula = ['=', '+', '-', '@'];

    public static byte[] Generar(ReporteDatos datos, Func<string, string> texto)
    {
        ArgumentNullException.ThrowIfNull(datos);
        ArgumentNullException.ThrowIfNull(texto);

        using var libro = new XLWorkbook();
        Resumen(libro.AddWorksheet(texto("Reporte_HojaResumen")), datos, texto);
        Lecturas(libro.AddWorksheet(texto("Reporte_HojaLecturas")), datos, texto);
        Periodos(libro.AddWorksheet(texto("Reporte_HojaPeriodos")), datos, texto);
        Tarifa(libro.AddWorksheet(texto("Reporte_HojaTarifa")), datos, texto);
        Aparatos(libro.AddWorksheet(texto("Reporte_HojaAparatos")), datos, texto);

        foreach (var hoja in libro.Worksheets)
        {
            hoja.Columns().AdjustToContents();
        }

        using var memoria = new MemoryStream();
        libro.SaveAs(memoria);
        return memoria.ToArray();
    }

    private static void Resumen(IXLWorksheet hoja, ReporteDatos datos, Func<string, string> texto)
    {
        var fila = 1;
        if (datos.EsDemo)
        {
            Texto(hoja.Cell(fila, 1), texto("Reporte_DatosDeEjemplo"));
            hoja.Cell(fila, 1).Style.Font.Bold = true;
            fila++;
        }

        if (datos.PreciosDeEjemplo)
        {
            Texto(hoja.Cell(fila, 1), texto("Reporte_PreciosDeEjemplo"));
            hoja.Cell(fila, 1).Style.Font.Bold = true;
            fila++;
        }

        var filaEncabezado = fila;
        Encabezado(hoja, fila, texto("Reporte_Dato"), texto("Reporte_Valor"));
        var r = datos.Resumen;

        void Agregar(string clave, object? valor, string? formato = null)
        {
            fila++;
            Texto(hoja.Cell(fila, 1), texto(clave));
            var celda = hoja.Cell(fila, 2);
            switch (valor)
            {
                case null:
                    Texto(celda, texto("Reporte_SinDatos"));
                    break;
                case string s:
                    Texto(celda, s);
                    break;
                case DateOnly d:
                    Fecha(celda, d);
                    break;
                case decimal n:
                    celda.Value = n;
                    if (formato is not null)
                    {
                        celda.Style.NumberFormat.Format = formato;
                    }

                    break;
                case int n:
                    celda.Value = n;
                    break;
            }
        }

        Agregar("Reporte_Lugar", datos.Lugar?.Nombre);
        Agregar("Reporte_Tarifa", datos.Tarifa?.Nombre);
        Agregar("Reporte_Generado", DateOnly.FromDateTime(datos.GeneradoEl));
        Agregar("Reporte_Inicio", r?.InicioPeriodo);
        Agregar("Reporte_Fin", r?.FinPeriodo);
        Agregar("Reporte_KwhHastaHoy", r?.KwhHastaHoy, FormatoReporte.FormatoKwhExcel);
        Agregar("Reporte_KwhProyectados", r?.KwhProyectados, FormatoReporte.FormatoKwhExcel);
        Agregar("Reporte_PagoProyectado", r?.MontoProyectado, FormatoReporte.FormatoPesosExcel);
        // El presupuesto no se exporta desde la Fase 7a.
        Agregar("Reporte_Nivel", r?.Dac is { } dac ? texto($"Dac_{dac.Nivel}") : null);
        Agregar("Reporte_Promedio", r?.Dac?.PromedioMensualKwh, FormatoReporte.FormatoKwhExcel);
        Agregar("Reporte_Limite", r?.LimiteDacKwhMensual, FormatoReporte.FormatoKwhExcel);
        Agregar("Reporte_MetaPorcentaje", datos.Meta is { } meta ? meta.PorcentajeObjetivo / 100m : null, "0%");
        Agregar("Reporte_Estado", datos.Meta is { } m ? texto($"EstadoMeta_{m.Estado}") : null);
        Fijar(hoja, fila: filaEncabezado);
    }

    private static void Lecturas(IXLWorksheet hoja, ReporteDatos datos, Func<string, string> texto)
    {
        Encabezado(hoja, 1, texto("Reporte_Fecha"), texto("Reporte_LecturaAcumulada"), texto("Reporte_ConsumoDesdeAnterior"), texto("Reporte_Estimada"));
        var fila = 1;
        foreach (var lectura in datos.Lecturas)
        {
            fila++;
            Fecha(hoja.Cell(fila, 1), lectura.Fecha);
            Numero(hoja.Cell(fila, 2), lectura.ValorKwh, FormatoReporte.FormatoKwhExcel);
            Numero(hoja.Cell(fila, 3), lectura.ConsumoDesdeAnterior, FormatoReporte.FormatoKwhExcel);
            Texto(hoja.Cell(fila, 4), texto(lectura.EsEstimada ? "Reporte_Si" : "Reporte_No"));
        }

        Fijar(hoja, 1);
    }

    private static void Periodos(IXLWorksheet hoja, ReporteDatos datos, Func<string, string> texto)
    {
        Encabezado(hoja, 1,
            texto("Reporte_Inicio"), texto("Reporte_Fin"), texto("Reporte_Kwh"), texto("Reporte_MontoCalculado"),
            texto("Reporte_MontoRecibo"), texto("Reporte_EstimadoColumna"));
        var fila = 1;

        // Solo los cerrados: el actual es una proyección y va en el Resumen.
        foreach (var periodo in datos.Periodos.Where(p => !p.EsActual))
        {
            fila++;
            Fecha(hoja.Cell(fila, 1), periodo.Inicio);
            Fecha(hoja.Cell(fila, 2), periodo.Fin);
            Numero(hoja.Cell(fila, 3), periodo.Kwh, FormatoReporte.FormatoKwhExcel);
            Numero(hoja.Cell(fila, 4), periodo.Monto, FormatoReporte.FormatoPesosExcel);
            Numero(hoja.Cell(fila, 5), periodo.MontoRecibo, FormatoReporte.FormatoPesosExcel);
            Texto(hoja.Cell(fila, 6), texto(periodo.Estimado ? "Reporte_Si" : "Reporte_No"));
        }

        Fijar(hoja, 1);
    }

    private static void Tarifa(IXLWorksheet hoja, ReporteDatos datos, Func<string, string> texto)
    {
        Encabezado(hoja, 1, texto("Reporte_Rango"), texto("Reporte_Desde"), texto("Reporte_Hasta"), texto("Reporte_Precio"));
        var fila = 1;
        if (datos.Tarifa is { } tarifa)
        {
            foreach (var rango in tarifa.Rangos)
            {
                fila++;
                Texto(hoja.Cell(fila, 1), rango.Nombre);
                Numero(hoja.Cell(fila, 2), rango.Desde, FormatoReporte.FormatoKwhExcel);
                Numero(hoja.Cell(fila, 3), rango.Hasta, FormatoReporte.FormatoKwhExcel);
                Numero(hoja.Cell(fila, 4), rango.PrecioPorKwh, FormatoReporte.FormatoPrecioExcel);
            }

            fila += 2;
            Texto(hoja.Cell(fila, 1), texto("Reporte_Tarifa"));
            Texto(hoja.Cell(fila, 2), tarifa.Nombre);
            fila++;
            Texto(hoja.Cell(fila, 1), texto("Reporte_CargoFijoMensual"));
            Numero(hoja.Cell(fila, 2), tarifa.CargoFijoMensual, FormatoReporte.FormatoPesosExcel);
            fila++;
            Texto(hoja.Cell(fila, 1), texto("Reporte_LimiteDac"));
            if (tarifa.LimiteDacKwhMensual is decimal limite)
            {
                Numero(hoja.Cell(fila, 2), limite, FormatoReporte.FormatoKwhExcel);
            }
            else
            {
                Texto(hoja.Cell(fila, 2), texto("Reporte_SinDatos"));
            }
        }

        Fijar(hoja, 1);
    }

    private static void Aparatos(IXLWorksheet hoja, ReporteDatos datos, Func<string, string> texto)
    {
        Encabezado(hoja, 1,
            texto("Reporte_Nombre"), texto("Reporte_Categoria"), texto("Reporte_Cantidad"), texto("Reporte_Watts"),
            texto("Reporte_HorasPorDia"), texto("Reporte_DiasPorSemana"), texto("Reporte_Ciclo"), texto("Reporte_KwhMes"));
        var fila = 1;
        foreach (var aparato in datos.Aparatos)
        {
            fila++;
            Texto(hoja.Cell(fila, 1), aparato.Nombre);
            Texto(hoja.Cell(fila, 2), texto($"CategoriaAparato_{aparato.Categoria}"));
            hoja.Cell(fila, 3).Value = aparato.Cantidad;
            Numero(hoja.Cell(fila, 4), aparato.Watts, "#,##0.##");
            Numero(hoja.Cell(fila, 5), aparato.HorasPorDia, "0.##");
            hoja.Cell(fila, 6).Value = aparato.DiasPorSemana;
            Numero(hoja.Cell(fila, 7), aparato.CicloDeTrabajo, "0.00");
            Numero(hoja.Cell(fila, 8), aparato.KwhPorMes, FormatoReporte.FormatoKwhExcel);
        }

        Fijar(hoja, 1);
    }

    private static void Encabezado(IXLWorksheet hoja, int fila, params string[] titulos)
    {
        for (var i = 0; i < titulos.Length; i++)
        {
            Texto(hoja.Cell(fila, i + 1), titulos[i]);
        }

        hoja.Row(fila).Style.Font.Bold = true;
    }

    private static void Fijar(IXLWorksheet hoja, int fila) => hoja.SheetView.FreezeRows(fila);

    /// <summary>
    /// Siempre como texto literal; si parece fórmula, con apóstrofo de texto para que Excel no la evalúe.
    /// </summary>
    private static void Texto(IXLCell celda, string? valor)
    {
        valor ??= string.Empty;
        celda.Value = valor;
        if (valor.Length > 0 && InicioDeFormula.Contains(valor[0]))
        {
            celda.Style.IncludeQuotePrefix = true;
        }
    }

    private static void Numero(IXLCell celda, decimal? valor, string formato)
    {
        if (valor is not decimal numero)
        {
            return;
        }

        celda.Value = numero;
        celda.Style.NumberFormat.Format = formato;
    }

    private static void Fecha(IXLCell celda, DateOnly fecha)
    {
        celda.Value = fecha.ToDateTime(TimeOnly.MinValue);
        celda.Style.DateFormat.Format = FormatoReporte.FormatoFechaExcel;
    }
}

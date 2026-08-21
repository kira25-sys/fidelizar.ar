using Fidelizar.Client.Formatting;
using Fidelizar.Shared.Sucursales;

namespace Fidelizar.Api.Tests;

/// <summary>
/// S9's export (FUNCTIONAL-SPEC §9). Pure string building, so it is tested here rather than in
/// the browser — Fidelizar.Api.Tests already references Client (see MontoTecleadoParserTests).
/// Every member name below is invented.
/// </summary>
public class CierreDiarioCsvTests
{
    private static CierreDiarioResponse Cierre(params CierreDiarioMovimiento[] movimientos) =>
        new(3, new DateOnly(2026, 8, 21), movimientos.Sum(m => m.Monto), movimientos);

    private static CierreDiarioMovimiento Canje(
        string socio = "Ana Gómez",
        decimal monto = 1500m,
        string cajero = "Lucía Paz",
        string? motivo = "Descuento") =>
        new(socio, monto, cajero, new DateTime(2026, 8, 21, 13, 5, 0, DateTimeKind.Utc), motivo);

    [Fact]
    public void Empieza_con_el_BOM_que_Excel_necesita_para_los_acentos()
    {
        var csv = CierreDiarioCsv.Construir(Cierre(Canje()), "Centro");

        Assert.StartsWith("﻿", csv, StringComparison.Ordinal);
    }

    [Fact]
    public void Trae_la_sucursal_y_la_fecha_para_que_el_archivo_se_pueda_archivar()
    {
        var csv = CierreDiarioCsv.Construir(Cierre(Canje()), "Centro");

        Assert.Contains("Sucursal;Centro\r\n", csv, StringComparison.Ordinal);
        Assert.Contains("Fecha;21/08/2026\r\n", csv, StringComparison.Ordinal);
        Assert.Contains("Canjes;1\r\n", csv, StringComparison.Ordinal);
    }

    [Fact]
    public void Escribe_una_fila_por_canje_con_las_cinco_columnas()
    {
        var csv = CierreDiarioCsv.Construir(Cierre(Canje()), "Centro");

        Assert.Contains("Socio;Monto;Cajero;Hora de registro;Motivo\r\n", csv, StringComparison.Ordinal);
        Assert.Contains("Ana Gómez;1500,00;Lucía Paz;", csv, StringComparison.Ordinal);
        Assert.Contains(";Descuento\r\n", csv, StringComparison.Ordinal);
    }

    [Fact]
    public void Cierra_con_el_total_al_pie()
    {
        var csv = CierreDiarioCsv.Construir(Cierre(Canje(monto: 1500m), Canje(monto: 250.5m)), "Centro");

        Assert.EndsWith("Total canjeado;1750,50\r\n", csv, StringComparison.Ordinal);
    }

    [Fact]
    public void Un_dia_sin_canjes_sigue_siendo_un_archivo_valido()
    {
        var csv = CierreDiarioCsv.Construir(Cierre(), "Centro");

        Assert.Contains("Canjes;0\r\n", csv, StringComparison.Ordinal);
        Assert.EndsWith("Total canjeado;0,00\r\n", csv, StringComparison.Ordinal);
    }

    [Fact]
    public void Un_motivo_vacio_deja_la_celda_vacia_y_no_rompe_la_fila()
    {
        var csv = CierreDiarioCsv.Construir(Cierre(Canje(motivo: null)), "Centro");

        Assert.Contains("Ana Gómez;1500,00;Lucía Paz;21/08/2026", csv, StringComparison.Ordinal);
        Assert.DoesNotContain("Descuento", csv, StringComparison.Ordinal);
    }

    /// <summary>RFC 4180: a field holding the separator or a quote is quoted, and the quote doubled.</summary>
    [Theory]
    [InlineData("Cumple; medio kilo", "\"Cumple; medio kilo\"")]
    [InlineData("Promo \"2x1\"", "\"Promo \"\"2x1\"\"\"")]
    [InlineData("Dos\nlineas", "\"Dos\nlineas\"")]
    public void Un_motivo_con_separador_o_comillas_no_corre_las_columnas(string motivo, string esperado)
    {
        var csv = CierreDiarioCsv.Construir(Cierre(Canje(motivo: motivo)), "Centro");

        Assert.Contains(esperado, csv, StringComparison.Ordinal);
    }

    /// <summary>A Motivo typed at the counter is free text; a cell starting with "=" is a
    /// formula, not a reason.</summary>
    [Theory]
    [InlineData("=1+1")]
    [InlineData("+A1")]
    [InlineData("@SUM(A1)")]
    [InlineData("-cmd")]
    public void Un_motivo_que_parece_formula_se_neutraliza(string motivo)
    {
        var csv = CierreDiarioCsv.Construir(Cierre(Canje(motivo: motivo)), "Centro");

        Assert.Contains("'" + motivo, csv, StringComparison.Ordinal);
    }

    [Fact]
    public void Un_monto_negativo_sigue_siendo_un_numero_y_no_texto()
    {
        var csv = CierreDiarioCsv.Construir(Cierre(Canje(monto: -100m)), "Centro");

        Assert.Contains(";-100,00;", csv, StringComparison.Ordinal);
        Assert.DoesNotContain("'-100,00", csv, StringComparison.Ordinal);
    }

    [Fact]
    public void El_nombre_del_archivo_lleva_sucursal_y_fecha_ordenable()
    {
        Assert.Equal("cierre-diario-sucursal-3-2026-08-21.csv", CierreDiarioCsv.NombreArchivo(Cierre()));
    }
}

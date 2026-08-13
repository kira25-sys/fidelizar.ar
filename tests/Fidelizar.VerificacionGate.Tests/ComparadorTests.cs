using Fidelizar.VerificacionGate.Comparacion;

namespace Fidelizar.VerificacionGate.Tests;

/// <summary>
/// Every id here is invented (CLAUDE.md: a real member never becomes a test case, not even for
/// F0-11). <see cref="Comparador"/> is pure — no I/O — so these fixtures are the whole test.
/// </summary>
public sealed class ComparadorTests
{
    [Fact]
    public void Cuando_las_tres_puntas_coinciden_no_hay_discrepancias_y_el_gate_esta_cumplido()
    {
        var postgres = new Dictionary<string, decimal> { ["TEST-001"] = 100.00m, ["TEST-002"] = 250.50m };
        var octaviano = new Dictionary<string, decimal> { ["TEST-001"] = 100.00m, ["TEST-002"] = 250.50m };
        var planilla = new List<FilaPlanilla>
        {
            new("Hoja Test", 5, "TEST-001", 100.00m),
            new("Hoja Test", 6, "TEST-002", 250.50m),
        };

        var resultado = Comparador.Comparar(postgres, octaviano, planilla);

        Assert.Empty(resultado.Discrepancias);
        Assert.True(resultado.GateCumplido);
        Assert.Equal(2, resultado.TotalSociosComparados);
    }

    [Fact]
    public void Una_diferencia_de_un_centavo_es_una_discrepancia_no_ruido()
    {
        var postgres = new Dictionary<string, decimal> { ["TEST-001"] = 100.01m };
        var octaviano = new Dictionary<string, decimal> { ["TEST-001"] = 100.00m };
        var planilla = new List<FilaPlanilla> { new("Hoja Test", 5, "TEST-001", 100.00m) };

        var resultado = Comparador.Comparar(postgres, octaviano, planilla);

        var discrepancia = Assert.Single(resultado.Discrepancias);
        Assert.Equal("TEST-001", discrepancia.ClienteExternoId);
        Assert.Equal(CausaDiscrepancia.MontoDistinto, discrepancia.Causa);
        Assert.Equal(0.01m, discrepancia.DiferenciaMaxima());
        Assert.False(resultado.GateCumplido);
    }

    [Fact]
    public void Un_socio_ausente_en_Postgres_se_reporta_como_FaltaEnPostgres()
    {
        var postgres = new Dictionary<string, decimal>();
        var octaviano = new Dictionary<string, decimal> { ["TEST-001"] = 50.00m };
        var planilla = new List<FilaPlanilla> { new("Hoja Test", 5, "TEST-001", 50.00m) };

        var resultado = Comparador.Comparar(postgres, octaviano, planilla);

        var discrepancia = Assert.Single(resultado.Discrepancias);
        Assert.Equal(CausaDiscrepancia.FaltaEnPostgres, discrepancia.Causa);
        Assert.Null(discrepancia.SaldoPostgres);
        Assert.Equal(50.00m, discrepancia.SaldoOctaviano);
        Assert.Equal(50.00m, discrepancia.SaldoPlanilla);
    }

    [Fact]
    public void Un_socio_ausente_en_octaviano_se_reporta_como_FaltaEnOctaviano()
    {
        var postgres = new Dictionary<string, decimal> { ["TEST-001"] = 50.00m };
        var octaviano = new Dictionary<string, decimal>();
        var planilla = new List<FilaPlanilla> { new("Hoja Test", 5, "TEST-001", 50.00m) };

        var resultado = Comparador.Comparar(postgres, octaviano, planilla);

        var discrepancia = Assert.Single(resultado.Discrepancias);
        Assert.Equal(CausaDiscrepancia.FaltaEnOctaviano, discrepancia.Causa);
        Assert.Null(discrepancia.SaldoOctaviano);
    }

    [Fact]
    public void Un_socio_ausente_en_la_planilla_se_reporta_como_FaltaEnPlanilla()
    {
        var postgres = new Dictionary<string, decimal> { ["TEST-001"] = 50.00m };
        var octaviano = new Dictionary<string, decimal> { ["TEST-001"] = 50.00m };
        var planilla = new List<FilaPlanilla>();

        var resultado = Comparador.Comparar(postgres, octaviano, planilla);

        var discrepancia = Assert.Single(resultado.Discrepancias);
        Assert.Equal(CausaDiscrepancia.FaltaEnPlanilla, discrepancia.Causa);
        Assert.Null(discrepancia.SaldoPlanilla);
    }

    [Fact]
    public void Una_fila_de_planilla_sin_ClienteExternoId_se_reporta_como_caso_borde_y_no_participa_de_la_comparacion()
    {
        var postgres = new Dictionary<string, decimal> { ["TEST-001"] = 50.00m };
        var octaviano = new Dictionary<string, decimal> { ["TEST-001"] = 50.00m };
        // Row 9 mimics a real per-branch "Total" footer row: no id, only aggregate numbers.
        var planilla = new List<FilaPlanilla>
        {
            new("Hoja Test", 5, "TEST-001", 50.00m),
            new("Hoja Test", 9, null, 999.99m),
        };

        var resultado = Comparador.Comparar(postgres, octaviano, planilla);

        Assert.Empty(resultado.Discrepancias);
        Assert.True(resultado.GateCumplido);
        Assert.Contains(resultado.CasosBorde, c => c.Descripcion.Contains("fila 9") && c.Descripcion.Contains("sin ClienteExternoId"));
    }

    [Fact]
    public void Un_ClienteExternoId_repetido_en_la_planilla_se_excluye_de_la_comparacion_y_se_reporta()
    {
        var postgres = new Dictionary<string, decimal> { ["TEST-001"] = 50.00m };
        var octaviano = new Dictionary<string, decimal> { ["TEST-001"] = 50.00m };
        var planilla = new List<FilaPlanilla>
        {
            new("Hoja A", 5, "TEST-001", 50.00m),
            new("Hoja B", 12, "TEST-001", 999.00m),
        };

        var resultado = Comparador.Comparar(postgres, octaviano, planilla);

        // Excluded from saldosPlanilla entirely -> shows up as FaltaEnPlanilla, not as a silent
        // pick of either row.
        var discrepancia = Assert.Single(resultado.Discrepancias);
        Assert.Equal(CausaDiscrepancia.FaltaEnPlanilla, discrepancia.Causa);
        Assert.Contains(resultado.CasosBorde, c => c.ClienteExternoId == "TEST-001" && c.Descripcion.Contains("aparece 2 veces"));
    }

    [Fact]
    public void Las_sumas_de_control_reflejan_solo_los_socios_realmente_comparables()
    {
        var postgres = new Dictionary<string, decimal> { ["TEST-001"] = 10.00m, ["TEST-002"] = 20.00m };
        var octaviano = new Dictionary<string, decimal> { ["TEST-001"] = 10.00m, ["TEST-002"] = 20.00m };
        var planilla = new List<FilaPlanilla>
        {
            new("Hoja Test", 5, "TEST-001", 10.00m),
            new("Hoja Test", 6, "TEST-002", 20.00m),
        };

        var resultado = Comparador.Comparar(postgres, octaviano, planilla);

        Assert.Equal(30.00m, resultado.SumaPostgres);
        Assert.Equal(30.00m, resultado.SumaOctaviano);
        Assert.Equal(30.00m, resultado.SumaPlanilla);
    }

    [Fact]
    public void Sin_ninguna_discrepancia_ni_socios_el_gate_esta_trivialmente_cumplido()
    {
        var resultado = Comparador.Comparar(
            new Dictionary<string, decimal>(), new Dictionary<string, decimal>(), []);

        Assert.True(resultado.GateCumplido);
        Assert.Equal(0, resultado.TotalSociosComparados);
    }
}

using Fidelizar.Client.Formatting;
using Fidelizar.Domain.Money;

namespace Fidelizar.Api.Tests;

/// <summary>
/// Pins <see cref="MontoTecleadoParser"/> (Client) to
/// <see cref="MontoParser.TryParseMontoManual"/> (Domain). Client references only Shared
/// (ARCHITECTURE §3), so the rule exists twice on purpose; these tests are what stops the two
/// copies from drifting. They live here because Fidelizar.Api.Tests already references both
/// assemblies — Client for <c>DependencyDirectionTests</c>, Domain through Api.
/// </summary>
public class MontoTecleadoParserTests
{
    /// <summary>Every shape the two parsers must agree on, including the ones that must fail.</summary>
    public static TheoryData<string> Entradas =>
    [
        // The examples MontoParser's own summary fixes.
        "1234.50", "1234,50", "1.234,50", "1,234.50", "1.500", "1,500", "1.2345",
        // Thousands, repeated separators, and money noise.
        "1.234.567", "1,234,567", "12400", "$ 1.500", " 1.500 ", "$1.500,50",
        // Negative: the padron importer accepts it, so the shared rule must too.
        "-500", "-1.500,25",
        // Not amounts.
        "", "   ", "abc", "1.2.3,4,5", "$", "-", "1a500", "1..500", "1,,500",
        // Edge digit counts around the ambiguous three.
        "1.5", "1.50", "1.5000", "1,5", "1,50", "1,5000",
    ];

    [Theory]
    [MemberData(nameof(Entradas))]
    public void Coincide_con_MontoParser_en_modo_tecleado(string entrada)
    {
        var esperadoOk = MontoParser.TryParseMontoManual(entrada, out var esperado);

        var obtenidoOk = MontoTecleadoParser.TryParse(entrada, out var obtenido);

        Assert.Equal(esperadoOk, obtenidoOk);
        Assert.Equal(esperado, obtenido);
    }

    [Theory]
    // River Plate convention on the one ambiguous case: dot is thousands, comma is decimal.
    [InlineData("1.500", 1500)]
    [InlineData("1,500", 1.5)]
    // Any digit count other than three is unambiguously decimal, whichever separator it is.
    [InlineData("1234.50", 1234.50)]
    [InlineData("1234,50", 1234.50)]
    [InlineData("1.2345", 1.2345)]
    // Both separators: the rightmost one is the decimal.
    [InlineData("1.234,50", 1234.50)]
    [InlineData("1,234.50", 1234.50)]
    // Repeated separator can only be thousands.
    [InlineData("1.234.567", 1234567)]
    // "$" and whitespace are stripped before anything else.
    [InlineData("$ 1.500", 1500)]
    [InlineData("12400", 12400)]
    public void Lee_el_monto_que_tecleo_la_cajera(string entrada, decimal esperado)
    {
        Assert.True(MontoTecleadoParser.TryParse(entrada, out var monto));
        Assert.Equal(esperado, monto);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("abc")]
    [InlineData("1a500")]
    [InlineData("$")]
    public void Rechaza_lo_que_no_es_un_monto(string? entrada)
    {
        Assert.False(MontoTecleadoParser.TryParse(entrada, out var monto));
        Assert.Equal(0m, monto);
    }

    /// <summary>
    /// The 100x bug MontoParser's summary records: with <c>NumberStyles.Any</c>, "1234,50" read
    /// the comma as a thousands separator and produced 123450. S4 would have redeemed a hundred
    /// times the amount the cashier typed.
    /// </summary>
    [Fact]
    public void No_reintroduce_el_error_de_cien_veces()
    {
        Assert.True(MontoTecleadoParser.TryParse("1234,50", out var monto));

        Assert.Equal(1234.50m, monto);
        Assert.NotEqual(123450m, monto);
    }
}

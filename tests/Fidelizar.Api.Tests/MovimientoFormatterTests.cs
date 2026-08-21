using Fidelizar.Client.Formatting;
using Fidelizar.Domain.Entities;

namespace Fidelizar.Api.Tests;

/// <summary>
/// Pins <see cref="MovimientoFormatter"/> (Client) to the wire contract it mirrors. Client
/// references only Shared (ARCHITECTURE §3), so the type names travel as strings and this is
/// what stops the copy from drifting from <see cref="TipoMovimientoCredito"/>. They live here
/// because Fidelizar.Api.Tests already references both assemblies — Client for
/// <c>DependencyDirectionTests</c>, Domain through Api.
/// </summary>
public class MovimientoFormatterTests
{
    /// <summary>Every value of the Domain enum has a Spanish label. A type added to the enum
    /// without one shows up in S7 and S8 under its raw name, never blank.</summary>
    [Fact]
    public void EtiquetaTipo_NamesEveryDomainType()
    {
        foreach (var tipo in Enum.GetNames<TipoMovimientoCredito>())
        {
            var etiqueta = MovimientoFormatter.EtiquetaTipo(tipo);

            Assert.False(string.IsNullOrWhiteSpace(etiqueta));
        }
    }

    [Theory]
    [InlineData("SaldoInicial", "Saldo inicial")]
    [InlineData("Acumulacion", "Acumulación")]
    [InlineData("Canje", "Canje")]
    [InlineData("Ajuste", "Ajuste")]
    public void EtiquetaTipo_UsesTheSpanishWordTheBusinessUses(string tipo, string esperado) =>
        Assert.Equal(esperado, MovimientoFormatter.EtiquetaTipo(tipo));

    /// <summary>Append-only Tipo (DATA-MODEL §4): an unknown value renders, it never vanishes.</summary>
    [Fact]
    public void EtiquetaTipo_FallsBackToTheRawName() =>
        Assert.Equal("TipoQueNoExisteTodavia", MovimientoFormatter.EtiquetaTipo("TipoQueNoExisteTodavia"));

    [Theory]
    [InlineData("Acumulacion", "badge--success")]
    [InlineData("Ajuste", "badge--warning")]
    [InlineData("Canje", "badge--neutral")]
    [InlineData("SaldoInicial", "badge--neutral")]
    [InlineData("TipoQueNoExisteTodavia", "badge--neutral")]
    public void ClaseBadgeTipo_NeverLeavesARowUnstyled(string tipo, string esperada) =>
        Assert.Equal(esperada, MovimientoFormatter.ClaseBadgeTipo(tipo));

    [Theory]
    [InlineData(1500, "+$ 1.500")]
    [InlineData(-1500, "-$ 1.500")]
    [InlineData(0, "$ 0")]
    public void MontoConSigno_MakesTheDirectionExplicit(decimal monto, string esperado) =>
        Assert.Equal(esperado, MovimientoFormatter.MontoConSigno(monto));

    /// <summary>
    /// FUNCTIONAL-SPEC §8: voiding movement M writes an Ajuste of -M.Monto. This is the figure
    /// S8 shows before confirming; the server computes its own from the original row
    /// (AnulacionMovimientoService), so the two must agree or the dialog is lying about what is
    /// about to be written.
    /// </summary>
    [Theory]
    [InlineData(1500, -1500)]
    [InlineData(-1500, 1500)]
    [InlineData(0, 0)]
    [InlineData(1234.56, -1234.56)]
    public void MontoDelAjuste_IsTheExactOpposite(decimal original, decimal esperado) =>
        Assert.Equal(esperado, MovimientoFormatter.MontoDelAjuste(original));

    /// <summary>Voiding the correction restores the original amount — an Ajuste is itself
    /// voidable and the arithmetic has to survive the round trip.</summary>
    [Theory]
    [InlineData(1500)]
    [InlineData(-0.01)]
    [InlineData(987654.32)]
    public void MontoDelAjuste_RoundTripsToTheOriginal(decimal original) =>
        Assert.Equal(original, MovimientoFormatter.MontoDelAjuste(MovimientoFormatter.MontoDelAjuste(original)));
}

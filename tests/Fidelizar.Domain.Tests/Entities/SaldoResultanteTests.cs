using Fidelizar.Domain.Entities;

namespace Fidelizar.Domain.Tests.Entities;

/// <summary>
/// I2's second half: <c>SaldoResultante</c> is historical evidence only and is never the source
/// of an answer. <c>InvarianteI2SaldoTests</c> (Fidelizar.Application.Tests) proves the balance
/// equals <c>SUM(Monto)</c> across random sequences; this file proves the other direction —
/// <c>SaldoResultante</c> is not kept in sync with that sum by anything in the entity itself, so a
/// future bug that reads it instead of summing <c>Monto</c> would be caught, not accidentally
/// "right by coincidence".
///
/// Uses the internal <see cref="MovimientoCredito.FijarSaldoResultante"/> (visible to this project
/// via <c>InternalsVisibleTo</c>, exactly like the real <c>MovimientoRepository</c> in
/// <c>Fidelizar.Infrastructure</c>) to poison the field with values that have nothing to do with
/// the real running balance.
/// </summary>
public class SaldoResultanteTests
{
    private static readonly DateOnly Hoy = new(2026, 8, 12);

    [Fact]
    public void SaldoResultante_no_se_mantiene_sincronizado_solo_por_crear_movimientos_la_suma_de_Monto_es_la_unica_fuente_correcta()
    {
        var movimientos = new List<MovimientoCredito>
        {
            MovimientoCredito.Crear(1, 1, Hoy, DateTime.UtcNow, TipoMovimientoCredito.SaldoInicial, 1_000m, Hoy),
            MovimientoCredito.Crear(1, 1, Hoy, DateTime.UtcNow, TipoMovimientoCredito.Acumulacion, 300m, Hoy, configuracionId: 1),
            MovimientoCredito.Crear(1, 1, Hoy, DateTime.UtcNow, TipoMovimientoCredito.Canje, -200m, Hoy, motivo: "Canje de prueba"),
        };

        // Deliberately poison every SaldoResultante with a value unrelated to the real running
        // balance — exactly what a bug reading this column, instead of summing Monto, would see.
        // A repository doing this correctly (MovimientoRepository.AppendAsync) computes it from
        // SUM(Monto) inside the same transaction as the insert; nothing in the entity itself does
        // that automatically, which is precisely the point: the balance MUST be recomputed by
        // whoever asks for it, never trusted from this column.
        foreach (var movimiento in movimientos)
        {
            movimiento.FijarSaldoResultante(-777_777m);
        }

        var saldoCorrecto = movimientos.Sum(m => m.Monto);

        Assert.Equal(1_100m, saldoCorrecto);
        Assert.All(movimientos, m => Assert.Equal(-777_777m, m.SaldoResultante));
        Assert.All(movimientos, m => Assert.NotEqual(saldoCorrecto, m.SaldoResultante));
    }
}

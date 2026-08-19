using Fidelizar.Application.Services;
using Fidelizar.Application.Tests.Fakes;
using Fidelizar.Domain.Entities;
using Fidelizar.Domain.Exceptions;
using Fidelizar.Domain.Money;

namespace Fidelizar.Application.Tests.Services;

/// <summary>
/// I2 — the balance is always <c>SUM(Monto)</c>, after an arbitrary sequence of operations, never
/// read from a stored column. <c>SaldoServiceTests</c> already covers this with one fixed
/// sequence (accrual, redemption, adjustment); this file is the property-based version ROADMAP
/// F0-10 asks for: many pseudo-random sequences of accruals, redemptions (through the real
/// <see cref="SaldoService"/>, so RN-24/RN-25 rejections are exercised too) and adjustments,
/// checking the invariant holds after <b>every single operation</b>, not just at the end.
///
/// No property-based testing package (e.g. FsCheck) is referenced — ARCHITECTURE §2 requires
/// approval before adding any dependency, and a seeded <see cref="Random"/> driving many trials
/// gets the same guarantee (a fixed seed keeps the run deterministic, so this cannot flake in CI).
/// </summary>
public class InvarianteI2SaldoTests
{
    private const int NegocioId = 1;
    private const int MiembroId = 42;
    private const int Trials = 60;
    private const int OperationsPerTrial = 40;
    private static readonly DateOnly Fecha = new(2026, 1, 15);

    /// <summary>
    /// I2, property-based: whatever pseudo-random mix of Acumulacion/Canje/Ajuste ran, the
    /// balance the service reports always equals an independently-computed <c>SUM(Monto)</c> over
    /// every movement actually stored — checked after each individual operation, across many
    /// randomised sequences.
    /// </summary>
    [Fact]
    public async Task Saldo_es_la_suma_de_los_movimientos_tras_secuencias_arbitrarias_de_operaciones()
    {
        // Fixed seed: this must never be a flaky test. The same "random" sequences run every time.
        var random = new Random(20260812);

        for (var trial = 0; trial < Trials; trial++)
        {
            var repositorio = new FakeMovimientoRepository();
            var miembroRepositorio = new FakeMiembroRepository();
            miembroRepositorio.SembrarNuevo(NegocioId, MiembroId);
            var servicio = new SaldoService(repositorio, miembroRepositorio);

            for (var op = 0; op < OperationsPerTrial; op++)
            {
                await EjecutarOperacionAleatoria(random, servicio, repositorio);

                var saldoDelServicio = await servicio.ObtenerSaldoAsync(NegocioId, MiembroId);
                var sumaIndependiente = repositorio.Movimientos
                    .Where(m => m.NegocioId == NegocioId && m.MiembroId == MiembroId)
                    .Sum(m => m.Monto);

                Assert.Equal(sumaIndependiente, saldoDelServicio);
            }
        }
    }

    private static async Task EjecutarOperacionAleatoria(
        Random random, SaldoService servicio, FakeMovimientoRepository repositorio)
    {
        switch (random.Next(3))
        {
            case 0: // Acumulacion
                var montoAcumulacion = Redondeo.Monto((decimal)(random.NextDouble() * 5_000));
                await repositorio.AppendAsync(MovimientoCredito.Crear(
                    NegocioId, MiembroId, Fecha, DateTime.UtcNow, TipoMovimientoCredito.Acumulacion,
                    montoAcumulacion, Fecha, configuracionId: 1));
                break;

            case 1: // Canje — through the real service, so RN-24/RN-25 rejections happen for real
                var montoCanje = Redondeo.Monto((decimal)(random.NextDouble() * 4_000));
                if (montoCanje <= 0m)
                {
                    break;
                }

                try
                {
                    var request = new RegistrarCanjeRequest(
                        NegocioId, MiembroId, montoCanje, "Canje generado para la prueba de propiedad I2",
                        UsuarioId: null, Fecha, Fecha);
                    await servicio.RegistrarCanjeAsync(request);
                }
                catch (ValidationException)
                {
                    // Rejected on purpose sometimes (canje mayor al saldo, o saldo en revisión —
                    // I6/RN-24/RN-25). A rejected canje must leave no trace, which is exactly what
                    // the invariant check right after this call verifies.
                }

                break;

            default: // Ajuste
                var montoAjuste = Redondeo.Monto((decimal)((random.NextDouble() - 0.5) * 3_000));
                await repositorio.AppendAsync(MovimientoCredito.Crear(
                    NegocioId, MiembroId, Fecha, DateTime.UtcNow, TipoMovimientoCredito.Ajuste,
                    montoAjuste, Fecha, motivo: "Ajuste generado para la prueba de propiedad I2"));
                break;
        }
    }
}

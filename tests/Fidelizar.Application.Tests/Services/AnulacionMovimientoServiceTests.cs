using Fidelizar.Application.Services;
using Fidelizar.Application.Tests.Fakes;
using Fidelizar.Domain.Entities;
using Fidelizar.Domain.Exceptions;

namespace Fidelizar.Application.Tests.Services;

/// <summary>S8 Anular movimiento — I1/I3: nunca un edit, nunca un delete, solo un <c>Ajuste</c>
/// nuevo de <c>-Monto</c>.</summary>
public class AnulacionMovimientoServiceTests
{
    private const int NegocioId = 1;
    private const int MiembroId = 42;
    private const int UsuarioId = 9;
    private static readonly DateOnly Hoy = new(2026, 8, 19);

    private static AnulacionMovimientoService CrearServicio(out FakeMovimientoRepository repositorio)
    {
        repositorio = new FakeMovimientoRepository();
        return new AnulacionMovimientoService(repositorio);
    }

    [Fact]
    public async Task Movimiento_inexistente_lanza_EntityNotFoundException()
    {
        var servicio = CrearServicio(out _);

        var request = new AnularMovimientoRequest(NegocioId, 999, "Corrección", UsuarioId, Hoy);

        await Assert.ThrowsAsync<EntityNotFoundException>(() => servicio.AnularAsync(request));
    }

    /// <summary>I8: un movimiento de otro negocio debe leerse exactamente como inexistente.</summary>
    [Fact]
    public async Task Movimiento_de_otro_negocio_lanza_EntityNotFoundException()
    {
        var servicio = CrearServicio(out var repositorio);
        var original = await repositorio.AppendAsync(MovimientoCredito.Crear(
            negocioId: 2, MiembroId, Hoy, DateTime.UtcNow, TipoMovimientoCredito.Canje, -300m, Hoy, motivo: "Canje"));

        var request = new AnularMovimientoRequest(NegocioId, original.Id, "Corrección", UsuarioId, Hoy);

        await Assert.ThrowsAsync<EntityNotFoundException>(() => servicio.AnularAsync(request));
    }

    /// <summary>I3: todo Ajuste necesita un motivo obligatorio.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Sin_motivo_se_rechaza(string motivo)
    {
        var servicio = CrearServicio(out var repositorio);
        var original = await repositorio.AppendAsync(MovimientoCredito.Crear(
            NegocioId, MiembroId, Hoy, DateTime.UtcNow, TipoMovimientoCredito.Canje, -300m, Hoy, motivo: "Canje"));

        var request = new AnularMovimientoRequest(NegocioId, original.Id, motivo, UsuarioId, Hoy);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => servicio.AnularAsync(request));
        Assert.Equal("MOTIVO_REQUERIDO", ex.ErrorCode);
    }

    /// <summary>I1/I3: la anulación escribe un Ajuste nuevo de -Monto y nunca toca la fila original.</summary>
    [Fact]
    public async Task Escribe_un_Ajuste_de_signo_opuesto_y_no_modifica_el_original()
    {
        var servicio = CrearServicio(out var repositorio);
        var original = await repositorio.AppendAsync(MovimientoCredito.Crear(
            NegocioId, MiembroId, Hoy, DateTime.UtcNow, TipoMovimientoCredito.Canje, -300m, Hoy, motivo: "Canje"));

        var ajuste = await servicio.AnularAsync(
            new AnularMovimientoRequest(NegocioId, original.Id, "Canje registrado por error", UsuarioId, Hoy));

        Assert.Equal(TipoMovimientoCredito.Ajuste, ajuste.Tipo);
        Assert.Equal(300m, ajuste.Monto);
        Assert.Equal(UsuarioId, ajuste.UsuarioId);
        Assert.Equal("Canje registrado por error", ajuste.Motivo);

        // El original sigue exactamente igual — nunca hay UPDATE (I1).
        Assert.Equal(2, repositorio.Movimientos.Count);
        Assert.Contains(repositorio.Movimientos, m => m.Id == original.Id && m.Monto == -300m);
    }
}

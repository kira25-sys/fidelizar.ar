using Fidelizar.Application.Services;
using Fidelizar.Application.Tests.Fakes;
using Fidelizar.Domain.Entities;
using Fidelizar.Domain.Exceptions;

namespace Fidelizar.Application.Tests.Services;

/// <summary>S7 Historial de movimientos — Encargada/Dueño only.</summary>
public class HistorialMovimientosServiceTests
{
    private const int NegocioId = 1;
    private const int MiembroId = 42;
    private static readonly DateOnly Hoy = new(2026, 8, 19);

    private static HistorialMovimientosService CrearServicio(
        out FakeMiembroRepository miembroRepositorio,
        out FakeMovimientoRepository movimientoRepositorio,
        out FakeUsuarioRepository usuarioRepositorio)
    {
        miembroRepositorio = new FakeMiembroRepository();
        movimientoRepositorio = new FakeMovimientoRepository();
        usuarioRepositorio = new FakeUsuarioRepository();
        return new HistorialMovimientosService(miembroRepositorio, movimientoRepositorio, usuarioRepositorio);
    }

    [Fact]
    public async Task Miembro_inexistente_lanza_EntityNotFoundException()
    {
        var servicio = CrearServicio(out _, out _, out _);

        await Assert.ThrowsAsync<EntityNotFoundException>(() => servicio.ObtenerAsync(NegocioId, MiembroId));
    }

    [Fact]
    public async Task Resuelve_el_nombre_del_usuario_que_registro_cada_movimiento()
    {
        var servicio = CrearServicio(out var miembroRepositorio, out var movimientoRepositorio, out var usuarioRepositorio);
        miembroRepositorio.SembrarNuevo(NegocioId, MiembroId);
        var cajera = Usuario.Crear(NegocioId, "Ana Cajera", "ana@x.com", "hash", RolUsuario.Cajero, DateTime.UtcNow, sucursalId: 1);
        usuarioRepositorio = new FakeUsuarioRepository(cajera);
        var servicioConUsuario = new HistorialMovimientosService(miembroRepositorio, movimientoRepositorio, usuarioRepositorio);

        var movimiento = await movimientoRepositorio.AppendAsync(MovimientoCredito.Crear(
            NegocioId, MiembroId, Hoy, DateTime.UtcNow, TipoMovimientoCredito.Canje, -300m, Hoy,
            usuarioId: 1, motivo: "Canje de prueba"));

        var historial = await servicioConUsuario.ObtenerAsync(NegocioId, MiembroId);

        var item = Assert.Single(historial);
        Assert.Equal(movimiento.Id, item.Id);
        Assert.Equal("Ana Cajera", item.UsuarioNombre);
    }

    [Fact]
    public async Task Movimiento_sin_usuario_no_lanza_y_deja_el_nombre_nulo()
    {
        var servicio = CrearServicio(out var miembroRepositorio, out var movimientoRepositorio, out _);
        miembroRepositorio.SembrarNuevo(NegocioId, MiembroId);
        await movimientoRepositorio.AppendAsync(MovimientoCredito.Crear(
            NegocioId, MiembroId, Hoy, DateTime.UtcNow, TipoMovimientoCredito.SaldoInicial, 1_000m, Hoy));

        var historial = await servicio.ObtenerAsync(NegocioId, MiembroId);

        var item = Assert.Single(historial);
        Assert.Null(item.UsuarioNombre);
    }
}

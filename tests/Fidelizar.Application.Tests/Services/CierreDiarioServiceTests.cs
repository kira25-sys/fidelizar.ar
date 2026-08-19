using Fidelizar.Application.Services;
using Fidelizar.Application.Tests.Fakes;
using Fidelizar.Domain.Entities;

namespace Fidelizar.Application.Tests.Services;

/// <summary>S9 Cierre diario de canjes — una sucursal, un día: socio, monto, cajero, motivo.</summary>
public class CierreDiarioServiceTests
{
    private const int NegocioId = 1;
    private const int SucursalId = 5;
    private static readonly DateOnly Fecha = new(2026, 8, 19);

    private static CierreDiarioService CrearServicio(
        out FakeMovimientoRepository movimientoRepositorio,
        out FakeUsuarioRepository usuarioRepositorio,
        out FakeMiembroRepository miembroRepositorio,
        Usuario cajeraDeLaSucursal)
    {
        movimientoRepositorio = new FakeMovimientoRepository();
        usuarioRepositorio = new FakeUsuarioRepository(cajeraDeLaSucursal);
        miembroRepositorio = new FakeMiembroRepository();
        return new CierreDiarioService(movimientoRepositorio, usuarioRepositorio, miembroRepositorio);
    }

    [Fact]
    public async Task Solo_incluye_canjes_de_cajeros_de_esa_sucursal()
    {
        var cajeraDeLaSucursal = Usuario.Crear(
            NegocioId, "Ana Cajera", "ana@x.com", "hash", RolUsuario.Cajero, DateTime.UtcNow, sucursalId: SucursalId);
        var servicio = CrearServicio(out var movimientoRepositorio, out var usuarioRepositorio, out var miembroRepositorio, cajeraDeLaSucursal);

        var cajeraDeOtraSucursal = Usuario.Crear(
            NegocioId, "Bea Cajera", "bea@x.com", "hash", RolUsuario.Cajero, DateTime.UtcNow, sucursalId: 999);
        await usuarioRepositorio.CrearAsync(cajeraDeOtraSucursal);

        var socio = miembroRepositorio.SembrarNuevo(NegocioId, 1, "Cliente Uno");

        await movimientoRepositorio.AppendAsync(MovimientoCredito.Crear(
            NegocioId, socio.Id, Fecha, DateTime.UtcNow, TipoMovimientoCredito.Canje, -300m, Fecha,
            usuarioId: 1, motivo: "Descuento en la compra"));
        await movimientoRepositorio.AppendAsync(MovimientoCredito.Crear(
            NegocioId, socio.Id, Fecha, DateTime.UtcNow, TipoMovimientoCredito.Canje, -100m, Fecha,
            usuarioId: 2, motivo: "Canje en otra sucursal"));

        var cierre = await servicio.ObtenerAsync(NegocioId, SucursalId, Fecha);

        var item = Assert.Single(cierre.Movimientos);
        Assert.Equal(300m, item.Monto);
        Assert.Equal("Cliente Uno", item.MiembroNombre);
        Assert.Equal("Ana Cajera", item.CajeroNombre);
        Assert.Equal(300m, cierre.TotalCanjeado);
    }

    [Fact]
    public async Task No_incluye_canjes_de_otro_dia_ni_otros_tipos_de_movimiento()
    {
        var cajera = Usuario.Crear(
            NegocioId, "Ana Cajera", "ana@x.com", "hash", RolUsuario.Cajero, DateTime.UtcNow, sucursalId: SucursalId);
        var servicio = CrearServicio(out var movimientoRepositorio, out _, out var miembroRepositorio, cajera);
        var socio = miembroRepositorio.SembrarNuevo(NegocioId, 1, "Cliente Uno");

        await movimientoRepositorio.AppendAsync(MovimientoCredito.Crear(
            NegocioId, socio.Id, Fecha.AddDays(-1), DateTime.UtcNow, TipoMovimientoCredito.Canje, -300m, Fecha,
            usuarioId: 1, motivo: "Canje de ayer"));
        await movimientoRepositorio.AppendAsync(MovimientoCredito.Crear(
            NegocioId, socio.Id, Fecha, DateTime.UtcNow, TipoMovimientoCredito.Acumulacion, 50m, Fecha, configuracionId: 1));

        var cierre = await servicio.ObtenerAsync(NegocioId, SucursalId, Fecha);

        Assert.Empty(cierre.Movimientos);
        Assert.Equal(0m, cierre.TotalCanjeado);
    }
}

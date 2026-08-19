using Fidelizar.Application.Services;
using Fidelizar.Application.Tests.Fakes;
using Fidelizar.Domain.Exceptions;

namespace Fidelizar.Application.Tests.Services;

/// <summary>S10 Sucursales (Dueño only).</summary>
public class SucursalServiceTests
{
    private const int NegocioId = 1;

    private static SucursalService CrearServicio(out FakeSucursalRepository repositorio)
    {
        repositorio = new FakeSucursalRepository();
        return new SucursalService(repositorio);
    }

    [Fact]
    public async Task Crear_devuelve_la_sucursal_activa()
    {
        var servicio = CrearServicio(out _);

        var creada = await servicio.CrearAsync(NegocioId, "Sucursal Centro", "COD-1");

        Assert.Equal("Sucursal Centro", creada.Nombre);
        Assert.Equal("COD-1", creada.CodigoExterno);
        Assert.True(creada.Activa);
    }

    [Fact]
    public async Task Nombre_vacio_se_rechaza()
    {
        var servicio = CrearServicio(out _);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => servicio.CrearAsync(NegocioId, "", null));
        Assert.Equal("NOMBRE_REQUERIDO", ex.ErrorCode);
    }

    [Fact]
    public async Task Listar_filtra_por_NegocioId()
    {
        var servicio = CrearServicio(out var repositorio);
        repositorio.Sembrar(NegocioId, "Sucursal Centro");
        repositorio.Sembrar(negocioId: 2, "Sucursal De Otro Negocio");

        var sucursales = await servicio.ListarAsync(NegocioId);

        var sucursal = Assert.Single(sucursales);
        Assert.Equal("Sucursal Centro", sucursal.Nombre);
    }
}

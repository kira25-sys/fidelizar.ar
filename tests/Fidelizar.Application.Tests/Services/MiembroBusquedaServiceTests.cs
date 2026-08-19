using Fidelizar.Application.Services;
using Fidelizar.Application.Tests.Fakes;
using Fidelizar.Domain.Entities;
using Fidelizar.Domain.Exceptions;

namespace Fidelizar.Application.Tests.Services;

/// <summary>S2 Buscar socio (FUNCTIONAL-SPEC §4) — "buscar, nunca listar".</summary>
public class MiembroBusquedaServiceTests
{
    private const int NegocioId = 1;

    private static MiembroBusquedaService CrearServicio(
        out FakeMiembroRepository miembroRepositorio, out FakeMovimientoRepository movimientoRepositorio)
    {
        miembroRepositorio = new FakeMiembroRepository();
        movimientoRepositorio = new FakeMovimientoRepository();
        return new MiembroBusquedaService(miembroRepositorio, movimientoRepositorio);
    }

    [Theory]
    [InlineData("")]
    [InlineData("an")]
    [InlineData("  a  ")]
    public async Task Query_de_menos_de_3_caracteres_normalizados_se_rechaza(string query)
    {
        var servicio = CrearServicio(out _, out _);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => servicio.BuscarAsync(NegocioId, query));
        Assert.Equal("BUSQUEDA_MUY_CORTA", ex.ErrorCode);
    }

    [Fact]
    public async Task Encuentra_por_cualquier_palabra_del_nombre_normalizado()
    {
        var servicio = CrearServicio(out var miembroRepositorio, out var movimientoRepositorio);
        var miembro = miembroRepositorio.SembrarNuevo(NegocioId, 1, "Ana María Gómez");
        await movimientoRepositorio.AppendAsync(MovimientoCredito.Crear(
            NegocioId, miembro.Id, new DateOnly(2026, 1, 1), DateTime.UtcNow,
            TipoMovimientoCredito.SaldoInicial, 12_400m, new DateOnly(2026, 1, 1)));

        var resultados = await servicio.BuscarAsync(NegocioId, "gomez ana");

        var resultado = Assert.Single(resultados);
        Assert.Equal("Ana María Gómez", resultado.Nombre);
        Assert.Equal(12_400m, resultado.Saldo);
    }

    [Fact]
    public async Task No_encuentra_socios_de_otro_negocio()
    {
        var servicio = CrearServicio(out var miembroRepositorio, out _);
        miembroRepositorio.SembrarNuevo(negocioId: 2, 1, "Ana Gómez");

        var resultados = await servicio.BuscarAsync(NegocioId, "gomez");

        Assert.Empty(resultados);
    }

    [Fact]
    public async Task Query_que_no_matchea_ninguna_palabra_no_devuelve_resultados()
    {
        var servicio = CrearServicio(out var miembroRepositorio, out _);
        miembroRepositorio.SembrarNuevo(NegocioId, 1, "Ana Gómez");

        var resultados = await servicio.BuscarAsync(NegocioId, "perez");

        Assert.Empty(resultados);
    }
}

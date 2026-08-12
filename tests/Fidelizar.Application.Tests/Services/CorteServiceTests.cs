using Fidelizar.Application.Services;
using Fidelizar.Application.Tests.Fakes;
using Fidelizar.Domain.Exceptions;

namespace Fidelizar.Application.Tests.Services;

/// <summary>F0-07: accrual must fail loudly when no cutoff has been declared — never invent one.</summary>
public class CorteServiceTests
{
    private const int NegocioId = 1;

    [Fact]
    public async Task Sin_corte_declarado_falla_ruidosamente()
    {
        var servicio = new CorteService(new FakeCorteRepository());

        var ex = await Assert.ThrowsAsync<ConflictException>(
            () => servicio.ObtenerCorteVigenteAsync(NegocioId));

        Assert.Equal("CORTE_NO_DECLARADO", ex.ErrorCode);
    }

    [Fact]
    public async Task Corte_declarado_se_puede_obtener_despues()
    {
        var servicio = new CorteService(new FakeCorteRepository());
        var fecha = new DateOnly(2026, 8, 1);

        await servicio.DeclararCorteAsync(NegocioId, fecha, declaradoPorUsuarioId: 3);
        var corte = await servicio.ObtenerCorteVigenteAsync(NegocioId);

        Assert.Equal(fecha, corte.Fecha);
        Assert.Equal(NegocioId, corte.NegocioId);
        Assert.Equal(3, corte.DeclaradoPorUsuarioId);
    }

    [Fact]
    public async Task Corte_de_otro_negocio_no_satisface_a_este()
    {
        var servicio = new CorteService(new FakeCorteRepository());
        await servicio.DeclararCorteAsync(negocioId: 2, new DateOnly(2026, 8, 1), declaradoPorUsuarioId: 3);

        // I8: el corte es por negocio — que otro lo tenga declarado no debe ocultar la falta acá.
        await Assert.ThrowsAsync<ConflictException>(() => servicio.ObtenerCorteVigenteAsync(NegocioId));
    }
}

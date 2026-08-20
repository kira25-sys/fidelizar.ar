using Fidelizar.Application.Services;
using Fidelizar.Application.Tests.Fakes;
using Fidelizar.Domain.Consentimientos;
using Fidelizar.Domain.Entities;
using Fidelizar.Domain.Exceptions;

namespace Fidelizar.Application.Tests.Services;

/// <summary>
/// S5's consent wording (FUNCTIONAL-SPEC §7, README decision #3 resolved 2026-08-19). Invented
/// business data throughout (CLAUDE.md).
/// </summary>
public class ConsentimientoTextoServiceTests
{
    private const int NegocioId = 7;

    private static readonly Negocio NegocioDePrueba = new()
    {
        Id = NegocioId,
        Nombre = "Negocio de Prueba SRL",
        Cuit = "30-12345678-9",
        Domicilio = "Av. Siempre Viva 742",
        CreadoEn = DateTime.UtcNow,
    };

    [Fact]
    public async Task ObtenerAsync_DatosPersonales_devuelve_el_texto_resuelto_para_este_negocio()
    {
        var servicio = new ConsentimientoTextoService(new FakeNegocioRepository(NegocioDePrueba));

        var resultado = await servicio.ObtenerAsync(NegocioId, TipoConsentimiento.DatosPersonales);

        Assert.Equal(TipoConsentimiento.DatosPersonales, resultado.Tipo);
        Assert.Equal(TextosConsentimiento.DatosPersonalesVersion, resultado.VersionTexto);
        Assert.Contains("Negocio de Prueba SRL", resultado.Texto);
    }

    [Fact]
    public async Task ObtenerAsync_DatosSensibles_devuelve_el_texto_resuelto_para_este_negocio()
    {
        var servicio = new ConsentimientoTextoService(new FakeNegocioRepository(NegocioDePrueba));

        var resultado = await servicio.ObtenerAsync(NegocioId, TipoConsentimiento.DatosSensibles);

        Assert.Equal(TipoConsentimiento.DatosSensibles, resultado.Tipo);
        Assert.Equal(TextosConsentimiento.DatosSensiblesVersion, resultado.VersionTexto);
        Assert.Contains("Negocio de Prueba SRL", resultado.Texto);
    }

    [Fact]
    public async Task ObtenerAsync_Comunicaciones_no_tiene_texto_aprobado_todavia()
    {
        var servicio = new ConsentimientoTextoService(new FakeNegocioRepository(NegocioDePrueba));

        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => servicio.ObtenerAsync(NegocioId, TipoConsentimiento.Comunicaciones));

        Assert.Equal("TIPO_CONSENTIMIENTO_SIN_TEXTO", ex.ErrorCode);
    }

    /// <summary>I8: un token de otro negocio no recibe la razón social, el CUIT ni el domicilio
    /// de este.</summary>
    [Fact]
    public async Task ObtenerAsync_con_el_NegocioId_de_otro_negocio_se_rechaza()
    {
        var servicio = new ConsentimientoTextoService(new FakeNegocioRepository(NegocioDePrueba));

        var ex = await Assert.ThrowsAsync<ConflictException>(
            () => servicio.ObtenerAsync(NegocioId + 1, TipoConsentimiento.DatosPersonales));

        Assert.Equal("NEGOCIO_AJENO", ex.ErrorCode);
        Assert.DoesNotContain("30-12345678-9", ex.Message);
    }
}

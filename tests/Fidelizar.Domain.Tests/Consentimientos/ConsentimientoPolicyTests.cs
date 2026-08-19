using Fidelizar.Domain.Consentimientos;
using Fidelizar.Domain.Entities;
using Fidelizar.Domain.Exceptions;

namespace Fidelizar.Domain.Tests.Consentimientos;

/// <summary>
/// I10 — sensitive fields cannot be written without a recorded, granted consent of the matching
/// type (DATA-MODEL §3, Ley 25.326). Pure, no database (ARCHITECTURE §11): every scenario is
/// built with an in-memory <see cref="Consentimiento"/> or with none at all.
/// </summary>
public class ConsentimientoPolicyTests
{
    private const int NegocioId = 1;
    private const int MiembroId = 42;
    private static readonly DateTime OcurridoEn = new(2026, 8, 19, 12, 0, 0, DateTimeKind.Utc);

    private static Consentimiento Otorgar(TipoConsentimiento tipo) =>
        Consentimiento.Registrar(
            NegocioId, MiembroId, tipo, otorgado: true, "v1", CanalConsentimiento.Mostrador, OcurridoEn);

    private static Consentimiento Revocar(TipoConsentimiento tipo) =>
        Consentimiento.Registrar(
            NegocioId, MiembroId, tipo, otorgado: false, "v1", CanalConsentimiento.Mostrador, OcurridoEn);

    /// <summary>I10 — negative case 1: sin ningún consentimiento registrado.</summary>
    [Fact]
    public void Sin_consentimiento_no_permite_escribir()
    {
        Assert.False(ConsentimientoPolicy.PermiteEscritura(null, TipoConsentimiento.DatosSensibles));

        var ex = Assert.Throws<ValidationException>(
            () => ConsentimientoPolicy.RequerirVigente(null, TipoConsentimiento.DatosSensibles));
        Assert.Equal("CONSENTIMIENTO_REQUERIDO", ex.ErrorCode);
    }

    /// <summary>I10 — negative case 2: el consentimiento del tipo correcto fue revocado.</summary>
    [Fact]
    public void Consentimiento_revocado_no_permite_escribir()
    {
        var vigente = Revocar(TipoConsentimiento.DatosSensibles);

        Assert.False(ConsentimientoPolicy.PermiteEscritura(vigente, TipoConsentimiento.DatosSensibles));

        var ex = Assert.Throws<ValidationException>(
            () => ConsentimientoPolicy.RequerirVigente(vigente, TipoConsentimiento.DatosSensibles));
        Assert.Equal("CONSENTIMIENTO_REQUERIDO", ex.ErrorCode);
    }

    /// <summary>I10 — negative case 3: hay consentimiento otorgado, pero de otro tipo (por
    /// ejemplo, datos personales, no datos sensibles).</summary>
    [Fact]
    public void Consentimiento_de_otro_tipo_no_permite_escribir()
    {
        var vigente = Otorgar(TipoConsentimiento.DatosPersonales);

        Assert.False(ConsentimientoPolicy.PermiteEscritura(vigente, TipoConsentimiento.DatosSensibles));

        var ex = Assert.Throws<ValidationException>(
            () => ConsentimientoPolicy.RequerirVigente(vigente, TipoConsentimiento.DatosSensibles));
        Assert.Equal("CONSENTIMIENTO_REQUERIDO", ex.ErrorCode);
    }

    /// <summary>Caso positivo: consentimiento vigente y otorgado, del tipo correcto.</summary>
    [Fact]
    public void Consentimiento_otorgado_del_tipo_correcto_permite_escribir()
    {
        var vigente = Otorgar(TipoConsentimiento.DatosSensibles);

        Assert.True(ConsentimientoPolicy.PermiteEscritura(vigente, TipoConsentimiento.DatosSensibles));

        // No lanza.
        ConsentimientoPolicy.RequerirVigente(vigente, TipoConsentimiento.DatosSensibles);
    }
}

using Fidelizar.Domain.Consentimientos;
using Fidelizar.Domain.Entities;

namespace Fidelizar.Domain.Tests.Consentimientos;

/// <summary>
/// README open decision #3 (resolved 2026-08-19): both consent texts. The wording is a fixed
/// template; the business's own name/CUIT/address are resolved from <see cref="Negocio"/> at
/// render time, never a literal (CLAUDE.md). Invented business data throughout.
/// </summary>
public class TextosConsentimientoTests
{
    private static Negocio Negocio(string? cuit = "30-12345678-9", string? domicilio = "Av. Siempre Viva 742") =>
        new()
        {
            Nombre = "Negocio de Prueba SRL",
            Cuit = cuit,
            Domicilio = domicilio,
            CreadoEn = DateTime.UtcNow,
        };

    [Fact]
    public void DatosPersonalesPara_reemplaza_razon_social_cuit_y_domicilio()
    {
        var (version, texto) = TextosConsentimiento.DatosPersonalesPara(Negocio());

        Assert.Equal(TextosConsentimiento.DatosPersonalesVersion, version);
        Assert.Contains("Negocio de Prueba SRL", texto);
        Assert.Contains("30-12345678-9", texto);
        Assert.Contains("Av. Siempre Viva 742", texto);
        Assert.DoesNotContain("{RazonSocial}", texto);
        Assert.DoesNotContain("{Cuit}", texto);
        Assert.DoesNotContain("{Domicilio}", texto);
    }

    [Fact]
    public void DatosSensiblesPara_reemplaza_razon_social_y_cuit_pero_no_menciona_domicilio()
    {
        var (version, texto) = TextosConsentimiento.DatosSensiblesPara(Negocio());

        Assert.Equal(TextosConsentimiento.DatosSensiblesVersion, version);
        Assert.Contains("Negocio de Prueba SRL", texto);
        Assert.Contains("30-12345678-9", texto);
        Assert.DoesNotContain("{RazonSocial}", texto);
        Assert.DoesNotContain("{Cuit}", texto);
    }

    [Fact]
    public void DatosPersonalesPara_con_Cuit_y_Domicilio_nulos_no_deja_placeholders_sin_resolver()
    {
        var (_, texto) = TextosConsentimiento.DatosPersonalesPara(Negocio(cuit: null, domicilio: null));

        Assert.DoesNotContain("{Cuit}", texto);
        Assert.DoesNotContain("{Domicilio}", texto);
    }

    /// <summary>La asimetría entre los dos textos es de fondo, no de redacción (decisión del
    /// dueño, 2026-08-19): DatosPersonales dice que sin él no hay alta; DatosSensibles dice
    /// explícitamente que sí se puede ser socio sin darlo y que es revocable en cualquier
    /// momento sin afectar la cuenta.</summary>
    [Fact]
    public void El_texto_de_DatosPersonales_dice_que_el_alta_no_es_posible_sin_el()
    {
        var (_, texto) = TextosConsentimiento.DatosPersonalesPara(Negocio());

        Assert.Contains("no es posible darme el alta", texto);
    }

    [Fact]
    public void El_texto_de_DatosSensibles_dice_que_se_puede_ser_socio_sin_darlo_y_que_es_revocable()
    {
        var (_, texto) = TextosConsentimiento.DatosSensiblesPara(Negocio());

        Assert.Contains("puedo ser socio", texto);
        Assert.Contains("revocar", texto, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sin que ello afecte mi cuenta ni mis puntos", texto);
    }
}

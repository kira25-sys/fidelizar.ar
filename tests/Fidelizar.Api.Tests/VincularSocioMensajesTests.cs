using System.Net;
using Fidelizar.Client.Api;
using Fidelizar.Client.Components;
using Fidelizar.Client.Formatting;

namespace Fidelizar.Api.Tests;

/// <summary>
/// F1-14's counter-facing copy. The four rejections in REST-CONTRACT-F1 §"F1-14 Socios sin
/// vincular" have to reach the Encargada as Spanish she can act on, not as a status code — the
/// duplicate above all, because it means the code she typed belongs to somebody else. These live
/// here because Fidelizar.Api.Tests already references Client (DependencyDirectionTests).
/// </summary>
public class VincularSocioMensajesTests
{
    private static ApiProblem Problema(HttpStatusCode status, string? errorCode = null) =>
        new("mensaje del servidor", status, errorCode);

    [Fact]
    public void Duplicado_names_the_code_and_says_it_belongs_to_another_socio()
    {
        var rechazo = VincularSocioMensajes.Rechazo(
            Problema(HttpStatusCode.Conflict, "CLIENTE_EXTERNO_ID_DUPLICADO"), "Ana Gómez", "4821");

        Assert.Contains("4821", rechazo.Mensaje);
        Assert.Contains("otro socio", rechazo.Mensaje);
        Assert.Contains("Ana Gómez", rechazo.Mensaje);
        Assert.Equal("danger", rechazo.Tono);

        // Nothing to reload: the list is right, the code is wrong.
        Assert.False(rechazo.ListaDesactualizada);
    }

    [Fact]
    public void Miembro_ya_vinculado_offers_a_reload_instead_of_a_retry()
    {
        var rechazo = VincularSocioMensajes.Rechazo(
            Problema(HttpStatusCode.Conflict, "MIEMBRO_YA_VINCULADO"), "Ana Gómez", "4821");

        Assert.Contains("Ana Gómez", rechazo.Mensaje);
        Assert.True(rechazo.ListaDesactualizada);
        Assert.Equal("warning", rechazo.Tono);
    }

    [Fact]
    public void Id_requerido_asks_for_the_code_without_mentioning_an_error_code()
    {
        var rechazo = VincularSocioMensajes.Rechazo(
            Problema(HttpStatusCode.BadRequest, "CLIENTE_EXTERNO_ID_REQUERIDO"), "Ana Gómez", "");

        Assert.Equal("Escribí el código de cliente del POS.", rechazo.Mensaje);
        Assert.Equal("danger", rechazo.Tono);
    }

    /// <summary>I8: the server answers the same 404 for "no existe" and "es de otro negocio", so
    /// the message must not distinguish them either.</summary>
    [Fact]
    public void NotFound_says_the_list_may_be_stale_and_never_that_the_socio_is_elsewhere()
    {
        var rechazo = VincularSocioMensajes.Rechazo(Problema(HttpStatusCode.NotFound), "Ana Gómez", "4821");

        Assert.True(rechazo.ListaDesactualizada);
        Assert.DoesNotContain("negocio", rechazo.Mensaje);
        Assert.DoesNotContain("otro", rechazo.Mensaje);
    }

    [Fact]
    public void Unauthorized_offers_the_login_screen_and_promises_the_code_is_kept()
    {
        var rechazo = VincularSocioMensajes.Rechazo(Problema(HttpStatusCode.Unauthorized), "Ana Gómez", "4821");

        Assert.True(rechazo.OfreceIngresar);
        Assert.Contains("sesión", rechazo.Mensaje);
    }

    [Fact]
    public void Offline_keeps_the_typed_code_and_invites_a_retry()
    {
        var offline = new ApiProblem(
            "No pudimos conectar con el servidor.", StatusCode: null, ApiErrorCodes.Offline, IsOffline: true);

        var rechazo = VincularSocioMensajes.Rechazo(offline, "Ana Gómez", "4821");

        Assert.Contains("probá de nuevo", rechazo.Mensaje);
        Assert.False(rechazo.OfreceIngresar);
        Assert.False(rechazo.ListaDesactualizada);
    }

    /// <summary>No rejection ever reaches the reader as a bare status code or an English word.</summary>
    [Theory]
    [InlineData(HttpStatusCode.Conflict, "CLIENTE_EXTERNO_ID_DUPLICADO")]
    [InlineData(HttpStatusCode.Conflict, "MIEMBRO_YA_VINCULADO")]
    [InlineData(HttpStatusCode.BadRequest, "CLIENTE_EXTERNO_ID_REQUERIDO")]
    [InlineData(HttpStatusCode.NotFound, null)]
    [InlineData(HttpStatusCode.Forbidden, null)]
    [InlineData(HttpStatusCode.InternalServerError, null)]
    public void Every_rejection_is_plain_spanish(HttpStatusCode status, string? errorCode)
    {
        var rechazo = VincularSocioMensajes.Rechazo(Problema(status, errorCode), "Ana Gómez", "4821");

        Assert.NotEmpty(rechazo.Mensaje);
        Assert.DoesNotContain("409", rechazo.Mensaje);
        Assert.DoesNotContain("404", rechazo.Mensaje);
        Assert.DoesNotContain("_", rechazo.Mensaje);
        Assert.Contains(rechazo.Tono, new[] { "danger", "warning" });
    }
}

/// <summary>F1-14 — the wait the list puts at the top of the screen.</summary>
public class EsperaFormatterTests
{
    [Theory]
    [InlineData(0, "hoy")]
    [InlineData(1, "hace 1 día")]
    [InlineData(2, "hace 2 días")]
    [InlineData(214, "hace 214 días")]
    public void Espera_reads_in_days(int dias, string esperado)
    {
        var hoy = new DateOnly(2026, 8, 21);

        Assert.Equal(esperado, EsperaFormatter.Espera(hoy.AddDays(-dias), hoy));
    }

    /// <summary>A tablet clock running behind the server must not read "hace -1 días".</summary>
    [Fact]
    public void A_future_alta_reads_as_today()
    {
        var hoy = new DateOnly(2026, 8, 21);

        Assert.Equal("hoy", EsperaFormatter.Espera(hoy.AddDays(3), hoy));
    }
}

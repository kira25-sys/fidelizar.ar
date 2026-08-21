using System.Net;
using Fidelizar.Api.Security;
using Fidelizar.Api.Tests.Security.Fakes;

namespace Fidelizar.Api.Tests.Security;

/// <summary>
/// The negative F1-15 exists for, with its own name (ROADMAP): "a cashier must not reach a phone
/// number by calling the endpoint directly, with no screen involved". S6 is the only endpoint that
/// ever returns <c>Telefono</c>/<c>Dni</c> (FUNCTIONAL-SPEC §8, REST-CONTRACT-F1) and
/// <c>FichaMostradorResponse</c> deliberately does not carry them.
///
/// Real HTTP, request built by hand: hiding the link in the client proves nothing about what the
/// server answers a `curl`.
/// </summary>
public class FichaCompletaPipelineTests(MatrizDePermisosApiFactory factory)
    : IClassFixture<MatrizDePermisosApiFactory>
{
    private static readonly EndpointDeLaMatriz FichaCompleta =
        MatrizDePermisos.Buscar("GET", "/api/miembros/42/completo");

    private static readonly EndpointDeLaMatriz FichaMostrador =
        MatrizDePermisos.Buscar("GET", "/api/miembros/42/ficha-mostrador");

    [Fact]
    public async Task Un_Cajero_que_llama_la_ficha_completa_directo_no_obtiene_telefono_ni_dni()
    {
        using var respuesta = await PedidoDeLaMatriz.EnviarAsync(factory, FichaCompleta, Roles.Cajero);
        var cuerpo = await respuesta.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
        Assert.DoesNotContain(DatosFicticiosDeLaMatriz.TelefonoFicticio, cuerpo, StringComparison.Ordinal);
        Assert.DoesNotContain(DatosFicticiosDeLaMatriz.DniFicticio, cuerpo, StringComparison.Ordinal);
    }

    /// <summary>
    /// Without this the test above could be green because nothing ever returns a phone. Here the
    /// same route, same host, same fixture — and an Encargada does get both fields, which is what
    /// makes the Cajero's 403 a real refusal instead of an empty endpoint.
    /// </summary>
    [Fact]
    public async Task Una_Encargada_si_obtiene_telefono_y_dni_en_la_ficha_completa()
    {
        using var respuesta = await PedidoDeLaMatriz.EnviarAsync(factory, FichaCompleta, Roles.Encargada);
        var cuerpo = await respuesta.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Contains(DatosFicticiosDeLaMatriz.TelefonoFicticio, cuerpo, StringComparison.Ordinal);
        Assert.Contains(DatosFicticiosDeLaMatriz.DniFicticio, cuerpo, StringComparison.Ordinal);
    }

    /// <summary>The other half of the privacy split: the view a Cajero <em>is</em> allowed to open
    /// answers 200 and still carries neither field, because the DTO has no room for them.</summary>
    [Fact]
    public async Task La_ficha_de_mostrador_de_un_Cajero_responde_200_y_no_trae_telefono_ni_dni()
    {
        using var respuesta = await PedidoDeLaMatriz.EnviarAsync(factory, FichaMostrador, Roles.Cajero);
        var cuerpo = await respuesta.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.DoesNotContain(DatosFicticiosDeLaMatriz.TelefonoFicticio, cuerpo, StringComparison.Ordinal);
        Assert.DoesNotContain(DatosFicticiosDeLaMatriz.DniFicticio, cuerpo, StringComparison.Ordinal);
        Assert.DoesNotContain("telefono", cuerpo, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("dni", cuerpo, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The other axis of ARCHITECTURE §8: a role high enough for the endpoint is still refused
    /// another branch's report. The Encargada of this fixture is stationed at
    /// <see cref="MatrizDePermisos.SucursalDelPersonal"/>; Dueño carries no branch claim and may
    /// ask for any.
    /// </summary>
    [Fact]
    public async Task Una_Encargada_no_llega_al_cierre_diario_de_otra_sucursal()
    {
        var ajeno = new EndpointDeLaMatriz(
            "GET",
            $"/api/sucursales/{MatrizDePermisos.SucursalAjena}/cierre-diario?fecha=2026-08-21",
            "api/sucursales/{sucursalId:int}/cierre-diario",
            PoliticaEsperada.EncargadaOrAbove,
            RequiereAntiforgery: false);

        using var respuestaEncargada = await PedidoDeLaMatriz.EnviarAsync(factory, ajeno, Roles.Encargada);
        using var respuestaDueno = await PedidoDeLaMatriz.EnviarAsync(factory, ajeno, Roles.Dueno);

        Assert.Equal(HttpStatusCode.Forbidden, respuestaEncargada.StatusCode);
        Assert.Equal(HttpStatusCode.OK, respuestaDueno.StatusCode);
    }
}

using System.Net;
using System.Reflection;
using Fidelizar.Api.Configurations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace Fidelizar.Api.Tests.Security;

/// <summary>
/// F1-15 (ROADMAP): "Permission matrix tests, run against the API: every role against every
/// endpoint, including the negatives — a cashier must not reach a phone number by calling the
/// endpoint directly, with no screen involved."
///
/// Every case here goes through real HTTP against <c>Fidelizar.Api</c>'s own pipeline. A test that
/// called the controller method would skip <c>[Authorize]</c> and prove nothing, the same way a
/// rate limiter registered in DI but absent from the pipeline protects nothing
/// (ARCHITECTURE §8, <c>RateLimiterPipelineTests</c>).
///
/// The endpoint table is <c>docs/REST-CONTRACT-F1.md</c>, transcribed in
/// <see cref="MatrizDePermisos"/> — the document is the source, so a route the code protects
/// differently than the contract says fails here.
/// </summary>
public class MatrizDePermisosPipelineTests(MatrizDePermisosApiFactory factory)
    : IClassFixture<MatrizDePermisosApiFactory>
{
    public static IEnumerable<object[]> CasosAutorizados() =>
        from endpoint in MatrizDePermisos.Endpoints
        from rol in MatrizDePermisos.RolesQuePasan(endpoint.Politica)
        select new object[] { endpoint.Metodo, endpoint.Ruta, rol };

    public static IEnumerable<object[]> CasosDenegados() =>
        from endpoint in MatrizDePermisos.Endpoints
        from rol in MatrizDePermisos.RolesQueReciben403(endpoint.Politica)
        select new object[] { endpoint.Metodo, endpoint.Ruta, rol };

    public static IEnumerable<object[]> EndpointsConSesionObligatoria() =>
        MatrizDePermisos.Endpoints
            .Where(e => e.Politica != PoliticaEsperada.Anonima)
            .Select(e => new object[] { e.Metodo, e.Ruta });

    public static IEnumerable<object[]> EndpointsAnonimos() =>
        MatrizDePermisos.Endpoints
            .Where(e => e.Politica == PoliticaEsperada.Anonima)
            .Select(e => new object[] { e.Metodo, e.Ruta });

    public static IEnumerable<object[]> EndpointsQueCambianEstado() =>
        MatrizDePermisos.Endpoints
            .Where(e => e.RequiereAntiforgery)
            .Select(e => new object[] { e.Metodo, e.Ruta });

    /// <summary>The positive half. Without it the negatives below could all be green because the
    /// route does not exist.</summary>
    [Theory]
    [MemberData(nameof(CasosAutorizados))]
    public async Task El_rol_autorizado_atraviesa_la_autorizacion(string metodo, string ruta, string rol)
    {
        var endpoint = MatrizDePermisos.Buscar(metodo, ruta);

        using var respuesta = await PedidoDeLaMatriz.EnviarAsync(factory, endpoint, rol);

        Assert.True(
            respuesta.IsSuccessStatusCode,
            $"{rol} tendría que atravesar {endpoint} ({endpoint.Politica}) y recibió " +
            await PedidoDeLaMatriz.DescribirAsync(respuesta));
    }

    /// <summary>
    /// The half the ROADMAP cares about: 403, not 200 with data, not 500, not a redirect. A hidden
    /// button is not a test result — this request is built by hand, with no client involved.
    /// </summary>
    [Theory]
    [MemberData(nameof(CasosDenegados))]
    public async Task Un_rol_sin_permiso_recibe_403(string metodo, string ruta, string rol)
    {
        var endpoint = MatrizDePermisos.Buscar(metodo, ruta);

        using var respuesta = await PedidoDeLaMatriz.EnviarAsync(factory, endpoint, rol);

        Assert.True(
            respuesta.StatusCode == HttpStatusCode.Forbidden,
            $"{rol} tendría que recibir 403 en {endpoint} ({endpoint.Politica}) y recibió " +
            await PedidoDeLaMatriz.DescribirAsync(respuesta));
    }

    [Theory]
    [MemberData(nameof(EndpointsConSesionObligatoria))]
    public async Task Un_pedido_sin_sesion_recibe_401(string metodo, string ruta)
    {
        var endpoint = MatrizDePermisos.Buscar(metodo, ruta);

        using var respuesta = await PedidoDeLaMatriz.EnviarAsync(factory, endpoint, rol: null);

        Assert.True(
            respuesta.StatusCode == HttpStatusCode.Unauthorized,
            $"Un anónimo tendría que recibir 401 en {endpoint} y recibió " +
            await PedidoDeLaMatriz.DescribirAsync(respuesta));
    }

    /// <summary>S1's two anonymous endpoints exist so a cashier can log in at all — they answer
    /// with no session on purpose (`[AllowAnonymous]`), and that is the whole exception.</summary>
    [Theory]
    [MemberData(nameof(EndpointsAnonimos))]
    public async Task Los_dos_endpoints_de_S1_responden_sin_sesion(string metodo, string ruta)
    {
        var endpoint = MatrizDePermisos.Buscar(metodo, ruta);

        using var respuesta = await PedidoDeLaMatriz.EnviarAsync(factory, endpoint, rol: null);

        Assert.True(
            respuesta.IsSuccessStatusCode,
            $"{endpoint} es anónimo por contrato y respondió " +
            await PedidoDeLaMatriz.DescribirAsync(respuesta));
    }

    /// <summary>
    /// ARCHITECTURE §8: every state-changing endpoint additionally requires the antiforgery token.
    /// Sent by the role that <em>is</em> authorised, so nothing but the missing header can explain
    /// the rejection — 400 <c>ANTIFORGERY_TOKEN_INVALIDO</c>, never a 2xx.
    /// </summary>
    [Theory]
    [MemberData(nameof(EndpointsQueCambianEstado))]
    public async Task Un_POST_sin_el_header_X_CSRF_TOKEN_es_rechazado(string metodo, string ruta)
    {
        var endpoint = MatrizDePermisos.Buscar(metodo, ruta);
        var rolAutorizado = MatrizDePermisos.RolesQuePasan(endpoint.Politica)[0];

        using var respuesta = await PedidoDeLaMatriz.EnviarAsync(
            factory, endpoint, rolAutorizado, conAntiforgery: false);
        var cuerpo = await respuesta.Content.ReadAsStringAsync();

        Assert.False(
            respuesta.IsSuccessStatusCode,
            $"{endpoint} aceptó un pedido sin {AntiforgeryConfigurationExtensions.HeaderName}: " +
            await PedidoDeLaMatriz.DescribirAsync(respuesta));
        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Contains("ANTIFORGERY_TOKEN_INVALIDO", cuerpo, StringComparison.Ordinal);
    }

    /// <summary>The matrix is only worth its name if it covers the contract: 20 HTTP endpoints out
    /// of the 18 rows of docs/REST-CONTRACT-F1.md (two rows carry "GET / POST").</summary>
    [Fact]
    public void La_matriz_cubre_los_veinte_endpoints_del_contrato()
    {
        Assert.Equal(20, MatrizDePermisos.Endpoints.Count);
        Assert.Equal(
            MatrizDePermisos.Endpoints.Count,
            MatrizDePermisos.Endpoints.Select(e => e.ClaveDeRuteo).Distinct().Count());
    }

    /// <summary>
    /// An endpoint that exists in the code but not in the contract table is a finding, not a gap
    /// to shrug at — so the matrix is cross-checked by reflection against every action declared in
    /// <c>Fidelizar.Api</c>, the same way <c>EveryEndpointDeclaresAuthorizationTests</c> walks
    /// them. A route shipped tomorrow without a row here fails this test on the spot.
    /// </summary>
    [Fact]
    public void La_matriz_cubre_todos_los_endpoints_del_ensamblado()
    {
        var enElCodigo = RutasDeclaradasEnApi();
        var enLaMatriz = MatrizDePermisos.Endpoints.Select(e => e.ClaveDeRuteo).ToHashSet(StringComparer.Ordinal);

        var sinCubrir = enElCodigo.Except(enLaMatriz).Order().ToList();
        var deMas = enLaMatriz.Except(enElCodigo).Order().ToList();

        Assert.True(
            sinCubrir.Count == 0,
            "Hay endpoints en Fidelizar.Api que la matriz no prueba (y que docs/REST-CONTRACT-F1.md " +
            $"tendría que listar): {string.Join(", ", sinCubrir)}");
        Assert.True(
            deMas.Count == 0,
            $"La matriz nombra rutas que ya no existen en Fidelizar.Api: {string.Join(", ", deMas)}");
    }

    /// <summary>"VERBO plantilla" for every controller action, built from the class-level
    /// <c>[Route]</c> and the action's own <c>[HttpGet]</c>/<c>[HttpPost]</c>.</summary>
    private static HashSet<string> RutasDeclaradasEnApi()
    {
        var rutas = new HashSet<string>(StringComparer.Ordinal);

        var controladores = typeof(Fidelizar.Api.Controllers.AuthController).Assembly.GetTypes()
            .Where(t => t.IsPublic && !t.IsAbstract && typeof(ControllerBase).IsAssignableFrom(t));

        foreach (var controlador in controladores)
        {
            var prefijo = controlador.GetCustomAttributes(inherit: true)
                .OfType<RouteAttribute>()
                .Select(r => r.Template)
                .FirstOrDefault() ?? string.Empty;

            var acciones = controlador.GetMethods(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => !m.IsSpecialName);

            foreach (var accion in acciones)
            {
                foreach (var verbo in accion.GetCustomAttributes(inherit: true).OfType<HttpMethodAttribute>())
                {
                    var sufijo = verbo.Template;
                    var plantilla = string.IsNullOrEmpty(sufijo) ? prefijo : $"{prefijo}/{sufijo}";

                    rutas.Add($"{verbo.HttpMethods.Single()} {plantilla}");
                }
            }
        }

        return rutas;
    }
}

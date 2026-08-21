using System.Net.Http.Json;
using System.Text.Json;
using Fidelizar.Api.Configurations;
using Fidelizar.Api.Security;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Fidelizar.Api.Tests.Security;

/// <summary>
/// Builds a request by hand — cookie header, CSRF header and body — and sends it through the real
/// pipeline. No browser, no client code, no <c>HttpClient</c> cookie jar: F1-15's whole point is
/// the request an attacker types, and a helper that quietly filled anything in would be testing
/// the helper.
/// </summary>
public static class PedidoDeLaMatriz
{
    /// <summary>Cookies are managed by hand so nothing is ever added to a request without this
    /// file saying so.</summary>
    private static readonly WebApplicationFactoryClientOptions SinCookieJar = new() { HandleCookies = false };

    /// <param name="rol">Null means no session at all — the anonymous caller.</param>
    /// <param name="conAntiforgery">False sends a state-changing request with no
    /// <c>X-CSRF-TOKEN</c>, which must be a rejection and not an escape (ARCHITECTURE §8).</param>
    public static async Task<HttpResponseMessage> EnviarAsync(
        MatrizDePermisosApiFactory factory, EndpointDeLaMatriz endpoint, string? rol, bool conAntiforgery = true)
    {
        var client = factory.CreateClient(SinCookieJar);

        var cookies = new List<string>();
        if (rol is not null)
        {
            cookies.Add(factory.CookieDeSesion(rol));
        }

        string? tokenCsrf = null;
        if (endpoint.RequiereAntiforgery && conAntiforgery)
        {
            // The antiforgery token is bound to the caller's identity, so it has to be obtained
            // with the same session cookie the state-changing request will carry.
            var (token, cookieCsrf) = await ObtenerAntiforgeryAsync(client, cookies);
            tokenCsrf = token;
            cookies.Add(cookieCsrf);
        }

        using var pedido = new HttpRequestMessage(new HttpMethod(endpoint.Metodo), endpoint.Ruta);
        AgregarCookies(pedido, cookies);

        if (tokenCsrf is not null)
        {
            pedido.Headers.Add(AntiforgeryConfigurationExtensions.HeaderName, tokenCsrf);
        }

        if (endpoint.Cuerpo is not null)
        {
            pedido.Content = JsonContent.Create(endpoint.Cuerpo, endpoint.Cuerpo.GetType());
        }

        return await client.SendAsync(pedido);
    }

    /// <summary>The status plus the body, so a failing matrix case says what actually came back
    /// instead of only which number it was.</summary>
    public static async Task<string> DescribirAsync(HttpResponseMessage respuesta)
    {
        var cuerpo = await respuesta.Content.ReadAsStringAsync();
        var recortado = cuerpo.Length > 400 ? cuerpo[..400] + "…" : cuerpo;
        return $"{(int)respuesta.StatusCode} {respuesta.StatusCode} — cuerpo: {recortado}";
    }

    private static void AgregarCookies(HttpRequestMessage pedido, IReadOnlyCollection<string> cookies)
    {
        if (cookies.Count > 0)
        {
            pedido.Headers.Add("Cookie", string.Join("; ", cookies));
        }
    }

    private static async Task<(string Token, string Cookie)> ObtenerAntiforgeryAsync(
        HttpClient client, IReadOnlyCollection<string> cookiesDeSesion)
    {
        using var pedido = new HttpRequestMessage(HttpMethod.Get, "/api/auth/csrf-token");
        AgregarCookies(pedido, cookiesDeSesion);

        using var respuesta = await client.SendAsync(pedido);
        respuesta.EnsureSuccessStatusCode();

        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        var token = cuerpo.GetProperty("token").GetString()
            ?? throw new InvalidOperationException("GET /api/auth/csrf-token no devolvió 'token'.");

        // The antiforgery cookie's name is private to Api; it is simply the one Set-Cookie that
        // is not the session cookie.
        var cookie = respuesta.Headers.GetValues("Set-Cookie")
            .Select(c => c.Split(';')[0])
            .First(c => !c.StartsWith($"{AuthCookie.Name}=", StringComparison.Ordinal));

        return (token, cookie);
    }
}

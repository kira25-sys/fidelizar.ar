using Fidelizar.Api.Configurations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Fidelizar.Api.Tests.Security;

/// <summary>
/// ARCHITECTURE §8: "The key is validated at startup: a missing or short key stops the
/// application from starting, rather than throwing on the first login attempt." Exercises
/// <see cref="AuthenticationConfigurationExtensions.AddAppAuthentication"/> directly — no HTTP
/// server, no database — since that call is what <c>Program.cs</c> makes before
/// <c>WebApplication.Build()</c> even runs.
/// </summary>
public class JwtSigningKeyValidationTests
{
    private static IConfiguration ConfigurationCon(string? signingKey)
    {
        var valores = new Dictionary<string, string?>();
        if (signingKey is not null)
        {
            valores["Jwt:SigningKey"] = signingKey;
        }

        return new ConfigurationBuilder().AddInMemoryCollection(valores).Build();
    }

    [Fact]
    public void Sin_clave_configurada_no_arranca()
    {
        var services = new ServiceCollection();

        var ex = Assert.Throws<InvalidOperationException>(
            () => services.AddAppAuthentication(ConfigurationCon(signingKey: null)));

        Assert.Contains("Jwt:SigningKey", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Clave_mas_corta_que_32_bytes_no_arranca()
    {
        var services = new ServiceCollection();

        // 31 caracteres ASCII = 31 bytes en UTF-8, uno menos que el mínimo de HS256.
        var claveCorta = new string('x', 31);

        Assert.Throws<InvalidOperationException>(
            () => services.AddAppAuthentication(ConfigurationCon(claveCorta)));
    }

    [Fact]
    public void Clave_de_exactamente_32_bytes_arranca()
    {
        var services = new ServiceCollection();
        var claveValida = new string('x', 32);

        var exception = Record.Exception(
            () => services.AddAppAuthentication(ConfigurationCon(claveValida)));

        Assert.Null(exception);
    }

    [Fact]
    public void Clave_generada_al_vuelo_en_el_test_arranca()
    {
        var services = new ServiceCollection();
        var claveGenerada = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(48));

        var exception = Record.Exception(
            () => services.AddAppAuthentication(ConfigurationCon(claveGenerada)));

        Assert.Null(exception);
    }
}

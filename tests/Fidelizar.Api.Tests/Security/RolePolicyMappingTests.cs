using Fidelizar.Api.Configurations;
using Fidelizar.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Fidelizar.Api.Tests.Security;

/// <summary>
/// ARCHITECTURE §8 role table: Encargada can do everything a Cajero can, Dueno everything
/// Encargada can — Soporte's "audited technical access" is a separate lane, not part of this
/// ladder. Builds the DI container directly, no HTTP server, no database.
/// </summary>
public class RolePolicyMappingTests
{
    private static AuthorizationOptions BuildOptions()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SigningKey"] = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(48)),
            })
            .Build();

        services.AddAppAuthentication(configuration);

        return services.BuildServiceProvider().GetRequiredService<IOptions<AuthorizationOptions>>().Value;
    }

    private static string[] RolesRequeridosPor(AuthorizationPolicy policy) =>
        policy.Requirements.OfType<RolesAuthorizationRequirement>().SelectMany(r => r.AllowedRoles).ToArray();

    [Fact]
    public void CajeroOrAbove_admite_Cajero_Encargada_y_Dueno_pero_no_Soporte()
    {
        var options = BuildOptions();
        var roles = RolesRequeridosPor(options.GetPolicy(Policies.CajeroOrAbove)!);

        Assert.Contains(Roles.Cajero, roles);
        Assert.Contains(Roles.Encargada, roles);
        Assert.Contains(Roles.Dueno, roles);
        Assert.DoesNotContain(Roles.Soporte, roles);
    }

    [Fact]
    public void EncargadaOrAbove_admite_Encargada_y_Dueno_pero_no_Cajero_ni_Soporte()
    {
        var options = BuildOptions();
        var roles = RolesRequeridosPor(options.GetPolicy(Policies.EncargadaOrAbove)!);

        Assert.Contains(Roles.Encargada, roles);
        Assert.Contains(Roles.Dueno, roles);
        Assert.DoesNotContain(Roles.Cajero, roles);
        Assert.DoesNotContain(Roles.Soporte, roles);
    }

    [Fact]
    public void DuenoOnly_admite_unicamente_Dueno()
    {
        var options = BuildOptions();
        var roles = RolesRequeridosPor(options.GetPolicy(Policies.DuenoOnly)!);

        Assert.Equal([Roles.Dueno], roles);
    }
}

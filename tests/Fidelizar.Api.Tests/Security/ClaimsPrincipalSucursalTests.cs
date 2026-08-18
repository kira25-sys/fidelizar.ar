using System.Security.Claims;
using Fidelizar.Api.Security;

namespace Fidelizar.Api.Tests.Security;

/// <summary>
/// The branch axis of F1-04 (roadmap): a <c>Cajero</c>/<c>Encargada</c> is tied to one
/// <c>SucursalId</c> and operates only on that branch's own resources; <c>Dueno</c>/<c>Soporte</c>
/// carry no branch claim and operate on any branch (DATA-MODEL §2, ARCHITECTURE §8). RN-07 and
/// FUNCTIONAL-SPEC are explicit that this never restricts which member a user may serve — only
/// this helper's callers do, and only for genuinely branch-scoped resources (e.g. S9 cierre
/// diario).
/// </summary>
public class ClaimsPrincipalSucursalTests
{
    private static ClaimsPrincipal PrincipalConSucursal(int? sucursalId)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "1"),
            new(ClaimTypes.Role, "Cajero"),
            new(JwtTokenService.NegocioIdClaim, "7"),
        };

        if (sucursalId is { } valor)
        {
            claims.Add(new Claim(JwtTokenService.SucursalIdClaim, valor.ToString()));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims));
    }

    [Fact]
    public void ObtenerSucursalId_devuelve_la_sucursal_del_claim()
    {
        var principal = PrincipalConSucursal(5);

        Assert.Equal(5, principal.ObtenerSucursalId());
    }

    [Fact]
    public void ObtenerSucursalId_devuelve_null_cuando_no_hay_claim_Dueno_o_Soporte()
    {
        var principal = PrincipalConSucursal(null);

        Assert.Null(principal.ObtenerSucursalId());
    }

    [Fact]
    public void PuedeOperarSucursal_es_verdadero_para_la_propia_sucursal()
    {
        var principal = PrincipalConSucursal(5);

        Assert.True(principal.PuedeOperarSucursal(5));
    }

    [Fact]
    public void PuedeOperarSucursal_es_falso_para_otra_sucursal()
    {
        var principal = PrincipalConSucursal(5);

        Assert.False(principal.PuedeOperarSucursal(6));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(999)]
    public void PuedeOperarSucursal_es_verdadero_para_cualquier_sucursal_sin_claim(int sucursalId)
    {
        var principal = PrincipalConSucursal(null);

        Assert.True(principal.PuedeOperarSucursal(sucursalId));
    }
}

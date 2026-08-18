using Fidelizar.Api.Security;

namespace Fidelizar.Api.Tests.Security;

/// <summary>ARCHITECTURE §8: "Token lifetime is short and renewal is silent."</summary>
public class TokenRenewalPolicyTests
{
    [Fact]
    public void Con_menos_de_la_mitad_de_vida_restante_debe_renovar()
    {
        var ahora = new DateTime(2026, 8, 13, 12, 0, 0, DateTimeKind.Utc);
        var expira = ahora.AddMinutes(6); // 6 de 15 minutos: menos de la mitad.

        Assert.True(TokenRenewalPolicy.DebeRenovar(expira, ahora, accessTokenMinutes: 15));
    }

    [Fact]
    public void Con_mas_de_la_mitad_de_vida_restante_no_debe_renovar()
    {
        var ahora = new DateTime(2026, 8, 13, 12, 0, 0, DateTimeKind.Utc);
        var expira = ahora.AddMinutes(10); // 10 de 15 minutos: más de la mitad.

        Assert.False(TokenRenewalPolicy.DebeRenovar(expira, ahora, accessTokenMinutes: 15));
    }

    [Fact]
    public void Un_token_ya_vencido_debe_renovar()
    {
        var ahora = new DateTime(2026, 8, 13, 12, 0, 0, DateTimeKind.Utc);
        var expira = ahora.AddMinutes(-1);

        Assert.True(TokenRenewalPolicy.DebeRenovar(expira, ahora, accessTokenMinutes: 15));
    }
}

using Fidelizar.Api.Security;
using Microsoft.AspNetCore.Http;

namespace Fidelizar.Api.Tests.Security;

/// <summary>
/// ARCHITECTURE §8: "It travels in an HttpOnly, Secure, SameSite=Strict cookie, not in
/// localStorage and not in an Authorization header set by JavaScript." Pure — no server needed.
/// </summary>
public class AuthCookieTests
{
    [Fact]
    public void Build_pone_las_tres_banderas()
    {
        var options = AuthCookie.Build(DateTime.UtcNow.AddMinutes(15));

        Assert.True(options.HttpOnly);
        Assert.True(options.Secure);
        Assert.Equal(SameSiteMode.Strict, options.SameSite);
    }

    [Fact]
    public void Build_conserva_la_expiracion_recibida()
    {
        var expira = new DateTime(2026, 8, 13, 12, 30, 0, DateTimeKind.Utc);

        var options = AuthCookie.Build(expira);

        Assert.Equal(expira, options.Expires!.Value.UtcDateTime);
    }

    [Fact]
    public void BuildForDeletion_tambien_pone_las_tres_banderas()
    {
        var options = AuthCookie.BuildForDeletion();

        Assert.True(options.HttpOnly);
        Assert.True(options.Secure);
        Assert.Equal(SameSiteMode.Strict, options.SameSite);
    }
}

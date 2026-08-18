using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Fidelizar.Api.Tests;

/// <summary>
/// ARCHITECTURE §8: "Login endpoints are rate-limited... Verify the limiter is actually in the
/// pipeline, not merely registered in DI." Same pattern as
/// <see cref="RateLimiterPipelineTests"/>, applied to <c>POST /api/auth/login</c> specifically —
/// its own named policy, tighter than the global default (F1-03).
///
/// The body sent is empty JSON (<c>{}</c>) with no antiforgery header: the
/// <c>[AntiforgeryTokenRequired]</c> filter rejects it with 400 before the request ever reaches
/// <c>AuthService</c>/the database — this test is only about the HTTP pipeline, the same
/// boundary <c>RateLimiterPipelineTests</c> draws for <c>/health</c>. The exact status an allowed
/// request gets back is incidental; only the presence of 429 once the limit is exceeded, and its
/// absence below the limit, matter here.
/// </summary>
public class AuthLoginRateLimiterPipelineTests
{
    [Fact]
    public async Task Exceeding_the_login_limit_returns_429_from_the_real_pipeline()
    {
        using var factory = new LowLoginLimitApiFactory();
        var client = factory.CreateClient();

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 10 && !statuses.Contains(HttpStatusCode.TooManyRequests); i++)
        {
            var response = await client.PostAsJsonAsync("/api/auth/login", new { });
            statuses.Add(response.StatusCode);
        }

        Assert.Contains(HttpStatusCode.TooManyRequests, statuses);
    }

    [Fact]
    public async Task El_primer_login_dentro_del_limite_no_se_rechaza_por_el_limiter()
    {
        using var factory = new LowLoginLimitApiFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new { });

        Assert.NotEqual(HttpStatusCode.TooManyRequests, response.StatusCode);
    }

    /// <summary>Same "Testing" hosting environment as <see cref="RateLimiterPipelineTests"/>, for
    /// the same reason — CI has no Postgres, and this test is only about the HTTP pipeline.</summary>
    private sealed class LowLoginLimitApiFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("RateLimiting:Login:PermitLimit", "2");
            builder.UseSetting("RateLimiting:Login:WindowSeconds", "60");
            builder.UseSetting("RateLimiting:Login:QueueLimit", "0");
            // Global limiter left generous so only the login-specific policy trips in this test.
            builder.UseSetting("RateLimiting:PermitLimit", "1000");
            builder.UseSetting(
                "ConnectionStrings:DefaultConnection",
                "Host=localhost;Database=CAMBIAR_ESTO;Username=CAMBIAR_ESTO;Password=CAMBIAR_ESTO");
            builder.UseSetting("Jwt:SigningKey", Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)));
        }
    }
}

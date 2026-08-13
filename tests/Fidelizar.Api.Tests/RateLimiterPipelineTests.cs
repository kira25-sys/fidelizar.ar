using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Fidelizar.Api.Tests;

/// <summary>
/// Completes F0-06b. ARCHITECTURE §8: "Verify the limiter is actually in the pipeline, not
/// merely registered in DI — a rate limiter configured but never added to the request pipeline
/// protects nothing while looking like it does." Until now that was only checked by hand with
/// curl. This drives real HTTP requests through Fidelizar.Api's actual pipeline via
/// <see cref="WebApplicationFactory{TEntryPoint}"/> and asserts a 429 shows up — something
/// reading <c>Program.cs</c> by eye cannot prove, because <c>AddAppRateLimiting</c> alone (DI
/// registration with no <c>app.UseRateLimiter()</c>) would compile and even "look" configured
/// while rejecting nothing.
///
/// Each test builds its own <see cref="LowLimitApiFactory"/> (no <c>IClassFixture</c>) rather
/// than sharing one: the rate limiter's state lives for the whole 60 s window, and TestServer
/// requests all land in the same "unknown"-IP partition, so a shared factory would let one test
/// exhaust the budget the next test depends on.
/// </summary>
public class RateLimiterPipelineTests
{
    [Fact]
    public async Task Exceeding_the_configured_limit_returns_429_from_the_real_pipeline()
    {
        using var factory = new LowLimitApiFactory();
        var client = factory.CreateClient();

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 10 && !statuses.Contains(HttpStatusCode.TooManyRequests); i++)
        {
            var response = await client.GetAsync("/health");
            statuses.Add(response.StatusCode);
        }

        Assert.Contains(HttpStatusCode.TooManyRequests, statuses);
    }

    [Fact]
    public async Task La_respuesta_429_trae_el_contrato_de_error_de_Shared()
    {
        using var factory = new LowLimitApiFactory();
        var client = factory.CreateClient();

        HttpResponseMessage? rejected = null;
        for (var i = 0; i < 10 && rejected is null; i++)
        {
            var response = await client.GetAsync("/health");
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                rejected = response;
            }
        }

        Assert.NotNull(rejected);
        Assert.StartsWith("application/json", rejected!.Content.Headers.ContentType?.MediaType);

        var body = await rejected.Content.ReadAsStringAsync();
        Assert.Contains("RATE_LIMIT_EXCEEDED", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Requests_dentro_del_limite_no_se_rechazan()
    {
        using var factory = new LowLimitApiFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.NotEqual(HttpStatusCode.TooManyRequests, response.StatusCode);
    }

    /// <summary>
    /// A tiny permit limit (2 requests / 60 s, no queue) so the test trips the limiter in a
    /// handful of requests instead of the production default of 100. Runs in the "Testing"
    /// hosting environment so <c>Program.cs</c> skips the real EF Core migration (F0-14) —
    /// CI has no Postgres to migrate against, and this test is only about the HTTP pipeline.
    /// The connection string below is a placeholder that is never opened: migrations do not run
    /// in this environment, and nothing else in this test touches the database.
    /// </summary>
    private sealed class LowLimitApiFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("RateLimiting:PermitLimit", "2");
            builder.UseSetting("RateLimiting:WindowSeconds", "60");
            builder.UseSetting("RateLimiting:QueueLimit", "0");
            builder.UseSetting(
                "ConnectionStrings:DefaultConnection",
                "Host=localhost;Database=CAMBIAR_ESTO;Username=CAMBIAR_ESTO;Password=CAMBIAR_ESTO");
        }
    }
}

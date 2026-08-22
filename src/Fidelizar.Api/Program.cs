using Fidelizar.Api.Configurations;
using Fidelizar.Api.Middleware;
using Fidelizar.Api.Monitoreo;
using Fidelizar.Api.Options;
using Fidelizar.Infrastructure.Configurations;
using Fidelizar.Infrastructure.Persistence;
using Fidelizar.Shared.Errors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Serilog;

namespace Fidelizar.Api;

public class Program
{
    public static async Task Main(string[] args)
    {
        // A simple bootstrap logger so startup failures before the host is built are still
        // logged somewhere, before the fully configured Serilog logger takes over.
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .CreateBootstrapLogger();

        try
        {
            Log.Information("Starting Fidelizar.Api");

            var builder = WebApplication.CreateBuilder(args);

            // ARCHITECTURE §14: the log enricher that stamps NegocioId on every line reads the
            // ambient HttpContext, which ASP.NET Core only publishes when this is registered.
            builder.Services.AddHttpContextAccessor();

            // One Add*Configuration extension per concern (ARCHITECTURE §15), so Program.cs
            // stays a table of contents rather than a wall of setup code.
            builder.AddSerilogConfiguration();
            builder.Services.AddAppHealthChecks(builder.Configuration);
            builder.Services.AddAppRateLimiting(builder.Configuration);
            builder.Services.AddInfrastructureConfiguration(builder.Configuration);
            builder.Services.AddApplicationServices();

            // ARCHITECTURE §8: throws here, before the host is built, if the signing key is
            // missing or too short — the app never starts rather than failing on the first login.
            builder.Services.AddAppAuthentication(builder.Configuration);
            builder.Services.AddAppAntiforgery();

            builder.Services
                .AddControllers()
                // Model-binding failures bypass ExceptionHandlingMiddleware — MVC short-circuits
                // before the action throws — so without this they answer the framework's
                // ProblemDetails instead of the one error shape every endpoint uses
                // (REST-CONTRACT-F1.md). The client parses ErrorResponse, so the cashier would
                // read a generic message instead of the Spanish one the DataAnnotation carries.
                .ConfigureApiBehaviorOptions(options =>
                    options.InvalidModelStateResponseFactory = contexto =>
                    {
                        var error = new ErrorResponse(
                            "VALIDACION_INVALIDA", "Revisá los datos ingresados.");

                        error.AddDetails(
                            contexto.ModelState
                                .Where(entrada => entrada.Value?.Errors.Count > 0)
                                .SelectMany(entrada => entrada.Value!.Errors.Select(
                                    e => (entrada.Key, e.ErrorMessage))));

                        return new BadRequestObjectResult(error);
                    });

            var app = builder.Build();

            // ARCHITECTURE §14: EF Core migrations run on container start, and a failed
            // migration aborts the start and leaves the previous version serving — never a half
            // migrated database answering questions about money. There is no inner try/catch on
            // purpose: a failure here falls through to the outer catch below, which logs it as
            // fatal and rethrows, so the process exits without ever calling RunAsync().
            //
            // When EF Core design-time tooling (e.g. "dotnet ef migrations add") spins this host
            // up just to read its services, builder.Build() throws HostAbortedException before
            // execution reaches this point, so a migration never runs during that.
            //
            // Skipped only in the "Testing" hosting environment: Fidelizar.Api.Tests'
            // RateLimiterPipelineTests (F0-06b) boots the real pipeline through
            // WebApplicationFactory to prove the limiter is actually wired in, and CI has no
            // Postgres to migrate against. Development, Staging and Production always migrate.
            if (!app.Environment.IsEnvironment("Testing"))
            {
                await using var migrationScope = app.Services.CreateAsyncScope();
                var dbContext = migrationScope.ServiceProvider.GetRequiredService<FidelizarDbContext>();
                Log.Information("Aplicando migraciones de EF Core");
                await dbContext.Database.MigrateAsync();
                Log.Information("Migraciones de EF Core aplicadas");
            }

            // ARCHITECTURE §14 / CLAUDE.md: see ConfigurarRequestLogging — the query string of a
            // member search never reaches the logged path.
            app.UseSerilogRequestLogging(LoggingConfigurationExtensions.ConfigurarRequestLogging);

            if (app.Environment.IsProduction())
            {
                app.UseHttpsRedirection();
            }

            app.UseRouting();

            // Early in the pipeline so it catches anything thrown by routing, rate limiting,
            // authentication, or a controller further down the chain.
            app.UseMiddleware<ExceptionHandlingMiddleware>();

            // Must be in the request pipeline, not just registered in DI (ARCHITECTURE §8) — a
            // rate limiter that is only configured via AddAppRateLimiting but never reaches
            // UseRateLimiter() protects nothing while looking like it does.
            app.UseRateLimiter();

            // ARCHITECTURE §8: reads the JWT from the auth cookie (AddAppAuthentication's
            // OnMessageReceived) and populates HttpContext.User; UseAuthorization enforces
            // [Authorize]/policies below it. Antiforgery validation itself runs per-endpoint via
            // Security.RequireAntiforgeryTokenAttribute, not as global middleware here.
            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllers();

            // ARCHITECTURE §14: the external uptime check per instance. Two probes, because
            // "the process is alive" and "the database answers" fail for different reasons and
            // need different reactions — /health/live stays 200 while Postgres is down, which is
            // what separates a dead host from a dead database on the owner's phone.
            //
            // /health keeps answering liveness so an already-configured monitor does not break.
            // F1-04: a monitoring probe carries no session, so the fallback policy would
            // otherwise reject all three with 401.
            var instancia = app.Services.GetRequiredService<IOptions<MonitoreoSettings>>().Value.Instancia;
            app.MapHealthChecks("/health", OpcionesDeSalud.Vivo(instancia)).AllowAnonymous();
            app.MapHealthChecks("/health/live", OpcionesDeSalud.Vivo(instancia)).AllowAnonymous();
            app.MapHealthChecks("/health/ready", OpcionesDeSalud.Listo(instancia)).AllowAnonymous();

            // ARCHITECTURE §3 "One deployable unit": Api serves the compiled WebAssembly client
            // from the same origin, so there is one container, one port and no CORS. The client's
            // assets reach Api's wwwroot through the ProjectReference in Fidelizar.Api.csproj.
            //
            // Mapped as endpoints, not as middleware ahead of UseRouting, so a request for a
            // static file still crosses UseAuthentication and UseAuthorization above.
            //
            // The one exception is the rate limiter, and it is not a preference: booting the
            // WebAssembly client downloads ~200 files at once, so a single first load blows past
            // the global 100/60s budget and the browser gets 429 for most of _framework/ — the
            // app never starts. Measured on 2026-08-21. Raising the limit is not the fix either:
            // the file count belongs to the framework, and one reload would trip it again. The
            // limiter exists to protect login and the API (ARCHITECTURE §8); a static file served
            // from the build manifest is not what it defends against.
            //
            // MapStaticAssets (not UseStaticFiles) because it answers from the build-time asset
            // manifest, which carries the right Content-Type for every file Blazor needs —
            // including _framework/*.dat, which the default extension provider does not know and
            // would refuse to serve.
            //
            // AllowAnonymous on both: F1-04's fallback policy requires an authenticated user, and
            // nobody can authenticate before the browser has downloaded the login screen. This
            // exposes nothing — Client compiles only against Shared (§3), and every endpoint that
            // returns data still enforces its own policy.
            app.MapStaticAssets().AllowAnonymous().DisableRateLimiting();

            // /api belongs to the server and is never a client route. Without this, the SPA
            // fallback below answers 200 index.html to an unknown /api path — measured, not
            // assumed: the test that asserts it was red before this line existed. A client
            // calling a mistyped or removed endpoint would then get HTML where it expected JSON,
            // and read a parse error instead of the 404 that tells it what actually happened.
            // "/api/{*path}" is a more specific pattern than the SPA catch-all, so it wins.
            app.MapFallback("/api/{*path}", (HttpContext context) =>
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return context.Response.WriteAsJsonAsync(
                    new ErrorResponse("NOT_FOUND", "The requested endpoint does not exist."));
            }).AllowAnonymous();

            // The SPA fallback: any path the client routes itself (/ingreso, /socios/buscar) has
            // to return index.html so Blazor can route it. Its pattern is "{*path:nonfile}" at
            // order int.MaxValue, so it is the last endpoint considered — /health, /api/* and
            // every static file are matched first.
            //
            // This is the Blazor-specific line: replacing the client with React means changing
            // the file this falls back to, and nothing else in this pipeline.
            app.MapFallbackToFile("index.html").AllowAnonymous().DisableRateLimiting();

            Log.Information("Fidelizar.Api started");

            await app.RunAsync();
        }
        catch (HostAbortedException)
        {
            // Expected when EF Core design-time tooling (e.g. "dotnet ef migrations add") spins
            // the host up just to read its services, then tears it down.
            Log.Information("Host aborted (expected during EF Core design-time tooling)");
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Fidelizar.Api failed to start");
            throw;
        }
        finally
        {
            Log.Information("Shutting down Fidelizar.Api");
            await Log.CloseAndFlushAsync();
        }
    }
}

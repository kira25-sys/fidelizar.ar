using Fidelizar.Api.Configurations;
using Fidelizar.Api.Middleware;
using Fidelizar.Infrastructure.Configurations;
using Fidelizar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
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

            // One Add*Configuration extension per concern (ARCHITECTURE §15), so Program.cs
            // stays a table of contents rather than a wall of setup code.
            builder.AddSerilogConfiguration();
            builder.Services.AddAppHealthChecks();
            builder.Services.AddAppRateLimiting(builder.Configuration);
            builder.Services.AddInfrastructureConfiguration(builder.Configuration);
            builder.Services.AddApplicationServices();

            // ARCHITECTURE §8: throws here, before the host is built, if the signing key is
            // missing or too short — the app never starts rather than failing on the first login.
            builder.Services.AddAppAuthentication(builder.Configuration);
            builder.Services.AddAppAntiforgery();

            builder.Services.AddControllers();

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

            app.UseSerilogRequestLogging();

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
            // F1-04: a monitoring probe carries no session, so the fallback policy would otherwise reject it with 401.
            app.MapHealthChecks("/health").AllowAnonymous();

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

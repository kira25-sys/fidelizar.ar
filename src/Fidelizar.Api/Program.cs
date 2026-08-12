using Fidelizar.Api.Configurations;
using Fidelizar.Api.Middleware;
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
            builder.Services.AddControllers();

            var app = builder.Build();

            app.UseSerilogRequestLogging();

            if (app.Environment.IsProduction())
            {
                app.UseHttpsRedirection();
            }

            app.UseRouting();

            // Early in the pipeline so it catches anything thrown by routing, rate limiting, or
            // a controller further down the chain.
            app.UseMiddleware<ExceptionHandlingMiddleware>();

            // Must be in the request pipeline, not just registered in DI (ARCHITECTURE §8) — a
            // rate limiter that is only configured via AddAppRateLimiting but never reaches
            // UseRateLimiter() protects nothing while looking like it does.
            app.UseRateLimiter();

            app.MapControllers();
            app.MapHealthChecks("/health");

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

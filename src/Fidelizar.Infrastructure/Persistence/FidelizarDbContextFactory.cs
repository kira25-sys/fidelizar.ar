using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Fidelizar.Infrastructure.Persistence;

/// <summary>
/// Design-time only: lets <c>dotnet ef</c> build <see cref="FidelizarDbContext"/> without
/// running Fidelizar.Api's full composition root (Fidelizar.Api does not carry
/// Microsoft.EntityFrameworkCore.Design, so it cannot be the tool's startup project).
/// Resolves the connection string from the same places the running API does, so that
/// <c>dotnet ef database update</c> and <c>dotnet run</c> can never point at different
/// databases. No connection string is ever committed (CLAUDE.md), not even a
/// placeholder-looking one.
/// </summary>
public sealed class FidelizarDbContextFactory : IDesignTimeDbContextFactory<FidelizarDbContext>
{
    /// <summary>
    /// Fidelizar.Api's <c>UserSecretsId</c>, hardcoded on purpose (2026-08-20).
    ///
    /// Giving Fidelizar.Infrastructure a secret store of its own was the alternative, and it
    /// costs more than it looks: the connection string would then have to be set twice, once
    /// for the API to run and once for <c>dotnet ef</c>, and the two copies drifting apart is
    /// exactly the failure being fixed here — the API silently reached the phase 0 gate
    /// database (the 293 real members) because the tooling and the app read different sources.
    /// One store, one value, no drift.
    ///
    /// This is a string literal, not a project reference: ARCHITECTURE §3's dependency
    /// direction is untouched — Infrastructure still does not reference Api. The GUID is
    /// committed in Fidelizar.Api.csproj and identifies a folder under the user profile; it is
    /// not itself a secret. <c>FidelizarDbContextFactoryTests</c> fails if the two ever diverge.
    /// </summary>
    internal const string ApiUserSecretsId = "00d761ed-0318-4878-93ea-637eed48a152";

    /// <summary>
    /// Reachable only when generating a migration with no database at hand: <c>dotnet ef
    /// migrations add</c> never opens the connection, so it must not be made to fail here.
    /// Throwing instead would break authoring migrations offline. <c>dotnet ef database
    /// update</c> does connect, and fails loudly against this host, which is the intended
    /// outcome for a genuinely unconfigured environment.
    /// </summary>
    internal const string PlaceholderConnectionString =
        "Host=localhost;Database=CAMBIAR_ESTO;Username=CAMBIAR_ESTO;Password=CAMBIAR_ESTO";

    public FidelizarDbContext CreateDbContext(string[] args)
    {
        var connectionString = ResolveConnectionString(BuildConfiguration());

        var optionsBuilder = new DbContextOptionsBuilder<FidelizarDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new FidelizarDbContext(optionsBuilder.Options);
    }

    /// <summary>
    /// User secrets, then environment variables — the same two sources, in the same order of
    /// precedence, that Fidelizar.Api's host applies (an environment variable wins).
    ///
    /// appsettings.json and appsettings.Development.json are deliberately NOT read, and this is
    /// not an oversight. Neither file can ever supply a connection string at design time:
    /// appsettings.json ships the literal <c>"CAMBIAR_ESTO"</c> and
    /// appsettings.Development.json has no <c>ConnectionStrings</c> section at all — and by
    /// CLAUDE.md neither one ever will, because a real connection string in a repository file
    /// is forbidden. Reading them would add no capability whatsoever, and would cost something
    /// real: Infrastructure would have to guess the physical path of Fidelizar.Api's folder to
    /// find them, which is a worse layering coupling than the GUID literal above.
    /// </summary>
    internal static IConfiguration BuildConfiguration() =>
        new ConfigurationBuilder()
            .AddUserSecrets(ApiUserSecretsId, reloadOnChange: false)
            .AddEnvironmentVariables()
            .Build();

    internal static string ResolveConnectionString(IConfiguration configuration)
    {
        var configured = configuration.GetConnectionString("DefaultConnection");

        return string.IsNullOrWhiteSpace(configured) ? PlaceholderConnectionString : configured;
    }
}

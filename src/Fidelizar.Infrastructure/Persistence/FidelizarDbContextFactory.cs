using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Fidelizar.Infrastructure.Persistence;

/// <summary>
/// Design-time only: lets <c>dotnet ef</c> build <see cref="FidelizarDbContext"/> without
/// running Fidelizar.Api's full composition root (Fidelizar.Api does not carry
/// Microsoft.EntityFrameworkCore.Design, so it cannot be the tool's startup project). The
/// connection string here is read from the environment and is never committed — CLAUDE.md
/// forbids a connection string or secret in the repository, even a placeholder-looking one. The
/// fallback below is only reachable when generating a migration with no database at hand
/// (<c>dotnet ef migrations add</c> never opens the connection); <c>dotnet ef database update</c>
/// needs a real one supplied via <c>ConnectionStrings__DefaultConnection</c>.
/// </summary>
public sealed class FidelizarDbContextFactory : IDesignTimeDbContextFactory<FidelizarDbContext>
{
    public FidelizarDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "Host=localhost;Database=CAMBIAR_ESTO;Username=CAMBIAR_ESTO;Password=CAMBIAR_ESTO";

        var optionsBuilder = new DbContextOptionsBuilder<FidelizarDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new FidelizarDbContext(optionsBuilder.Options);
    }
}

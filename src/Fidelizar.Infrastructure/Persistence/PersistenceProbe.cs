using Fidelizar.Domain.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Fidelizar.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of <see cref="IPersistenceProbe"/> (ARCHITECTURE §14). The cheapest
/// question that still proves the database is reachable and accepting connections.
/// </summary>
public sealed class PersistenceProbe(FidelizarDbContext dbContext) : IPersistenceProbe
{
    public async Task<bool> RespondeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await dbContext.Database.CanConnectAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            // A probe that throws would turn "the database is down" into a 500 with a stack
            // trace; the caller only ever needs the yes/no. The exception is deliberately not
            // logged here: Npgsql puts the connection string in some of its messages, and that
            // string carries the password (CLAUDE.md).
            return false;
        }
    }
}

using Fidelizar.Domain.Persistence;

namespace Fidelizar.Infrastructure.Persistence;

/// <summary>EF Core implementation of <see cref="IUnitOfWork"/>. Every repository call made
/// inside <paramref name="operacion" /> shares this same <see cref="FidelizarDbContext"/> — DI
/// scopes it per request (ARCHITECTURE §3), so the transaction opened here wraps every
/// <c>SaveChangesAsync</c> any of them issues.</summary>
public sealed class UnitOfWork(FidelizarDbContext dbContext) : IUnitOfWork
{
    public async Task EjecutarEnTransaccionAsync(
        Func<CancellationToken, Task> operacion, CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            await operacion(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

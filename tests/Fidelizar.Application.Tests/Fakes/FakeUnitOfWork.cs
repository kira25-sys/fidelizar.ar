using Fidelizar.Domain.Persistence;

namespace Fidelizar.Application.Tests.Fakes;

/// <summary>
/// In-memory stand-in for <see cref="IUnitOfWork"/> (ARCHITECTURE §11) — just runs the operation,
/// no real transaction. Good enough for every test that only cares about the happy path; tests
/// that need to prove a rollback actually undoes a partial write use
/// <c>AltaMiembroServiceTests</c>'s own transaction-simulating decorator instead, since undoing a
/// write requires knowing which fake repository to undo it on.
/// </summary>
public sealed class FakeUnitOfWork : IUnitOfWork
{
    public Task EjecutarEnTransaccionAsync(
        Func<CancellationToken, Task> operacion, CancellationToken cancellationToken = default) =>
        operacion(cancellationToken);
}

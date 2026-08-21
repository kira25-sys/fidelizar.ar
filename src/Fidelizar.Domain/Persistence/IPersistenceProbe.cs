namespace Fidelizar.Domain.Persistence;

/// <summary>
/// Answers "does the store respond?" for the readiness probe of ARCHITECTURE §14, without
/// handing the caller a <c>DbContext</c>. It is not a repository and it reads no data.
/// </summary>
public interface IPersistenceProbe
{
    /// <summary>True if the database answered. Never throws — a failure is a <c>false</c>.</summary>
    Task<bool> RespondeAsync(CancellationToken cancellationToken = default);
}

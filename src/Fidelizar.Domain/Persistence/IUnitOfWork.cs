namespace Fidelizar.Domain.Persistence;

/// <summary>
/// The transactional boundary S5 Alta de socio needs and nothing else in the product has needed
/// so far: <c>Miembro</c> and its mandatory <c>DatosPersonales</c> <c>Consentimiento</c> are two
/// different aggregates, written through two different repositories, and I10 requires that if
/// either write fails, neither is left behind — a member without a recorded consent is exactly
/// the legal hole phase 1 exists to close.
///
/// <para>
/// This is not the generic repository ARCHITECTURE §3 rejects — it exposes no entity operations
/// at all, only a transaction boundary around calls the caller still makes through the normal
/// per-aggregate repositories and services. <c>Domain</c> defines the interface; <c>Infrastructure</c>
/// is the only layer that knows what a transaction actually is (ARCHITECTURE §3: no EF, no SQL,
/// no HTTP in <c>Domain</c>).
/// </para>
/// </summary>
public interface IUnitOfWork
{
    /// <summary>
    /// Runs <paramref name="operacion"/> inside one database transaction. If it throws, every
    /// write <paramref name="operacion"/> made through any repository is rolled back — none of
    /// them is left half-committed.
    /// </summary>
    Task EjecutarEnTransaccionAsync(
        Func<CancellationToken, Task> operacion, CancellationToken cancellationToken = default);
}

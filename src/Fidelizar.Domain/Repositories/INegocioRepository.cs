using Fidelizar.Domain.Entities;

namespace Fidelizar.Domain.Repositories;

/// <summary>
/// The client business (DATA-MODEL §1). Authentication needs it: a cashier signs in with only an
/// email and a password, never a business selector (FUNCTIONAL-SPEC §13.2), yet I8 still requires
/// <c>NegocioId</c> to be an explicit value everywhere.
/// </summary>
public interface INegocioRepository
{
    /// <summary>
    /// Fails loudly when zero or more than one active <see cref="Negocio"/> exists, rather than
    /// silently picking a row: ARCHITECTURE §5 deploys one database per business, so a count
    /// other than one is a deployment misconfiguration, not a case to guess through.
    /// </summary>
    Task<Negocio> ObtenerUnicoAsync(CancellationToken cancellationToken = default);
}

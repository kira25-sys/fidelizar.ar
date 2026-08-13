using Fidelizar.Domain.Entities;

namespace Fidelizar.Domain.Repositories;

/// <summary>
/// The client business (DATA-MODEL §1). Exists here, ahead of any other need, because
/// authentication has to resolve <c>NegocioId</c> from somewhere: FUNCTIONAL-SPEC §13.2 has a
/// cashier sign in with only an email and a password, never a business selector, and I8 still
/// requires <c>NegocioId</c> to be an explicit value everywhere, never assumed.
///
/// <see cref="ObtenerUnicoAsync"/> is the only member: ARCHITECTURE §5 deploys one database per
/// business, so today there is exactly one row to find. This is an F1-03 addition outside
/// DATA-MODEL §2's literal scope, made because nothing else in the product resolves "which
/// business is this deployment" — flagged to the orchestrator in the F1-03 report for
/// confirmation, the same way any other identity-shaped assumption would be (CLAUDE.md, "ask when
/// in doubt").
/// </summary>
public interface INegocioRepository
{
    /// <summary>
    /// Fails loudly — the same discipline <see cref="ICorteRepository"/> already applies to a
    /// missing <c>Corte</c> — rather than silently picking a row when zero or more than one
    /// active <see cref="Negocio"/> exists. A count other than one is a deployment
    /// misconfiguration, not a case to guess through.
    /// </summary>
    Task<Negocio> ObtenerUnicoAsync(CancellationToken cancellationToken = default);
}

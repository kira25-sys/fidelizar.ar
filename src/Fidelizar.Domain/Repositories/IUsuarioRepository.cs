using Fidelizar.Domain.Entities;

namespace Fidelizar.Domain.Repositories;

/// <summary>
/// Users. <c>NegocioId</c> is a required parameter on every member (I8) — even the email lookup a
/// login depends on cannot skip it. Minimal on purpose: the rest of the S10 screen's operations
/// land with F1-13, never as a generic <c>GetAll&lt;T&gt;</c> (ARCHITECTURE §3).
/// </summary>
public interface IUsuarioRepository
{
    /// <summary>
    /// The user with this email within the business, or null. Matches the unique index on
    /// <c>(NegocioId, Email)</c>; <c>Email</c> is <c>citext</c>, so the comparison is
    /// case-insensitive at the database level.
    /// </summary>
    Task<Usuario?> ObtenerPorEmailAsync(int negocioId, string email, CancellationToken cancellationToken = default);

    Task<Usuario> CrearAsync(Usuario usuario, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists a deactivation. Never a delete — only <see cref="Usuario.Activo"/> changes
    /// (DATA-MODEL §2). <paramref name="usuario"/> must already be tracked by this repository's
    /// <c>DbContext</c>, with <see cref="Usuario.Desactivar"/> already called on it.
    /// </summary>
    Task DesactivarAsync(Usuario usuario, CancellationToken cancellationToken = default);
}

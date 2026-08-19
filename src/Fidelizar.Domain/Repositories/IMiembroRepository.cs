using Fidelizar.Domain.Entities;

namespace Fidelizar.Domain.Repositories;

/// <summary>
/// Members. <c>NegocioId</c> is a required parameter on every member (I8), so there is nowhere to
/// forget the tenant filter. Minimal on purpose — search and reactivation land in phase 1 as
/// their own methods here, never as a generic <c>GetAll&lt;T&gt;</c> (ARCHITECTURE §3).
/// </summary>
public interface IMiembroRepository
{
    /// <summary>
    /// The member linked to this POS id, or null when none is linked yet. Matches the partial
    /// unique index on <c>(NegocioId, ClienteExternoId)</c> (DATA-MODEL §3); there is no
    /// equivalent lookup for unlinked members, because <c>null</c> is not a key.
    /// </summary>
    Task<Miembro?> GetByClienteExternoIdAsync(
        int negocioId, string clienteExternoId, CancellationToken cancellationToken = default);

    /// <summary>
    /// A single member by id, or null. The lookup S3/S6 needed and never had: without it,
    /// <c>SUM(Monto)</c> over zero rows cannot be told apart from a real member with a $0 balance
    /// (REST-CONTRACT-F1.md's documented gap in <c>ObtenerSaldo</c>).
    /// </summary>
    Task<Miembro?> GetByIdAsync(int negocioId, int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// S2 Buscar socio — search, never a listing (FUNCTIONAL-SPEC §4): there is no "all members"
    /// variant of this call. Matches every one of <paramref name="palabrasNormalizadas"/> as a
    /// substring of <c>NombreNormalizado</c> (AND across words — "gomez ana" finds
    /// "Ana María Gómez" only because both words are present, not because either one alone is).
    /// The caller normalises and splits the query first (<c>VipNombres</c>); this method does not
    /// decide when a query is too short to search — that is I7/FUNCTIONAL-SPEC §4's call, made
    /// once, in the Application service, not repeated per repository.
    /// </summary>
    Task<IReadOnlyList<Miembro>> BuscarAsync(
        int negocioId, IReadOnlyList<string> palabrasNormalizadas, CancellationToken cancellationToken = default);

    Task<Miembro> AddAsync(Miembro miembro, CancellationToken cancellationToken = default);
}

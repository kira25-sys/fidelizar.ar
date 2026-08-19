namespace Fidelizar.Application.Services;

/// <summary>One S2 search result row — name, member number, balance, and the date that
/// disambiguates a homonym group (FUNCTIONAL-SPEC §4, FLOW-S2-S5 §1.4).</summary>
public sealed record MiembroBusquedaResultado(
    int Id, string Nombre, string? NumeroSocio, decimal Saldo, DateOnly FechaAlta);

/// <summary>
/// S2 Buscar socio (FUNCTIONAL-SPEC §4). The whole point of this service is the negative: there
/// is no method here that returns every member of a business. "Search, never browse" is enforced
/// once, in the one place a query reaches the database.
/// </summary>
public interface IMiembroBusquedaService
{
    /// <summary>
    /// Candidates matching every word of <paramref name="query"/> against
    /// <c>Miembro.NombreNormalizado</c>. Rejects a query shorter than 3 normalised characters
    /// (I7, FUNCTIONAL-SPEC §4) rather than falling back to "no results" — a caller bypassing the
    /// client's own 3-character gate must not get a listing by omission.
    /// </summary>
    Task<IReadOnlyList<MiembroBusquedaResultado>> BuscarAsync(
        int negocioId, string query, CancellationToken cancellationToken = default);
}

using Fidelizar.Domain.Entities;

namespace Fidelizar.Domain.Repositories;

/// <summary>
/// Consent records (DATA-MODEL §3, I10). Append-only, like <see cref="IMovimientoRepository"/>:
/// there is no Update and no Delete here, on purpose — a withdrawal is a new row with
/// <see cref="Consentimiento.Otorgado"/> <c>false</c>, never an edit of an old one. A reflection
/// test (<c>RepositoryInterfacesTests</c>) asserts no repository in this namespace ever grows a
/// method whose name suggests deletion or update. <c>NegocioId</c> is a required parameter on
/// every member (I8).
/// </summary>
public interface IConsentimientoRepository
{
    /// <summary>
    /// The current consent for a <c>(MiembroId, Tipo)</c> pair — the newest row, or null when
    /// none was ever recorded. Because the table is append-only, "newest" is exactly what
    /// "current" means (DATA-MODEL §3).
    /// </summary>
    Task<Consentimiento?> GetVigenteAsync(
        int negocioId, int miembroId, TipoConsentimiento tipo, CancellationToken cancellationToken = default);

    /// <summary>The member's full consent history across every type, newest first — the ficha
    /// completa (S6, F1-10) will read this to show what was agreed and when.</summary>
    Task<IReadOnlyList<Consentimiento>> GetHistorialAsync(
        int negocioId, int miembroId, CancellationToken cancellationToken = default);

    /// <summary>Appends one consent row — a grant or a withdrawal, both just rows.</summary>
    Task<Consentimiento> AppendAsync(Consentimiento consentimiento, CancellationToken cancellationToken = default);
}

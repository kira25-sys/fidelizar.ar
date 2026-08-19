using Fidelizar.Domain.Entities;

namespace Fidelizar.Application.Services;

/// <summary>What the caller supplies to grant or withdraw a consent (DATA-MODEL §3).</summary>
/// <param name="NegocioId">Required, not a convention (I8).</param>
/// <param name="MiembroId">The member the consent is about.</param>
/// <param name="Tipo">Which kind of data the consent covers.</param>
/// <param name="VersionTexto">Which wording of the consent text the member was shown. The actual
/// legal wording for the two consent texts is an open decision (docs/README.md #3, owned by the
/// business owner) — this service does not invent one, it only records whichever version the
/// caller supplies.</param>
/// <param name="Canal">How it was obtained.</param>
/// <param name="OcurridoEn">When it happened, in UTC.</param>
/// <param name="RegistradoPorUsuarioId">Who recorded it. Null for self-service.</param>
public sealed record OtorgarConsentimientoRequest(
    int NegocioId,
    int MiembroId,
    TipoConsentimiento Tipo,
    string VersionTexto,
    CanalConsentimiento Canal,
    DateTime OcurridoEn,
    int? RegistradoPorUsuarioId);

/// <summary>Same shape as a grant — a withdrawal is just a row with <c>Otorgado = false</c>
/// (DATA-MODEL §3), never an update to the granting row.</summary>
public sealed record RevocarConsentimientoRequest(
    int NegocioId,
    int MiembroId,
    TipoConsentimiento Tipo,
    string VersionTexto,
    CanalConsentimiento Canal,
    DateTime OcurridoEn,
    int? RegistradoPorUsuarioId);

public interface IConsentimientoService
{
    /// <summary>The current consent for a type, or null when none was ever recorded.</summary>
    Task<Consentimiento?> ObtenerVigenteAsync(
        int negocioId, int miembroId, TipoConsentimiento tipo, CancellationToken cancellationToken = default);

    /// <summary>Whether there is a current, granted consent of this type.</summary>
    Task<bool> EstaVigenteAsync(
        int negocioId, int miembroId, TipoConsentimiento tipo, CancellationToken cancellationToken = default);

    /// <summary>Grants a consent — appends a new row with <c>Otorgado = true</c>.</summary>
    Task<Consentimiento> OtorgarAsync(
        OtorgarConsentimientoRequest request, CancellationToken cancellationToken = default);

    /// <summary>Withdraws a consent — appends a new row with <c>Otorgado = false</c>. Never edits
    /// or removes the row it supersedes (I1-adjacent discipline for consent).</summary>
    Task<Consentimiento> RevocarAsync(
        RevocarConsentimientoRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// The gate every write path for a sensitive field must call before persisting anything
    /// (I10, DATA-MODEL §3). Looks up the current consent and hands it to
    /// <see cref="Fidelizar.Domain.Consentimientos.ConsentimientoPolicy.RequerirVigente"/>; throws
    /// <c>ValidationException("CONSENTIMIENTO_REQUERIDO")</c> when it is missing, withdrawn, or of
    /// a different type than <paramref name="tipo"/>.
    /// </summary>
    Task RequerirVigenteAsync(
        int negocioId, int miembroId, TipoConsentimiento tipo, CancellationToken cancellationToken = default);
}

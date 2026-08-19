using Fidelizar.Domain.Entities;
using Fidelizar.Domain.Exceptions;

namespace Fidelizar.Domain.Consentimientos;

/// <summary>
/// I10: sensitive fields (diet, allergies and other health-adjacent data — <c>PerfilMiembro</c>,
/// F3-01) cannot be written without a recorded, granted consent of the matching
/// <see cref="TipoConsentimiento"/> (DATA-MODEL §3, Ley 25.326). This is the rule DATA-MODEL §3
/// calls out as "enforced in the domain service": a pure, DB-free check
/// (ARCHITECTURE §11) so it is testable with no database and so every future write path — the
/// one <c>PerfilMiembroService</c> will need, and any other sensitive-field write that comes
/// after it — has exactly one place to call instead of re-deriving the rule at its own endpoint.
///
/// This class does not reach a database itself; <see cref="Fidelizar.Domain.Repositories.IConsentimientoRepository.GetVigenteAsync"/>
/// resolves "the current consent" and hands it here to be judged.
/// </summary>
public static class ConsentimientoPolicy
{
    /// <summary>
    /// Whether <paramref name="vigente"/> is enough to permit writing a field that requires
    /// <paramref name="tipoRequerido"/>. False for: no consent on record, a consent of a
    /// different type (a caller mistake — <see cref="RequerirVigente"/> is the one that should
    /// be used to look this up so the type can never mismatch), and a consent whose newest row
    /// is a withdrawal (<see cref="Consentimiento.Otorgado"/> <c>false</c>).
    /// </summary>
    public static bool PermiteEscritura(Consentimiento? vigente, TipoConsentimiento tipoRequerido) =>
        vigente is not null
        && vigente.Tipo == tipoRequerido
        && vigente.Otorgado;

    /// <summary>
    /// Throws when <paramref name="vigente"/> does not permit writing a field that requires
    /// <paramref name="tipoRequerido"/>. The single call every write path for a sensitive field
    /// must make before touching a row — not the controller of the day, this rule.
    /// </summary>
    /// <exception cref="ValidationException">
    /// Error code <c>CONSENTIMIENTO_REQUERIDO</c>. Mirrors the shape <c>SaldoService</c> already
    /// uses for a blocked business rule (RN-25's "saldo en revisión"), so the API's exception
    /// middleware needs no new mapping to surface this as HTTP 400.
    /// </exception>
    public static void RequerirVigente(Consentimiento? vigente, TipoConsentimiento tipoRequerido)
    {
        if (PermiteEscritura(vigente, tipoRequerido))
        {
            return;
        }

        throw new ValidationException(
            $"No hay un consentimiento vigente de tipo '{tipoRequerido}' para este socio. No se " +
            "puede escribir un dato que lo requiere (I10, Ley 25.326).",
            "CONSENTIMIENTO_REQUERIDO");
    }
}

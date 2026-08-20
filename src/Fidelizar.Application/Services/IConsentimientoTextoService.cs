using Fidelizar.Domain.Entities;

namespace Fidelizar.Application.Services;

/// <summary>The resolved wording for one consent type — <see cref="Fidelizar.Domain.Consentimientos.TextosConsentimiento"/>'s
/// template with this business's own name/CUIT/address substituted in.</summary>
public sealed record ConsentimientoTextoResultado(TipoConsentimiento Tipo, string VersionTexto, string Texto);

/// <summary>
/// Serves S5's consent checkboxes the actual legal wording to show the member — "the endpoint,
/// not the screen": the frontend (F1-09) needs real text, not a hardcoded copy that would put the
/// business's own CUIT and address into client code (CLAUDE.md: nothing personal or business-owned
/// reaches <c>Shared</c>/<c>Client</c> as a literal).
/// </summary>
public interface IConsentimientoTextoService
{
    /// <summary>
    /// The resolved text for <paramref name="tipo"/>. Only <c>DatosPersonales</c> and
    /// <c>DatosSensibles</c> have an approved text (README open decision #3, resolved 2026-08-19)
    /// — <c>Comunicaciones</c> has none yet.
    /// </summary>
    /// <param name="negocioId">
    /// The caller's own business, from the token — required, not a convention (I8). A deployment
    /// serves one business (ARCHITECTURE §5), so this is checked against the row rather than
    /// trusted: a token for a different business than the one in this database gets an error, not
    /// another business's CUIT and address.
    /// </param>
    /// <param name="tipo">Which consent text to resolve.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="Fidelizar.Domain.Exceptions.ValidationException">
    /// <paramref name="tipo"/> has no approved text (<c>TIPO_CONSENTIMIENTO_SIN_TEXTO</c>).
    /// </exception>
    /// <exception cref="Fidelizar.Domain.Exceptions.ConflictException">
    /// <paramref name="negocioId"/> is not the business this database holds
    /// (<c>NEGOCIO_AJENO</c>).
    /// </exception>
    Task<ConsentimientoTextoResultado> ObtenerAsync(
        int negocioId, TipoConsentimiento tipo, CancellationToken cancellationToken = default);
}

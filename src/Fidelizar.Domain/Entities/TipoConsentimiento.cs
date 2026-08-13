namespace Fidelizar.Domain.Entities;

/// <summary>
/// What a <see cref="Consentimiento"/> row covers (DATA-MODEL §3). Persisted as <c>int</c> —
/// values are <b>never reordered and never reused</b>, the same discipline as
/// <see cref="TipoMovimientoCredito"/>.
/// </summary>
public enum TipoConsentimiento
{
    /// <summary>Name, phone, DNI, birthday — ordinary personal data.</summary>
    DatosPersonales = 0,

    /// <summary>Diet, allergies and other health-adjacent data (Law 25.326, I10).</summary>
    DatosSensibles = 1,

    /// <summary>Being contacted (WhatsApp, later phases).</summary>
    Comunicaciones = 2,
}

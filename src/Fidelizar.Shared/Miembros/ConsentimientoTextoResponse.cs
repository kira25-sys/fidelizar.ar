namespace Fidelizar.Shared.Miembros;

/// <summary>
/// The resolved wording S5's consent checkboxes show the member (FUNCTIONAL-SPEC §7) — this
/// business's own name/CUIT/address already substituted in, never a literal shipped to the
/// browser. <c>VersionTexto</c> is what a <c>Consentimiento</c> row actually stores when the
/// member accepts.
/// </summary>
public sealed record ConsentimientoTextoResponse(string Tipo, string VersionTexto, string Texto);

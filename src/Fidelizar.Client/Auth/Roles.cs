namespace Fidelizar.Client.Auth;

/// <summary>
/// Role names exactly as they travel in <c>SesionResponse.Rol</c>. Mirrors
/// <c>Fidelizar.Api.Security.Roles</c> — <c>Client</c> references only <c>Shared</c>
/// (ARCHITECTURE §3), the same mirroring <c>ApiClient</c> does for the CSRF header name.
/// </summary>
public static class Roles
{
    public const string Cajero = "Cajero";
    public const string Encargada = "Encargada";
    public const string Dueno = "Dueno";
    public const string Soporte = "Soporte";

    /// <summary>
    /// Mirrors the server's <c>EncargadaOrAbove</c> policy — S6, S7, S8 and S9
    /// (FUNCTIONAL-SPEC §3). **Presentation only.** Hiding a link is not protection: every one of
    /// those endpoints carries <c>[Authorize(Policy = Policies.EncargadaOrAbove)]</c> and a
    /// <c>Cajero</c> who types the URL gets a server <c>403</c> regardless of what this returns.
    /// </summary>
    public static bool EsEncargadaODueno(string? rol) => rol is Encargada or Dueno;
}

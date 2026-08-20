namespace Fidelizar.Client.Auth;

/// <summary>
/// The four role identifiers as they travel on the wire, mirrored from DATA-MODEL §2. Client
/// cannot reference Domain's <c>RolUsuario</c> nor Api's <c>Roles</c> (ARCHITECTURE §3), so the
/// strings are repeated here — the same mirroring <c>ApiClient</c> already does for the CSRF
/// header name. <c>Sistema</c> is absent on purpose: it never signs in and no account may ever
/// be created under it.
///
/// Nothing here is a permission check. Authorisation is server-side (ARCHITECTURE §8); these
/// constants only decide what a screen bothers to render.
/// </summary>
public static class Roles
{
    public const string Cajero = "Cajero";
    public const string Encargada = "Encargada";
    public const string Dueno = "Dueno";
    public const string Soporte = "Soporte";

    /// <summary>Every role the owner may create in S10, in the order the form offers them.</summary>
    public static readonly IReadOnlyList<string> Asignables = [Cajero, Encargada, Dueno, Soporte];

    /// <summary>UI text, where the identifier is not: "Dueño" carries the ñ, "Dueno" never does.</summary>
    public static string Etiqueta(string rol) => rol switch
    {
        Cajero => "Cajero",
        Encargada => "Encargada",
        Dueno => "Dueño",
        Soporte => "Soporte",
        _ => rol,
    };

    /// <summary>
    /// DATA-MODEL §2: a Cajero and an Encargada belong to exactly one branch, a Dueño and a
    /// Soporte to none. Enforced by <c>Usuario.Crear</c> — this only lets S10's form show or hide
    /// the branch field instead of waiting for a SUCURSAL_REQUERIDA the owner could have avoided.
    /// </summary>
    public static bool RequiereSucursal(string rol) => rol is Cajero or Encargada;
}

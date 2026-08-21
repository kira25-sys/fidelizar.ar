using Fidelizar.Api.Security;
using Fidelizar.Shared.Auth;
using Fidelizar.Shared.Miembros;
using Fidelizar.Shared.Sucursales;
using Fidelizar.Shared.Usuarios;
using AnularRequest = Fidelizar.Shared.Movimientos.AnularMovimientoRequest;
using CanjeRequest = Fidelizar.Shared.Movimientos.RegistrarCanjeRequest;

namespace Fidelizar.Api.Tests.Security;

/// <summary>The policy an endpoint is expected to enforce, transcribed from the "Endpoint table"
/// of docs/REST-CONTRACT-F1.md — that document is the source, not the code.</summary>
public enum PoliticaEsperada
{
    /// <summary>`[AllowAnonymous]`: reachable with no session at all.</summary>
    Anonima,

    /// <summary>`[Authorize]` with no policy: any valid session, any role.</summary>
    CualquierSesion,
    CajeroOrAbove,
    EncargadaOrAbove,
    DuenoOnly,
}

/// <summary>One row of the matrix.</summary>
/// <param name="Metodo">HTTP verb.</param>
/// <param name="Ruta">A concrete URL to call, with invented ids.</param>
/// <param name="Plantilla">The routing template, so
/// <c>MatrizDePermisosPipelineTests.La_matriz_cubre_todos_los_endpoints_del_ensamblado</c> can
/// cross-check the matrix against every action actually declared in <c>Fidelizar.Api</c>.</param>
/// <param name="Politica">What docs/REST-CONTRACT-F1.md says this route enforces.</param>
/// <param name="RequiereAntiforgery">State-changing routes carry <c>[AntiforgeryTokenRequired]</c>.</param>
/// <param name="Cuerpo">A body good enough to reach the controller when the caller is allowed through.</param>
public sealed record EndpointDeLaMatriz(
    string Metodo,
    string Ruta,
    string Plantilla,
    PoliticaEsperada Politica,
    bool RequiereAntiforgery,
    object? Cuerpo = null)
{
    public override string ToString() => $"{Metodo} {Ruta}";

    /// <summary>Verb plus template — the identity a controller action is matched by.</summary>
    public string ClaveDeRuteo => $"{Metodo} {Plantilla}";
}

/// <summary>
/// The F1-15 matrix (ROADMAP): every endpoint of docs/REST-CONTRACT-F1.md against every role and
/// against the anonymous caller. Data only — <see cref="MatrizDePermisosPipelineTests"/> is what
/// drives it through the real HTTP pipeline.
///
/// That table has 18 rows and describes 20 HTTP endpoints: `/api/usuarios` and `/api/sucursales`
/// each carry "GET / POST" in a single row.
/// </summary>
public static class MatrizDePermisos
{
    /// <summary>The branch both the Cajero and the Encargada of these tests are stationed at
    /// (DATA-MODEL §2: those two roles always carry one; Dueño and Soporte never do).</summary>
    public const int SucursalDelPersonal = 1;

    /// <summary>A different branch, for the S9 branch-axis negative (ARCHITECTURE §8).</summary>
    public const int SucursalAjena = 2;

    private static readonly DateOnly Hoy = DateOnly.FromDateTime(DateTime.UtcNow);

    /// <summary>Invented data only (CLAUDE.md) — no member, user or branch here resembles a real
    /// one, and none of it is ever written to a database: every Application service is a stub.</summary>
    public static IReadOnlyList<EndpointDeLaMatriz> Endpoints { get; } =
    [
        // S1 Ingreso
        new("GET", "/api/auth/csrf-token", "api/auth/csrf-token",
            PoliticaEsperada.Anonima, RequiereAntiforgery: false),
        new("POST", "/api/auth/login", "api/auth/login",
            PoliticaEsperada.Anonima, RequiereAntiforgery: true,
            new LoginRequest("cajera.ficticia@ejemplo.test", "clave-de-prueba")),
        new("POST", "/api/auth/logout", "api/auth/logout",
            PoliticaEsperada.CualquierSesion, RequiereAntiforgery: true),
        new("GET", "/api/auth/me", "api/auth/me",
            PoliticaEsperada.CualquierSesion, RequiereAntiforgery: false),

        // S2 Buscar socio
        new("GET", "/api/miembros?q=ficticia", "api/miembros",
            PoliticaEsperada.CajeroOrAbove, RequiereAntiforgery: false),

        // S3 Ficha del socio
        new("GET", "/api/miembros/42/saldo", "api/miembros/{miembroId:int}/saldo",
            PoliticaEsperada.CajeroOrAbove, RequiereAntiforgery: false),
        new("GET", "/api/miembros/42/ficha-mostrador", "api/miembros/{miembroId:int}/ficha-mostrador",
            PoliticaEsperada.CajeroOrAbove, RequiereAntiforgery: false),

        // S4 Registrar canje
        new("POST", "/api/miembros/42/canjes", "api/miembros/{miembroId:int}/canjes",
            PoliticaEsperada.CajeroOrAbove, RequiereAntiforgery: true,
            new CanjeRequest(100m, Hoy, "Canje ficticio de prueba", "clave-idempotencia-de-prueba")),

        // S5 Alta de socio
        new("POST", "/api/miembros", "api/miembros",
            PoliticaEsperada.CajeroOrAbove, RequiereAntiforgery: true,
            new AltaMiembroRequest(
                "Socia Ficticia De Prueba", null, null, null, null, null, null,
                ConsentimientoDatosPersonales: true, ConsentimientoDatosSensibles: false)),
        new("GET", "/api/miembros/consentimiento-texto/DatosPersonales", "api/miembros/consentimiento-texto/{tipo}",
            PoliticaEsperada.CajeroOrAbove, RequiereAntiforgery: false),

        // S6 Ficha completa — the only endpoint that ever returns Telefono/Dni.
        new("GET", "/api/miembros/42/completo", "api/miembros/{miembroId:int}/completo",
            PoliticaEsperada.EncargadaOrAbove, RequiereAntiforgery: false),

        // S7 Historial de movimientos
        new("GET", "/api/miembros/42/movimientos", "api/miembros/{miembroId:int}/movimientos",
            PoliticaEsperada.EncargadaOrAbove, RequiereAntiforgery: false),

        // S8 Anular movimiento
        new("POST", "/api/movimientos/77/anular", "api/movimientos/{movimientoId:long}/anular",
            PoliticaEsperada.EncargadaOrAbove, RequiereAntiforgery: true,
            new AnularRequest("Error de tipeo en el mostrador")),

        // S9 Cierre diario
        new("GET", $"/api/sucursales/{SucursalDelPersonal}/cierre-diario?fecha=2026-08-21",
            "api/sucursales/{sucursalId:int}/cierre-diario",
            PoliticaEsperada.EncargadaOrAbove, RequiereAntiforgery: false),

        // S10 Usuarios
        new("GET", "/api/usuarios", "api/usuarios",
            PoliticaEsperada.DuenoOnly, RequiereAntiforgery: false),
        new("POST", "/api/usuarios", "api/usuarios",
            PoliticaEsperada.DuenoOnly, RequiereAntiforgery: true,
            new CrearUsuarioRequest(
                "Encargada Ficticia", "encargada.ficticia@ejemplo.test", "clave-de-prueba", "Encargada",
                SucursalDelPersonal)),

        // S10 Sucursales
        new("GET", "/api/sucursales", "api/sucursales",
            PoliticaEsperada.DuenoOnly, RequiereAntiforgery: false),
        new("POST", "/api/sucursales", "api/sucursales",
            PoliticaEsperada.DuenoOnly, RequiereAntiforgery: true,
            new CrearSucursalRequest("Sucursal Ficticia", "SUC-FICTICIA")),

        // F1-14 Socios sin vincular
        new("GET", "/api/miembros/sin-vincular", "api/miembros/sin-vincular",
            PoliticaEsperada.EncargadaOrAbove, RequiereAntiforgery: false),
        new("POST", "/api/miembros/42/vinculacion", "api/miembros/{miembroId:int}/vinculacion",
            PoliticaEsperada.EncargadaOrAbove, RequiereAntiforgery: true,
            new VincularClienteExternoRequest("POS-FICTICIO-1")),
    ];

    /// <summary>Every role this product issues a token for (Security/Roles.cs).</summary>
    public static IReadOnlyList<string> TodosLosRoles { get; } =
        [Roles.Cajero, Roles.Encargada, Roles.Dueno, Roles.Soporte];

    /// <summary>ARCHITECTURE §8's ladder. Soporte is deliberately outside it — it satisfies no
    /// business policy, only "there is a session".</summary>
    public static IReadOnlyList<string> RolesQuePasan(PoliticaEsperada politica) => politica switch
    {
        PoliticaEsperada.Anonima or PoliticaEsperada.CualquierSesion => TodosLosRoles,
        PoliticaEsperada.CajeroOrAbove => [Roles.Cajero, Roles.Encargada, Roles.Dueno],
        PoliticaEsperada.EncargadaOrAbove => [Roles.Encargada, Roles.Dueno],
        PoliticaEsperada.DuenoOnly => [Roles.Dueno],
        _ => throw new ArgumentOutOfRangeException(nameof(politica)),
    };

    public static IReadOnlyList<string> RolesQueReciben403(PoliticaEsperada politica) =>
        TodosLosRoles.Except(RolesQuePasan(politica)).ToList();

    public static EndpointDeLaMatriz Buscar(string metodo, string ruta) =>
        Endpoints.Single(e => e.Metodo == metodo && e.Ruta == ruta);
}

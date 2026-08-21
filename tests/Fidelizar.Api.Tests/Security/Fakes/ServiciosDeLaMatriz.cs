using Fidelizar.Application.Services;
using Fidelizar.Domain.Entities;

namespace Fidelizar.Api.Tests.Security.Fakes;

/// <summary>
/// The invented record every stub below answers with (CLAUDE.md: never real member data). The
/// phone and DNI are sentinels on purpose: <see cref="Fidelizar.Api.Tests.Security.FichaCompletaPipelineTests"/>
/// asserts they reach an Encargada and never reach a Cajero, and a sentinel makes both halves
/// unambiguous.
/// </summary>
public static class DatosFicticiosDeLaMatriz
{
    public const int NegocioId = 7;
    public const int MiembroId = 42;
    public const string NombreDelMiembro = "Socia Ficticia De Prueba";
    public const string TelefonoFicticio = "+54 9 11 5555-0001";
    public const string DniFicticio = "30111222";
    public const string NumeroSocio = "SOC-FICTICIO-0001";

    public static DateOnly Hoy => DateOnly.FromDateTime(DateTime.UtcNow);

    public static DateOnly FechaDeCorte => new(2026, 1, 1);
}

/// <summary>
/// In-memory stand-ins for every <c>Fidelizar.Application</c> service the controllers depend on.
/// The matrix is about who reaches an endpoint, not about what the endpoint computes, and CI has
/// no Postgres (ARCHITECTURE §11) — with these registered, an authorised call answers 2xx instead
/// of failing on a connection and hiding the status under test.
///
/// Deliberately separate from <c>Controllers/Fakes</c>: those record what a controller asked for,
/// these only need to return something well-formed.
/// </summary>
public sealed class SaldoServiceDeLaMatriz : ISaldoService
{
    public Task<decimal> ObtenerSaldoAsync(int negocioId, int miembroId, CancellationToken cancellationToken = default) =>
        Task.FromResult(1500m);

    public Task<MovimientoCredito> RegistrarCanjeAsync(
        RegistrarCanjeRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(MovimientoCredito.Crear(
            request.NegocioId, request.MiembroId, request.FechaEfectiva, DateTime.UtcNow,
            TipoMovimientoCredito.Canje, -request.Monto, request.Hoy, request.UsuarioId, request.Motivo,
            claveIdempotencia: request.ClaveIdempotencia));
}

public sealed class CorteServiceDeLaMatriz : ICorteService
{
    public Task<Corte> ObtenerCorteVigenteAsync(int negocioId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Corte.Declarar(negocioId, DatosFicticiosDeLaMatriz.FechaDeCorte, 1, DateTime.UtcNow));

    public Task<Corte> DeclararCorteAsync(
        int negocioId, DateOnly fecha, int declaradoPorUsuarioId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Corte.Declarar(negocioId, fecha, declaradoPorUsuarioId, DateTime.UtcNow));
}

public sealed class MiembroBusquedaServiceDeLaMatriz : IMiembroBusquedaService
{
    public Task<IReadOnlyList<MiembroBusquedaResultado>> BuscarAsync(
        int negocioId, string query, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<MiembroBusquedaResultado>>(
        [
            new(DatosFicticiosDeLaMatriz.MiembroId, DatosFicticiosDeLaMatriz.NombreDelMiembro,
                DatosFicticiosDeLaMatriz.NumeroSocio, 1500m, DatosFicticiosDeLaMatriz.FechaDeCorte),
        ]);
}

public sealed class FichaMostradorServiceDeLaMatriz : IFichaMostradorService
{
    public Task<FichaMostradorResultado> ObtenerAsync(
        int negocioId, int miembroId, DateOnly hoy, CancellationToken cancellationToken = default) =>
        Task.FromResult(new FichaMostradorResultado(
            miembroId, DatosFicticiosDeLaMatriz.NombreDelMiembro, DatosFicticiosDeLaMatriz.NumeroSocio,
            1500m, DatosFicticiosDeLaMatriz.FechaDeCorte, []));
}

/// <summary>Carries the sentinel phone and DNI — S6 is the only endpoint that may ever return
/// them (FUNCTIONAL-SPEC §8).</summary>
public sealed class FichaCompletaServiceDeLaMatriz : IFichaCompletaService
{
    public Task<FichaCompletaResultado> ObtenerAsync(
        int negocioId, int miembroId, int usuarioIdQueLee, CancellationToken cancellationToken = default) =>
        Task.FromResult(new FichaCompletaResultado(
            miembroId,
            DatosFicticiosDeLaMatriz.NombreDelMiembro,
            DatosFicticiosDeLaMatriz.NumeroSocio,
            "POS-FICTICIO-1",
            DatosFicticiosDeLaMatriz.TelefonoFicticio,
            DatosFicticiosDeLaMatriz.DniFicticio,
            new DateOnly(2000, 3, 14),
            MatrizDePermisos.SucursalDelPersonal,
            Activo: true));
}

public sealed class HistorialMovimientosServiceDeLaMatriz : IHistorialMovimientosService
{
    public Task<IReadOnlyList<MovimientoHistorialItem>> ObtenerAsync(
        int negocioId, int miembroId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<MovimientoHistorialItem>>([]);
}

public sealed class AnulacionMovimientoServiceDeLaMatriz : IAnulacionMovimientoService
{
    public Task<MovimientoCredito> AnularAsync(
        AnularMovimientoRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(MovimientoCredito.Crear(
            request.NegocioId, DatosFicticiosDeLaMatriz.MiembroId, request.Hoy, DateTime.UtcNow,
            TipoMovimientoCredito.Ajuste, 100m, request.Hoy, request.UsuarioId, request.Motivo));
}

public sealed class CierreDiarioServiceDeLaMatriz : ICierreDiarioService
{
    public Task<CierreDiarioResultado> ObtenerAsync(
        int negocioId, int sucursalId, DateOnly fecha, CancellationToken cancellationToken = default) =>
        Task.FromResult(new CierreDiarioResultado(sucursalId, fecha, 0m, []));
}

public sealed class UsuarioServiceDeLaMatriz : IUsuarioService
{
    public Task<IReadOnlyList<UsuarioResultado>> ListarAsync(
        int negocioId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<UsuarioResultado>>([]);

    public Task<UsuarioResultado> CrearAsync(
        CrearUsuarioSolicitud solicitud, CancellationToken cancellationToken = default) =>
        Task.FromResult(new UsuarioResultado(
            9, solicitud.NombreCompleto, solicitud.Email, solicitud.Rol, solicitud.SucursalId, Activo: true));
}

public sealed class SucursalServiceDeLaMatriz : ISucursalService
{
    public Task<IReadOnlyList<SucursalResultado>> ListarAsync(
        int negocioId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<SucursalResultado>>([]);

    public Task<SucursalResultado> CrearAsync(
        int negocioId, string nombre, string? codigoExterno, CancellationToken cancellationToken = default) =>
        Task.FromResult(new SucursalResultado(9, nombre, codigoExterno, Activa: true));
}

public sealed class AltaMiembroServiceDeLaMatriz : IAltaMiembroService
{
    public Task<Miembro> DarDeAltaAsync(
        AltaMiembroSolicitud solicitud, CancellationToken cancellationToken = default) =>
        Task.FromResult(new Miembro
        {
            Id = DatosFicticiosDeLaMatriz.MiembroId,
            NegocioId = solicitud.NegocioId,
            Nombre = solicitud.Nombre,
            NombreNormalizado = solicitud.Nombre.ToLowerInvariant(),
            FechaAlta = solicitud.Hoy,
        });
}

public sealed class ConsentimientoTextoServiceDeLaMatriz : IConsentimientoTextoService
{
    public Task<ConsentimientoTextoResultado> ObtenerAsync(
        int negocioId, TipoConsentimiento tipo, CancellationToken cancellationToken = default) =>
        Task.FromResult(new ConsentimientoTextoResultado(tipo, "v-de-prueba", "Texto ficticio de prueba."));
}

public sealed class VinculacionMiembroServiceDeLaMatriz : IVinculacionMiembroService
{
    public Task<IReadOnlyList<MiembroSinVincularResultado>> ListarSinVincularAsync(
        int negocioId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<MiembroSinVincularResultado>>([]);

    public Task<VinculacionResultado> VincularAsync(
        VincularClienteExternoSolicitud solicitud, CancellationToken cancellationToken = default) =>
        Task.FromResult(new VinculacionResultado(
            solicitud.MiembroId, DatosFicticiosDeLaMatriz.NombreDelMiembro,
            solicitud.ClienteExternoId ?? "POS-FICTICIO-1", DateTime.UtcNow));
}

/// <summary>Answers a made-up Dueño so <c>POST /api/auth/login</c> reaches its 200 — the matrix
/// only asks whether the endpoint is reachable without a session, never whether a credential is
/// correct (that is <c>AuthServiceTests</c>' job).</summary>
public sealed class AuthServiceDeLaMatriz : IAuthService
{
    public Task<Usuario> AutenticarAsync(string email, string password, CancellationToken cancellationToken = default) =>
        Task.FromResult(Usuario.Crear(
            DatosFicticiosDeLaMatriz.NegocioId, "Dueña Ficticia De Prueba", email, "hash-ficticio",
            RolUsuario.Dueno, DateTime.UtcNow));
}

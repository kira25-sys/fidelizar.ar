using Fidelizar.Api.Security;
using Fidelizar.Application.Services;
using Fidelizar.Domain.Entities;
using Fidelizar.Domain.Exceptions;
using Fidelizar.Shared.Miembros;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CanjeRequest = Fidelizar.Shared.Movimientos.RegistrarCanjeRequest;
using CanjeResponse = Fidelizar.Shared.Movimientos.CanjeResponse;

namespace Fidelizar.Api.Controllers;

/// <summary>
/// S2, S3 and S6 (FUNCTIONAL-SPEC §4/§5/§8) plus S4's redemption endpoint. Every action here
/// talks to <c>Fidelizar.Application</c>, never to a repository (ARCHITECTURE §3).
///
/// <c>NegocioId</c> and the acting user's id always come from the authenticated principal
/// (<see cref="ClaimsPrincipalExtensions"/>), never from the URL or the body (I8, ARCHITECTURE
/// §8) — a Cajero cannot ask for another business's member by typing a different id.
///
/// The class-level policy is the floor for this controller (any signed-in staff member can
/// search and open the counter view); S6 Ficha completa raises it per-action to
/// <see cref="Policies.EncargadaOrAbove"/> — ASP.NET Core combines multiple
/// <c>[Authorize]</c> attributes with AND, so the effective requirement there is exactly
/// Encargada-or-above, never merely Cajero-or-above.
/// </summary>
[ApiController]
[Route("api/miembros")]
[Authorize(Policy = Policies.CajeroOrAbove)]
public sealed class MiembrosController(
    ISaldoService saldoService,
    ICorteService corteService,
    IMiembroBusquedaService busquedaService,
    IFichaMostradorService fichaMostradorService,
    IFichaCompletaService fichaCompletaService,
    IHistorialMovimientosService historialMovimientosService,
    IAltaMiembroService altaMiembroService,
    IConsentimientoTextoService consentimientoTextoService,
    IVinculacionMiembroService vinculacionMiembroService) : ControllerBase
{
    /// <summary>
    /// S3 Ficha del socio, the balance piece (FUNCTIONAL-SPEC §5). <see cref="ISaldoService.ObtenerSaldoAsync"/>
    /// 404s for an unknown or another-business's <paramref name="miembroId"/> — the exception
    /// propagates to <c>ExceptionHandlingMiddleware</c>, which is why this action has nothing to
    /// branch on itself.
    /// </summary>
    [HttpGet("{miembroId:int}/saldo")]
    public async Task<IActionResult> ObtenerSaldo(int miembroId, CancellationToken cancellationToken)
    {
        var negocioId = User.ObtenerNegocioId();

        var saldo = await saldoService.ObtenerSaldoAsync(negocioId, miembroId, cancellationToken);
        var corte = await corteService.ObtenerCorteVigenteAsync(negocioId, cancellationToken);

        return Ok(new SaldoMiembroResponse(miembroId, saldo, corte.Fecha));
    }

    /// <summary>
    /// S2 Buscar socio (FUNCTIONAL-SPEC §4). "Buscar, nunca listar" is enforced by
    /// <see cref="IMiembroBusquedaService"/> itself — <paramref name="q"/> too short throws
    /// <c>ValidationException</c> (400), never an empty-string fallback that would return
    /// everyone.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> BuscarMiembros([FromQuery] string? q, CancellationToken cancellationToken)
    {
        var negocioId = User.ObtenerNegocioId();

        // No query at all is exactly as short as a query below the 3-character floor —
        // IMiembroBusquedaService rejects both the same way (400, never a listing).
        var resultados = await busquedaService.BuscarAsync(negocioId, q ?? string.Empty, cancellationToken);
        var corte = await corteService.ObtenerCorteVigenteAsync(negocioId, cancellationToken);

        var respuesta = resultados
            .Select(r => new MiembroResumen(r.Id, r.Nombre, r.NumeroSocio, r.Saldo, r.FechaAlta, corte.Fecha))
            .ToList();

        return Ok(respuesta);
    }

    /// <summary>S3 Ficha del socio, the full counter view (FUNCTIONAL-SPEC §5) — never phone or
    /// DNI, regardless of role: that is S6's gate alone.</summary>
    [HttpGet("{miembroId:int}/ficha-mostrador")]
    public async Task<IActionResult> FichaMostrador(int miembroId, CancellationToken cancellationToken)
    {
        var negocioId = User.ObtenerNegocioId();
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);

        var ficha = await fichaMostradorService.ObtenerAsync(negocioId, miembroId, hoy, cancellationToken);

        var respuesta = new FichaMostradorResponse(
            ficha.Id,
            ficha.Nombre,
            ficha.NumeroSocio,
            ficha.Saldo,
            ficha.CorteFecha,
            ficha.Alertas.Select(a => new AlertaMiembro(a.Tipo.ToString(), a.Texto)).ToList());

        return Ok(respuesta);
    }

    /// <summary>
    /// S6 Ficha completa (FUNCTIONAL-SPEC §8) — the only place phone and DNI ever leave the
    /// server, and only for Encargada/Dueño. Every read is audited
    /// (<c>IFichaCompletaService</c> writes the <c>RegistroAuditoria</c> row before returning).
    /// </summary>
    [HttpGet("{miembroId:int}/completo")]
    [Authorize(Policy = Policies.EncargadaOrAbove)]
    public async Task<IActionResult> FichaCompleta(int miembroId, CancellationToken cancellationToken)
    {
        var negocioId = User.ObtenerNegocioId();
        var usuarioId = User.ObtenerUsuarioId();

        var ficha = await fichaCompletaService.ObtenerAsync(negocioId, miembroId, usuarioId, cancellationToken);

        var respuesta = new FichaCompletaResponse(
            ficha.Id,
            ficha.Nombre,
            ficha.NumeroSocio,
            ficha.ClienteExternoId,
            ficha.Telefono,
            ficha.Dni,
            ficha.FechaNacimiento is { } f ? $"{f.Day:D2}/{f.Month:D2}" : null,
            ficha.SucursalId,
            ficha.Activo);

        return Ok(respuesta);
    }

    /// <summary>S7 Historial de movimientos (FUNCTIONAL-SPEC §screen-map) — Encargada/Dueño
    /// only, newest first.</summary>
    [HttpGet("{miembroId:int}/movimientos")]
    [Authorize(Policy = Policies.EncargadaOrAbove)]
    public async Task<IActionResult> HistorialMovimientos(int miembroId, CancellationToken cancellationToken)
    {
        var negocioId = User.ObtenerNegocioId();

        var historial = await historialMovimientosService.ObtenerAsync(negocioId, miembroId, cancellationToken);

        var respuesta = historial
            .Select(m => new Fidelizar.Shared.Movimientos.MovimientoResponse(
                m.Id, m.Tipo.ToString(), m.Monto, m.FechaEfectiva, m.RegistradoEn, m.Motivo, m.SaldoResultante, m.UsuarioNombre))
            .ToList();

        return Ok(respuesta);
    }

    /// <summary>
    /// S4 Registrar canje (FUNCTIONAL-SPEC §6). Writes a <c>Canje</c> movement through
    /// <see cref="ISaldoService.RegistrarCanjeAsync"/>, which is the only place that enforces
    /// I6/RN-24 (never above the balance) and RN-25 (blocked while under review).
    /// </summary>
    [HttpPost("{miembroId:int}/canjes")]
    [AntiforgeryTokenRequired]
    public async Task<IActionResult> RegistrarCanje(
        int miembroId, [FromBody] CanjeRequest request, CancellationToken cancellationToken)
    {
        var negocioId = User.ObtenerNegocioId();
        var usuarioId = User.ObtenerUsuarioId();
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);

        var movimiento = await saldoService.RegistrarCanjeAsync(
            new RegistrarCanjeRequest(
                negocioId, miembroId, request.Monto, request.Motivo, usuarioId, request.FechaEfectiva, hoy,
                request.ClaveIdempotencia),
            cancellationToken);

        return Ok(new CanjeResponse(movimiento.Id, -movimiento.Monto, movimiento.FechaEfectiva, movimiento.SaldoResultante));
    }

    /// <summary>
    /// S5 Alta de socio (FUNCTIONAL-SPEC §7). <see cref="IAltaMiembroService.DarDeAltaAsync"/>
    /// writes the <c>Miembro</c> and the mandatory <c>DatosPersonales</c> <c>Consentimiento</c> in
    /// one transaction (I10) — if either fails, neither lands. <c>Saldo</c> is always 0 here: a
    /// brand-new member has no ledger rows yet, so there is nothing for
    /// <see cref="ISaldoService"/> to sum.
    /// </summary>
    [HttpPost]
    [AntiforgeryTokenRequired]
    public async Task<IActionResult> RegistrarAlta(
        [FromBody] AltaMiembroRequest request, CancellationToken cancellationToken)
    {
        var negocioId = User.ObtenerNegocioId();
        var usuarioId = User.ObtenerUsuarioId();
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);

        var miembro = await altaMiembroService.DarDeAltaAsync(
            new AltaMiembroSolicitud(
                negocioId,
                request.Nombre,
                request.ClienteExternoId,
                request.Telefono,
                request.Dni,
                request.FechaNacimientoDia,
                request.FechaNacimientoMes,
                request.SucursalId,
                request.ConsentimientoDatosPersonales,
                request.ConsentimientoDatosSensibles,
                usuarioId,
                hoy),
            cancellationToken);

        var corte = await corteService.ObtenerCorteVigenteAsync(negocioId, cancellationToken);
        var respuesta = new MiembroResumen(miembro.Id, miembro.Nombre, miembro.NumeroSocio, Saldo: 0m, miembro.FechaAlta, corte.Fecha);

        return CreatedAtAction(nameof(FichaMostrador), new { miembroId = miembro.Id }, respuesta);
    }

    /// <summary>
    /// F1-14 "Socios sin vincular" (ROADMAP) — Encargada/Dueño only. Not the "all members"
    /// listing FUNCTIONAL-SPEC §4 forbids: it is that listing's complement, holds only the
    /// members that accrue nothing yet, and carries no phone or DNI.
    /// </summary>
    [HttpGet("sin-vincular")]
    [Authorize(Policy = Policies.EncargadaOrAbove)]
    public async Task<IActionResult> ListarSinVincular(CancellationToken cancellationToken)
    {
        var negocioId = User.ObtenerNegocioId();

        var miembros = await vinculacionMiembroService.ListarSinVincularAsync(negocioId, cancellationToken);

        var respuesta = miembros
            .Select(m => new MiembroSinVincularResponse(m.Id, m.Nombre, m.NumeroSocio, m.FechaAlta, m.SucursalId))
            .ToList();

        return Ok(respuesta);
    }

    /// <summary>
    /// F1-14 — links one member to its POS id, so its future sales can be credited.
    /// <see cref="IVinculacionMiembroService.VincularAsync"/> owns every rejection: 404 for an
    /// unknown member or one of another business (I8), 409 for an already-linked member or an id
    /// already in use.
    /// </summary>
    [HttpPost("{miembroId:int}/vinculacion")]
    [Authorize(Policy = Policies.EncargadaOrAbove)]
    [AntiforgeryTokenRequired]
    public async Task<IActionResult> VincularClienteExterno(
        int miembroId, [FromBody] VincularClienteExternoRequest request, CancellationToken cancellationToken)
    {
        var negocioId = User.ObtenerNegocioId();
        var usuarioId = User.ObtenerUsuarioId();

        var resultado = await vinculacionMiembroService.VincularAsync(
            new VincularClienteExternoSolicitud(negocioId, miembroId, request.ClienteExternoId, usuarioId),
            cancellationToken);

        return Ok(new VinculacionClienteExternoResponse(
            resultado.MiembroId, resultado.Nombre, resultado.ClienteExternoId, resultado.VinculadoEn));
    }

    /// <summary>
    /// The resolved consent wording S5 shows the member before either checkbox — this business's
    /// own name/CUIT/(domicilio) already substituted in
    /// (<see cref="Fidelizar.Domain.Consentimientos.TextosConsentimiento"/>), never a literal
    /// shipped to the browser. Only <c>DatosPersonales</c> and <c>DatosSensibles</c> have an
    /// approved text (README open decision #3, resolved 2026-08-19).
    /// </summary>
    [HttpGet("consentimiento-texto/{tipo}")]
    public async Task<IActionResult> ObtenerConsentimientoTexto(string tipo, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<TipoConsentimiento>(tipo, ignoreCase: true, out var tipoParseado))
        {
            throw new ValidationException($"Tipo de consentimiento inválido: '{tipo}'.", "TIPO_CONSENTIMIENTO_INVALIDO");
        }

        var resultado = await consentimientoTextoService.ObtenerAsync(
            User.ObtenerNegocioId(), tipoParseado, cancellationToken);

        return Ok(new ConsentimientoTextoResponse(resultado.Tipo.ToString(), resultado.VersionTexto, resultado.Texto));
    }
}

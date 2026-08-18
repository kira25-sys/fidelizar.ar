using Fidelizar.Api.Security;
using Fidelizar.Application.Services;
using Fidelizar.Shared.Miembros;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CanjeRequest = Fidelizar.Shared.Movimientos.RegistrarCanjeRequest;
using CanjeResponse = Fidelizar.Shared.Movimientos.CanjeResponse;

namespace Fidelizar.Api.Controllers;

/// <summary>
/// F1-04b's REST contract, the subset actually backed by an existing Application service today:
/// <see cref="ISaldoService"/> (balance query and redemption) and <see cref="ICorteService"/>
/// (the program's cutoff). Everything else the phase-1 screens need — search, full record,
/// history, void, daily close, users — is documented in docs/REST-CONTRACT-F1.md and the OpenAPI
/// spec under docs/api/, not implemented here: there is no Application service to call yet, and
/// an endpoint that fakes one is worse than no endpoint (task instructions, F1-04b).
///
/// <c>NegocioId</c> and the acting user's id always come from the authenticated principal
/// (<see cref="ClaimsPrincipalExtensions"/>), never from the URL or the body (I8, ARCHITECTURE
/// §8) — a Cajero cannot ask for another business's member by typing a different id.
/// </summary>
[ApiController]
[Route("api/miembros")]
[Authorize(Policy = Policies.CajeroOrAbove)]
public sealed class MiembrosController(ISaldoService saldoService, ICorteService corteService) : ControllerBase
{
    /// <summary>
    /// S3 Ficha del socio, the balance piece (FUNCTIONAL-SPEC §5). Known gap: with no Miembro
    /// lookup service yet, this cannot tell an unknown <paramref name="miembroId"/> apart from a
    /// member whose balance happens to be zero — both return 0 (see docs/REST-CONTRACT-F1.md).
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
                negocioId, miembroId, request.Monto, request.Motivo, usuarioId, request.FechaEfectiva, hoy),
            cancellationToken);

        return Ok(new CanjeResponse(movimiento.Id, -movimiento.Monto, movimiento.FechaEfectiva, movimiento.SaldoResultante));
    }
}

using Fidelizar.Api.Security;
using Fidelizar.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AnularRequest = Fidelizar.Shared.Movimientos.AnularMovimientoRequest;
using MovimientoResponse = Fidelizar.Shared.Movimientos.MovimientoResponse;

namespace Fidelizar.Api.Controllers;

/// <summary>
/// S8 Anular movimiento (FUNCTIONAL-SPEC §8) — Encargada/Dueño only. This is invariant I1
/// surfaced in the UI: there is no edit and no delete anywhere in this product, only a new
/// <c>Ajuste</c>. <c>NegocioId</c> and the acting user always come from the JWT (I8, ARCHITECTURE
/// §8), never from the body.
/// </summary>
[ApiController]
[Route("api/movimientos")]
[Authorize(Policy = Policies.EncargadaOrAbove)]
public sealed class MovimientosController(IAnulacionMovimientoService anulacionMovimientoService) : ControllerBase
{
    [HttpPost("{movimientoId:long}/anular")]
    [AntiforgeryTokenRequired]
    public async Task<IActionResult> Anular(
        long movimientoId, [FromBody] AnularRequest request, CancellationToken cancellationToken)
    {
        var negocioId = User.ObtenerNegocioId();
        var usuarioId = User.ObtenerUsuarioId();
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);

        var ajuste = await anulacionMovimientoService.AnularAsync(
            new AnularMovimientoRequest(negocioId, movimientoId, request.Motivo, usuarioId, hoy),
            cancellationToken);

        var respuesta = new MovimientoResponse(
            ajuste.Id, ajuste.Tipo.ToString(), ajuste.Monto, ajuste.FechaEfectiva, ajuste.RegistradoEn,
            ajuste.Motivo, ajuste.SaldoResultante, null);

        return Ok(respuesta);
    }
}

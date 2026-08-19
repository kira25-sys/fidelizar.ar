using Fidelizar.Api.Security;
using Fidelizar.Application.Services;
using Fidelizar.Shared.Sucursales;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fidelizar.Api.Controllers;

/// <summary>
/// S9 Cierre diario de canjes and S10 Sucursales. Two different policies live on this controller
/// on purpose (FUNCTIONAL-SPEC §screen-map: S9 is Encargada/Dueño, S10 is Dueño only) — every
/// action below declares its own <c>[Authorize]</c> explicitly rather than leaning on a shared
/// class-level policy, so nothing here can end up looser than intended by accident.
/// </summary>
[ApiController]
[Route("api/sucursales")]
public sealed class SucursalesController(
    ICierreDiarioService cierreDiarioService, ISucursalService sucursalService) : ControllerBase
{
    /// <summary>
    /// S9 (FUNCTIONAL-SPEC §9). The branch axis of ARCHITECTURE §8/<see cref="ClaimsPrincipalExtensions"/>:
    /// an Encargada tied to one branch may only ask for that branch's own close, never another's —
    /// Dueño has no branch claim and can ask for any.
    /// </summary>
    [HttpGet("{sucursalId:int}/cierre-diario")]
    [Authorize(Policy = Policies.EncargadaOrAbove)]
    public async Task<IActionResult> CierreDiario(
        int sucursalId, [FromQuery] DateOnly fecha, CancellationToken cancellationToken)
    {
        if (!User.PuedeOperarSucursal(sucursalId))
        {
            return Forbid();
        }

        var negocioId = User.ObtenerNegocioId();

        var cierre = await cierreDiarioService.ObtenerAsync(negocioId, sucursalId, fecha, cancellationToken);

        var respuesta = new CierreDiarioResponse(
            cierre.SucursalId,
            cierre.Fecha,
            cierre.TotalCanjeado,
            cierre.Movimientos
                .Select(m => new CierreDiarioMovimiento(m.MiembroNombre, m.Monto, m.CajeroNombre, m.Hora, m.Motivo))
                .ToList());

        return Ok(respuesta);
    }

    /// <summary>S10 Sucursales — list (Dueño only).</summary>
    [HttpGet]
    [Authorize(Policy = Policies.DuenoOnly)]
    public async Task<IActionResult> Listar(CancellationToken cancellationToken)
    {
        var negocioId = User.ObtenerNegocioId();

        var sucursales = await sucursalService.ListarAsync(negocioId, cancellationToken);

        var respuesta = sucursales
            .Select(s => new SucursalResponse(s.Id, s.Nombre, s.CodigoExterno, s.Activa))
            .ToList();

        return Ok(respuesta);
    }

    /// <summary>S10 Sucursales — create (Dueño only).</summary>
    [HttpPost]
    [Authorize(Policy = Policies.DuenoOnly)]
    [AntiforgeryTokenRequired]
    public async Task<IActionResult> Crear([FromBody] CrearSucursalRequest request, CancellationToken cancellationToken)
    {
        var negocioId = User.ObtenerNegocioId();

        var creada = await sucursalService.CrearAsync(negocioId, request.Nombre, request.CodigoExterno, cancellationToken);

        var respuesta = new SucursalResponse(creada.Id, creada.Nombre, creada.CodigoExterno, creada.Activa);

        return CreatedAtAction(nameof(Listar), respuesta);
    }
}

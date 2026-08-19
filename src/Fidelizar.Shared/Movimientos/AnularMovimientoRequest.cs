using System.ComponentModel.DataAnnotations;

namespace Fidelizar.Shared.Movimientos;

/// <summary>S8 Anular movimiento request body. <c>Motivo</c> is mandatory client-side as a fast
/// hint; the real enforcement is <c>MovimientoCredito.Crear</c>'s own check on every
/// <c>Ajuste</c> (I3), server-side, regardless of this attribute.</summary>
public sealed record AnularMovimientoRequest(
    [Required(ErrorMessage = "El motivo es obligatorio.")]
    string Motivo);

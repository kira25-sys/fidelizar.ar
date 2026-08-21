using System.ComponentModel.DataAnnotations;

namespace Fidelizar.Shared.Miembros;

/// <summary>
/// F1-14 — the POS customer id to link a member to. The attribute is only a fast Spanish message
/// for the Encargada; the real rejections (<c>CLIENTE_EXTERNO_ID_REQUERIDO</c>,
/// <c>MIEMBRO_YA_VINCULADO</c>, <c>CLIENTE_EXTERNO_ID_DUPLICADO</c>) are server-side in
/// <c>VinculacionMiembroService</c> (ARCHITECTURE §3).
/// </summary>
public sealed record VincularClienteExternoRequest(
    [Required(ErrorMessage = "El id de cliente del POS es obligatorio.")]
    string ClienteExternoId);

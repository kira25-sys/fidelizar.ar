using System.ComponentModel.DataAnnotations;

namespace Fidelizar.Shared.Miembros;

/// <summary>
/// S5 Alta de socio request body (FUNCTIONAL-SPEC §7) — consent is part of the form, not an
/// afterthought (I10). No data-annotation short-circuits <c>ConsentimientoDatosPersonales ==
/// false</c> here (a bare boolean has no client-side "must be true" attribute worth the
/// complexity) — the real, server-side rejection is
/// <c>AltaMiembroService.DarDeAltaAsync</c>'s <c>CONSENTIMIENTO_DATOS_PERSONALES_REQUERIDO</c>,
/// which runs before anything is written, and which the client cannot skip by omitting the check.
/// </summary>
public sealed record AltaMiembroRequest(
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    string Nombre,
    string? ClienteExternoId,
    string? Telefono,
    string? Dni,
    [Range(1, 31, ErrorMessage = "El día tiene que estar entre 1 y 31.")]
    int? FechaNacimientoDia,
    [Range(1, 12, ErrorMessage = "El mes tiene que estar entre 1 y 12.")]
    int? FechaNacimientoMes,
    int? SucursalId,
    bool ConsentimientoDatosPersonales,
    bool ConsentimientoDatosSensibles);

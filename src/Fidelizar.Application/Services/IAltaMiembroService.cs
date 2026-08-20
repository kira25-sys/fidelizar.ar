using Fidelizar.Domain.Entities;

namespace Fidelizar.Application.Services;

/// <summary>
/// What the caller supplies for S5 Alta de socio (FUNCTIONAL-SPEC §7). Distinct from
/// <c>Fidelizar.Shared.Miembros.AltaMiembroRequest</c> — the same pattern
/// <c>RegistrarCanjeRequest</c> already established (<c>Fidelizar.Application</c> cannot
/// reference <c>Fidelizar.Shared</c>, ARCHITECTURE §3).
/// </summary>
/// <param name="NegocioId">Required, not a convention (I8).</param>
/// <param name="Nombre">The only field the form truly requires besides consent.</param>
/// <param name="ClienteExternoId">Almost always null at counter registration — the id is born in
/// the POS. The member is created unlinked and accrues nothing until linked (DATA-MODEL §3).</param>
/// <param name="Telefono">Informational.</param>
/// <param name="Dni">Informational.</param>
/// <param name="FechaNacimientoDia">Day only — RN-11 ignores the year, so the form never asks for
/// one.</param>
/// <param name="FechaNacimientoMes">Month only.</param>
/// <param name="SucursalId">Organisational only (RN-07); validated to exist when supplied.</param>
/// <param name="ConsentimientoDatosPersonales">
/// Must be <c>true</c> — the checkbox FUNCTIONAL-SPEC §7 marks mandatory. Alta is rejected
/// without it, before anything is written (I10).
/// </param>
/// <param name="ConsentimientoDatosSensibles">
/// Optional (FUNCTIONAL-SPEC §7): the text itself says membership does not require it. Alta
/// succeeds either way; only the consent row differs.
/// </param>
/// <param name="UsuarioId">Who is registering the member. Null only for a self-service channel
/// this product does not have yet.</param>
/// <param name="Hoy">Today's date — <c>Miembro.FechaAlta</c> and both consent rows' <c>OcurridoEn</c>
/// are stamped from it. Passed in rather than read from the clock so this stays testable without
/// one (the same pattern <c>RegistrarCanjeRequest.Hoy</c> already uses).</param>
public sealed record AltaMiembroSolicitud(
    int NegocioId,
    string Nombre,
    string? ClienteExternoId,
    string? Telefono,
    string? Dni,
    int? FechaNacimientoDia,
    int? FechaNacimientoMes,
    int? SucursalId,
    bool ConsentimientoDatosPersonales,
    bool ConsentimientoDatosSensibles,
    int? UsuarioId,
    DateOnly Hoy);

public interface IAltaMiembroService
{
    /// <summary>
    /// Creates the <see cref="Miembro"/> and, in the same transaction, the mandatory
    /// <c>DatosPersonales</c> <see cref="Consentimiento"/> — and the optional
    /// <c>DatosSensibles</c> one when requested. Either both writes land or neither does (I10):
    /// a member without a recorded consent is exactly the legal hole phase 1 exists to close.
    /// </summary>
    /// <exception cref="Fidelizar.Domain.Exceptions.ValidationException">
    /// Empty <c>Nombre</c> (<c>NOMBRE_REQUERIDO</c>); <c>ConsentimientoDatosPersonales</c> is
    /// <c>false</c> (<c>CONSENTIMIENTO_DATOS_PERSONALES_REQUERIDO</c> — nothing is written, not
    /// even the <c>Miembro</c>); exactly one of <c>FechaNacimientoDia</c>/<c>FechaNacimientoMes</c>
    /// supplied (<c>FECHA_NACIMIENTO_INCOMPLETA</c>); <c>SucursalId</c> supplied but does not exist
    /// (<c>SUCURSAL_INEXISTENTE</c>).
    /// </exception>
    /// <exception cref="Fidelizar.Domain.Exceptions.ConflictException">
    /// <c>ClienteExternoId</c> supplied and already linked to another member of this business
    /// (<c>CLIENTE_EXTERNO_ID_DUPLICADO</c>).
    /// </exception>
    Task<Miembro> DarDeAltaAsync(AltaMiembroSolicitud solicitud, CancellationToken cancellationToken = default);
}

using Fidelizar.Domain.Consentimientos;
using Fidelizar.Domain.Entities;
using Fidelizar.Domain.Exceptions;
using Fidelizar.Domain.Persistence;
using Fidelizar.Domain.Repositories;
using Fidelizar.Domain.Texto;

namespace Fidelizar.Application.Services;

/// <summary>
/// S5 Alta de socio (FUNCTIONAL-SPEC §7). Composes <see cref="IMiembroRepository.AddAsync"/> and
/// <see cref="IConsentimientoService.OtorgarAsync"/> — reused, not duplicated, exactly as F1-08
/// built them — inside one <see cref="IUnitOfWork"/> transaction, because I10 requires that a
/// member without its mandatory consent never exists even for an instant longer than a failed
/// request. Knows nothing about HTTP or EF (ARCHITECTURE §3).
/// </summary>
public sealed class AltaMiembroService(
    IMiembroRepository miembroRepository,
    ISucursalRepository sucursalRepository,
    IConsentimientoService consentimientoService,
    IUnitOfWork unitOfWork) : IAltaMiembroService
{
    public async Task<Miembro> DarDeAltaAsync(
        AltaMiembroSolicitud solicitud, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(solicitud.Nombre))
        {
            throw new ValidationException("El nombre es obligatorio.", "NOMBRE_REQUERIDO");
        }

        // I10 / FUNCTIONAL-SPEC §7: the mandatory checkbox. Checked before anything is written —
        // an alta that fails this never touches the database at all, let alone leaves a Miembro
        // behind with no consent on record.
        if (!solicitud.ConsentimientoDatosPersonales)
        {
            throw new ValidationException(
                "El consentimiento de datos personales es obligatorio para dar de alta un socio.",
                "CONSENTIMIENTO_DATOS_PERSONALES_REQUERIDO");
        }

        var fechaNacimiento = ResolverFechaNacimiento(solicitud.FechaNacimientoDia, solicitud.FechaNacimientoMes);

        if (solicitud.SucursalId is { } sucursalId
            && await sucursalRepository.GetByIdAsync(solicitud.NegocioId, sucursalId, cancellationToken) is null)
        {
            throw new ValidationException(
                $"La sucursal {sucursalId} no existe en este negocio.", "SUCURSAL_INEXISTENTE");
        }

        if (solicitud.ClienteExternoId is { } clienteExternoId
            && await miembroRepository.GetByClienteExternoIdAsync(solicitud.NegocioId, clienteExternoId, cancellationToken) is not null)
        {
            throw new ConflictException(
                $"Ya hay un socio vinculado al id de POS '{clienteExternoId}' en este negocio.",
                "CLIENTE_EXTERNO_ID_DUPLICADO");
        }

        var miembro = new Miembro
        {
            NegocioId = solicitud.NegocioId,
            ClienteExternoId = solicitud.ClienteExternoId,
            Nombre = solicitud.Nombre,
            NombreNormalizado = VipNombres.Normalizar(solicitud.Nombre),
            Telefono = solicitud.Telefono,
            Dni = solicitud.Dni,
            FechaNacimiento = fechaNacimiento,
            SucursalId = solicitud.SucursalId,
            FechaAlta = solicitud.Hoy,
            Activo = true,
            ActualizadoEn = DateTime.UtcNow,
        };

        await unitOfWork.EjecutarEnTransaccionAsync(async ct =>
        {
            miembro = await miembroRepository.AddAsync(miembro, ct);

            var ocurridoEn = DateTime.UtcNow;

            await consentimientoService.OtorgarAsync(
                new OtorgarConsentimientoRequest(
                    solicitud.NegocioId,
                    miembro.Id,
                    TipoConsentimiento.DatosPersonales,
                    TextosConsentimiento.DatosPersonalesVersion,
                    CanalConsentimiento.Mostrador,
                    ocurridoEn,
                    solicitud.UsuarioId),
                ct);

            // Optional (FUNCTIONAL-SPEC §7): the DatosSensibles text itself says membership does
            // not require it. No row at all when it was not offered — "no row" and "declined" are
            // different facts, and nothing here invents a decision the cashier was never asked.
            if (solicitud.ConsentimientoDatosSensibles)
            {
                await consentimientoService.OtorgarAsync(
                    new OtorgarConsentimientoRequest(
                        solicitud.NegocioId,
                        miembro.Id,
                        TipoConsentimiento.DatosSensibles,
                        TextosConsentimiento.DatosSensiblesVersion,
                        CanalConsentimiento.Mostrador,
                        ocurridoEn,
                        solicitud.UsuarioId),
                    ct);
            }
        }, cancellationToken);

        return miembro;
    }

    /// <summary>
    /// RN-11 / DATA-MODEL §3: only day and month matter, the year is ignored even when present —
    /// so the form never asks for one and this manufactures a placeholder year to satisfy
    /// <c>DateOnly</c>'s shape. 2000 is a leap year on purpose: a Feb 29 birthday needs no special
    /// handling here (<c>AvisoCumpleanos</c> already handles the leap-year fallback for every
    /// other year when the notice is actually computed).
    /// </summary>
    private static DateOnly? ResolverFechaNacimiento(int? dia, int? mes)
    {
        if (dia is null && mes is null)
        {
            return null;
        }

        if (dia is null || mes is null)
        {
            throw new ValidationException(
                "Si se carga la fecha de nacimiento, hacen falta tanto el día como el mes.",
                "FECHA_NACIMIENTO_INCOMPLETA");
        }

        try
        {
            return new DateOnly(2000, mes.Value, dia.Value);
        }
        catch (ArgumentOutOfRangeException)
        {
            throw new ValidationException(
                "La fecha de nacimiento no es válida.", "FECHA_NACIMIENTO_INVALIDA");
        }
    }
}

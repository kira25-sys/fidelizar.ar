using Fidelizar.Domain.Entities;
using Fidelizar.Domain.Exceptions;
using Fidelizar.Domain.Persistence;
using Fidelizar.Domain.Repositories;

namespace Fidelizar.Application.Services;

/// <summary>See <see cref="IVinculacionMiembroService"/>.</summary>
public sealed class VinculacionMiembroService(
    IMiembroRepository miembroRepository,
    IRegistroAuditoriaRepository registroAuditoriaRepository,
    IUnitOfWork unitOfWork) : IVinculacionMiembroService
{
    public async Task<IReadOnlyList<MiembroSinVincularResultado>> ListarSinVincularAsync(
        int negocioId, CancellationToken cancellationToken = default)
    {
        var miembros = await miembroRepository.ListarSinVincularAsync(negocioId, cancellationToken);

        return miembros
            .Select(m => new MiembroSinVincularResultado(m.Id, m.Nombre, m.NumeroSocio, m.FechaAlta, m.SucursalId))
            .ToList();
    }

    public async Task<VinculacionResultado> VincularAsync(
        VincularClienteExternoSolicitud solicitud, CancellationToken cancellationToken = default)
    {
        var clienteExternoId = solicitud.ClienteExternoId?.Trim();

        if (string.IsNullOrEmpty(clienteExternoId))
        {
            throw new ValidationException(
                "El id de cliente del POS es obligatorio para vincular un socio.",
                "CLIENTE_EXTERNO_ID_REQUERIDO");
        }

        // I8: a member of another Negocio answers exactly like one that does not exist — the
        // response never reveals that the id is real somewhere else.
        var miembro = await miembroRepository.GetByIdAsync(solicitud.NegocioId, solicitud.MiembroId, cancellationToken)
            ?? throw new EntityNotFoundException($"Miembro {solicitud.MiembroId}");

        if (miembro.ClienteExternoId is not null)
        {
            throw new ConflictException(
                $"El socio {miembro.Id} ya está vinculado a un id de POS.", "MIEMBRO_YA_VINCULADO");
        }

        // Checked before the write for a clear message; the partial unique index on
        // (NegocioId, ClienteExternoId) is what actually stops a concurrent second linking
        // (DATA-MODEL §3), and MiembroRepository translates it into this same error code.
        if (await miembroRepository.GetByClienteExternoIdAsync(
                solicitud.NegocioId, clienteExternoId, cancellationToken) is not null)
        {
            throw new ConflictException(
                $"Ya hay un socio vinculado al id de POS '{clienteExternoId}' en este negocio.",
                "CLIENTE_EXTERNO_ID_DUPLICADO");
        }

        var ahoraUtc = DateTime.UtcNow;

        await unitOfWork.EjecutarEnTransaccionAsync(async ct =>
        {
            var vinculado = await miembroRepository.VincularClienteExternoAsync(
                solicitud.NegocioId, miembro.Id, clienteExternoId, ahoraUtc, ct);

            // Zero rows with the member already read above means another request linked it first.
            if (!vinculado)
            {
                throw new ConflictException(
                    $"El socio {miembro.Id} ya está vinculado a un id de POS.", "MIEMBRO_YA_VINCULADO");
            }

            // DATA-MODEL §2: linking decides whose future purchases accrue, so who did it is
            // recorded. Same transaction as the write — an unaudited link never exists.
            var registro = RegistroAuditoria.Registrar(
                solicitud.NegocioId,
                solicitud.UsuarioId,
                "VincularClienteExterno",
                ahoraUtc,
                entidadTipo: nameof(Miembro),
                entidadId: miembro.Id);
            await registroAuditoriaRepository.RegistrarAsync(registro, ct);
        }, cancellationToken);

        return new VinculacionResultado(miembro.Id, miembro.Nombre, clienteExternoId, ahoraUtc);
    }
}

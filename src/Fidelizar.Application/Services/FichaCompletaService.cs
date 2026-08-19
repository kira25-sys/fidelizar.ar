using Fidelizar.Domain.Entities;
using Fidelizar.Domain.Exceptions;
using Fidelizar.Domain.Repositories;

namespace Fidelizar.Application.Services;

/// <summary>See <see cref="IFichaCompletaService"/>.</summary>
public sealed class FichaCompletaService(
    IMiembroRepository miembroRepository,
    IRegistroAuditoriaRepository registroAuditoriaRepository) : IFichaCompletaService
{
    public async Task<FichaCompletaResultado> ObtenerAsync(
        int negocioId, int miembroId, int usuarioIdQueLee, CancellationToken cancellationToken = default)
    {
        var miembro = await miembroRepository.GetByIdAsync(negocioId, miembroId, cancellationToken)
            ?? throw new EntityNotFoundException($"Miembro {miembroId}");

        // DATA-MODEL §2: every read of sensitive data is audited, not only Soporte access.
        // Written on every call, not only the first — an Encargada re-opening the same ficha
        // twice in a shift is two audited reads, not one.
        var registro = RegistroAuditoria.Registrar(
            negocioId,
            usuarioIdQueLee,
            "VerFichaCompleta",
            DateTime.UtcNow,
            entidadTipo: nameof(Miembro),
            entidadId: miembro.Id);
        await registroAuditoriaRepository.RegistrarAsync(registro, cancellationToken);

        return new FichaCompletaResultado(
            miembro.Id,
            miembro.Nombre,
            miembro.NumeroSocio,
            miembro.ClienteExternoId,
            miembro.Telefono,
            miembro.Dni,
            miembro.FechaNacimiento,
            miembro.SucursalId,
            miembro.Activo);
    }
}

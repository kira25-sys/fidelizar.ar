namespace Fidelizar.Application.Services;

/// <summary>S6 Ficha completa (Encargada/Dueño only, FUNCTIONAL-SPEC §5/§8's privacy split). The
/// one place phone and DNI are ever returned — <see cref="IFichaMostradorService"/> never carries
/// them.</summary>
public sealed record FichaCompletaResultado(
    int Id,
    string Nombre,
    string? NumeroSocio,
    string? ClienteExternoId,
    string? Telefono,
    string? Dni,
    DateOnly? FechaNacimiento,
    int? SucursalId,
    bool Activo);

/// <summary>
/// S6's full record read. Every call is audited (DATA-MODEL §2): a business reading a member's
/// phone or DNI leaves a trace of who read it and when, the same way support access does.
/// </summary>
public interface IFichaCompletaService
{
    /// <exception cref="Fidelizar.Domain.Exceptions.EntityNotFoundException">
    /// No member with <paramref name="miembroId"/> exists for this business.
    /// </exception>
    Task<FichaCompletaResultado> ObtenerAsync(
        int negocioId, int miembroId, int usuarioIdQueLee, CancellationToken cancellationToken = default);
}

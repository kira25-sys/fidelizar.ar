namespace Fidelizar.Application.Services;

public sealed record SucursalResultado(int Id, string Nombre, string? CodigoExterno, bool Activa);

/// <summary>S10 Sucursales (Dueño only). Nothing existed for this before this task —
/// <see cref="Fidelizar.Domain.Repositories.ISucursalRepository"/> is new alongside it.</summary>
public interface ISucursalService
{
    Task<IReadOnlyList<SucursalResultado>> ListarAsync(int negocioId, CancellationToken cancellationToken = default);

    Task<SucursalResultado> CrearAsync(
        int negocioId, string nombre, string? codigoExterno, CancellationToken cancellationToken = default);
}

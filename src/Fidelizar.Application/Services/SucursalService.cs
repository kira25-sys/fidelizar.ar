using Fidelizar.Domain.Entities;
using Fidelizar.Domain.Repositories;

namespace Fidelizar.Application.Services;

/// <summary>See <see cref="ISucursalService"/>.</summary>
public sealed class SucursalService(ISucursalRepository sucursalRepository) : ISucursalService
{
    public async Task<IReadOnlyList<SucursalResultado>> ListarAsync(
        int negocioId, CancellationToken cancellationToken = default)
    {
        var sucursales = await sucursalRepository.ListarAsync(negocioId, cancellationToken);
        return sucursales.Select(AResultado).ToList();
    }

    public async Task<SucursalResultado> CrearAsync(
        int negocioId, string nombre, string? codigoExterno, CancellationToken cancellationToken = default)
    {
        var sucursal = Sucursal.Crear(negocioId, nombre, codigoExterno);
        var creada = await sucursalRepository.AddAsync(sucursal, cancellationToken);
        return AResultado(creada);
    }

    private static SucursalResultado AResultado(Sucursal sucursal) =>
        new(sucursal.Id, sucursal.Nombre, sucursal.CodigoExterno, sucursal.Activa);
}

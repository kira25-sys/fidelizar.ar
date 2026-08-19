using System.Reflection;
using Fidelizar.Domain.Entities;
using Fidelizar.Domain.Repositories;

namespace Fidelizar.Application.Tests.Fakes;

/// <summary>In-memory stand-in for <see cref="ISucursalRepository"/> (ARCHITECTURE §11).</summary>
public sealed class FakeSucursalRepository : ISucursalRepository
{
    private static readonly PropertyInfo IdProperty = typeof(Sucursal).GetProperty(nameof(Sucursal.Id))!;

    private readonly List<Sucursal> _sucursales = [];
    private int _nextId = 1;

    public Sucursal Sembrar(int negocioId, string nombre = "Sucursal De Prueba", string? codigoExterno = null)
    {
        var sucursal = Sucursal.Crear(negocioId, nombre, codigoExterno);
        IdProperty.SetValue(sucursal, _nextId++);
        _sucursales.Add(sucursal);
        return sucursal;
    }

    public Task<Sucursal?> GetByIdAsync(int negocioId, int id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_sucursales.FirstOrDefault(s => s.NegocioId == negocioId && s.Id == id));

    public Task<IReadOnlyList<Sucursal>> ListarAsync(int negocioId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Sucursal>>(_sucursales.Where(s => s.NegocioId == negocioId).ToList());

    public Task<Sucursal> AddAsync(Sucursal sucursal, CancellationToken cancellationToken = default)
    {
        IdProperty.SetValue(sucursal, _nextId++);
        _sucursales.Add(sucursal);
        return Task.FromResult(sucursal);
    }
}

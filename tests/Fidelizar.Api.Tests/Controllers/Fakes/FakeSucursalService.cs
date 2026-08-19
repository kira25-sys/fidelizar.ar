using Fidelizar.Application.Services;

namespace Fidelizar.Api.Tests.Controllers.Fakes;

public sealed class FakeSucursalService : ISucursalService
{
    public IReadOnlyList<SucursalResultado> SucursalesARetornar { get; set; } = [];

    public SucursalResultado? SucursalCreadaARetornar { get; set; }

    public Task<IReadOnlyList<SucursalResultado>> ListarAsync(int negocioId, CancellationToken cancellationToken = default) =>
        Task.FromResult(SucursalesARetornar);

    public Task<SucursalResultado> CrearAsync(
        int negocioId, string nombre, string? codigoExterno, CancellationToken cancellationToken = default) =>
        Task.FromResult(SucursalCreadaARetornar ?? new SucursalResultado(1, nombre, codigoExterno, true));
}

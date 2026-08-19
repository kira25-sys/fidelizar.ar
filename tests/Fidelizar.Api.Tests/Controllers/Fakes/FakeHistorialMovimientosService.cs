using Fidelizar.Application.Services;

namespace Fidelizar.Api.Tests.Controllers.Fakes;

public sealed class FakeHistorialMovimientosService : IHistorialMovimientosService
{
    public IReadOnlyList<MovimientoHistorialItem> HistorialARetornar { get; set; } = [];

    public Task<IReadOnlyList<MovimientoHistorialItem>> ObtenerAsync(
        int negocioId, int miembroId, CancellationToken cancellationToken = default) =>
        Task.FromResult(HistorialARetornar);
}

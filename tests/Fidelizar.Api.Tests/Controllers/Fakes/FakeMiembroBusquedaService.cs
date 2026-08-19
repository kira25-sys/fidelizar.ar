using Fidelizar.Application.Services;

namespace Fidelizar.Api.Tests.Controllers.Fakes;

public sealed class FakeMiembroBusquedaService : IMiembroBusquedaService
{
    public IReadOnlyList<MiembroBusquedaResultado> ResultadosARetornar { get; set; } = [];

    public string? UltimaQuery { get; private set; }

    public Task<IReadOnlyList<MiembroBusquedaResultado>> BuscarAsync(
        int negocioId, string query, CancellationToken cancellationToken = default)
    {
        UltimaQuery = query;
        return Task.FromResult(ResultadosARetornar);
    }
}

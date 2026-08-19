using Fidelizar.Application.Services;

namespace Fidelizar.Api.Tests.Controllers.Fakes;

public sealed class FakeCierreDiarioService : ICierreDiarioService
{
    public CierreDiarioResultado? ResultadoARetornar { get; set; }

    public Task<CierreDiarioResultado> ObtenerAsync(
        int negocioId, int sucursalId, DateOnly fecha, CancellationToken cancellationToken = default) =>
        Task.FromResult(ResultadoARetornar!);
}

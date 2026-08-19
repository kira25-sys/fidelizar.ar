using Fidelizar.Application.Services;

namespace Fidelizar.Api.Tests.Controllers.Fakes;

public sealed class FakeFichaMostradorService : IFichaMostradorService
{
    public FichaMostradorResultado? FichaARetornar { get; set; }

    public Task<FichaMostradorResultado> ObtenerAsync(
        int negocioId, int miembroId, DateOnly hoy, CancellationToken cancellationToken = default) =>
        Task.FromResult(FichaARetornar!);
}

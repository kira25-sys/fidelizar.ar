using Fidelizar.Application.Services;

namespace Fidelizar.Api.Tests.Controllers.Fakes;

public sealed class FakeFichaCompletaService : IFichaCompletaService
{
    public FichaCompletaResultado? FichaARetornar { get; set; }

    public int? UltimoUsuarioIdQueLee { get; private set; }

    public Task<FichaCompletaResultado> ObtenerAsync(
        int negocioId, int miembroId, int usuarioIdQueLee, CancellationToken cancellationToken = default)
    {
        UltimoUsuarioIdQueLee = usuarioIdQueLee;
        return Task.FromResult(FichaARetornar!);
    }
}

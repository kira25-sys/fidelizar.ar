using Fidelizar.Application.Services;

namespace Fidelizar.Api.Tests.Controllers.Fakes;

/// <summary>Records what the controller asked for, no database involved (ARCHITECTURE §11).</summary>
public sealed class FakeVinculacionMiembroService : IVinculacionMiembroService
{
    public int? NegocioIdRecibido { get; private set; }

    public VincularClienteExternoSolicitud? UltimaSolicitud { get; private set; }

    public IReadOnlyList<MiembroSinVincularResultado> SinVincularARetornar { get; set; } = [];

    public VinculacionResultado? ResultadoARetornar { get; set; }

    public Task<IReadOnlyList<MiembroSinVincularResultado>> ListarSinVincularAsync(
        int negocioId, CancellationToken cancellationToken = default)
    {
        NegocioIdRecibido = negocioId;
        return Task.FromResult(SinVincularARetornar);
    }

    public Task<VinculacionResultado> VincularAsync(
        VincularClienteExternoSolicitud solicitud, CancellationToken cancellationToken = default)
    {
        UltimaSolicitud = solicitud;
        return Task.FromResult(ResultadoARetornar!);
    }
}

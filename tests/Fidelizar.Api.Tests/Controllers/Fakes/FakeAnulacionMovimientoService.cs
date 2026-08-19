using Fidelizar.Application.Services;
using Fidelizar.Domain.Entities;

namespace Fidelizar.Api.Tests.Controllers.Fakes;

public sealed class FakeAnulacionMovimientoService : IAnulacionMovimientoService
{
    public MovimientoCredito? AjusteARetornar { get; set; }

    public AnularMovimientoRequest? UltimoRequest { get; private set; }

    public Task<MovimientoCredito> AnularAsync(
        AnularMovimientoRequest request, CancellationToken cancellationToken = default)
    {
        UltimoRequest = request;
        return Task.FromResult(AjusteARetornar!);
    }
}

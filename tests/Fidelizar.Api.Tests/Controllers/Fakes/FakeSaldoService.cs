using Fidelizar.Application.Services;
using Fidelizar.Domain.Entities;

namespace Fidelizar.Api.Tests.Controllers.Fakes;

/// <summary>Records what the controller asked for, no database involved (ARCHITECTURE §11:
/// Application tests run with no database, and a controller unit test needs no more).</summary>
public sealed class FakeSaldoService : ISaldoService
{
    public decimal SaldoARetornar { get; set; }

    public RegistrarCanjeRequest? UltimoRequest { get; private set; }

    public MovimientoCredito? MovimientoARetornar { get; set; }

    public Task<decimal> ObtenerSaldoAsync(int negocioId, int miembroId, CancellationToken cancellationToken = default) =>
        Task.FromResult(SaldoARetornar);

    public Task<MovimientoCredito> RegistrarCanjeAsync(
        RegistrarCanjeRequest request, CancellationToken cancellationToken = default)
    {
        UltimoRequest = request;
        return Task.FromResult(MovimientoARetornar!);
    }
}

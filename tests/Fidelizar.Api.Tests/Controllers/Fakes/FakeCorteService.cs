using Fidelizar.Application.Services;
using Fidelizar.Domain.Entities;

namespace Fidelizar.Api.Tests.Controllers.Fakes;

public sealed class FakeCorteService : ICorteService
{
    public Corte? CorteARetornar { get; set; }

    public Task<Corte> ObtenerCorteVigenteAsync(int negocioId, CancellationToken cancellationToken = default) =>
        Task.FromResult(CorteARetornar!);

    public Task<Corte> DeclararCorteAsync(int negocioId, DateOnly fecha, int declaradoPorUsuarioId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("No usado por estos tests.");
}

using Fidelizar.Domain.Entities;
using Fidelizar.Domain.Repositories;

namespace Fidelizar.Application.Tests.Fakes;

/// <summary>In-memory stand-in for <see cref="IRegistroAuditoriaRepository"/> (ARCHITECTURE §11).</summary>
public sealed class FakeRegistroAuditoriaRepository : IRegistroAuditoriaRepository
{
    private readonly List<RegistroAuditoria> _registros = [];

    public IReadOnlyList<RegistroAuditoria> Registros => _registros;

    public Task<RegistroAuditoria> RegistrarAsync(
        RegistroAuditoria registro, CancellationToken cancellationToken = default)
    {
        _registros.Add(registro);
        return Task.FromResult(registro);
    }
}

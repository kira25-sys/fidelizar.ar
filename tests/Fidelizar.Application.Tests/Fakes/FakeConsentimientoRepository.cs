using Fidelizar.Domain.Entities;
using Fidelizar.Domain.Repositories;

namespace Fidelizar.Application.Tests.Fakes;

/// <summary>
/// In-memory stand-in for <see cref="IConsentimientoRepository"/> so
/// <c>Fidelizar.Application.Tests</c> runs with no database (ARCHITECTURE §11). Append-only, like
/// the real one: there is no way to edit or remove a row already added, only to append another.
/// </summary>
public sealed class FakeConsentimientoRepository : IConsentimientoRepository
{
    private readonly List<Consentimiento> _consentimientos = [];

    public IReadOnlyList<Consentimiento> Consentimientos => _consentimientos;

    public Task<Consentimiento?> GetVigenteAsync(
        int negocioId, int miembroId, TipoConsentimiento tipo, CancellationToken cancellationToken = default) =>
        Task.FromResult(_consentimientos
            .Select((c, indice) => (c, indice))
            .Where(x => x.c.NegocioId == negocioId && x.c.MiembroId == miembroId && x.c.Tipo == tipo)
            // Mirrors the real repository's tie-break (OcurridoEn desc, then Id desc): when two
            // rows share the same instant, the one appended later — later in insertion order — is
            // the current one.
            .OrderByDescending(x => x.c.OcurridoEn)
            .ThenByDescending(x => x.indice)
            .Select(x => x.c)
            .FirstOrDefault());

    public Task<IReadOnlyList<Consentimiento>> GetHistorialAsync(
        int negocioId, int miembroId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Consentimiento>>(_consentimientos
            .Select((c, indice) => (c, indice))
            .Where(x => x.c.NegocioId == negocioId && x.c.MiembroId == miembroId)
            .OrderByDescending(x => x.c.OcurridoEn)
            .ThenByDescending(x => x.indice)
            .Select(x => x.c)
            .ToList());

    public Task<Consentimiento> AppendAsync(Consentimiento consentimiento, CancellationToken cancellationToken = default)
    {
        _consentimientos.Add(consentimiento);
        return Task.FromResult(consentimiento);
    }
}

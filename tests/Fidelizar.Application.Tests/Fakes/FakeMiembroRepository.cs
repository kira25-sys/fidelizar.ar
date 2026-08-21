using System.Reflection;
using Fidelizar.Domain.Entities;
using Fidelizar.Domain.Repositories;
using Fidelizar.Domain.Texto;

namespace Fidelizar.Application.Tests.Fakes;

/// <summary>
/// In-memory stand-in for <see cref="IMiembroRepository"/> so <c>Fidelizar.Application.Tests</c>
/// runs with no database (ARCHITECTURE §11). Test fixtures only — every member here is invented
/// (CLAUDE.md).
/// </summary>
public sealed class FakeMiembroRepository : IMiembroRepository
{
    private static readonly PropertyInfo IdProperty = typeof(Miembro).GetProperty(nameof(Miembro.Id))!;

    private static readonly PropertyInfo ClienteExternoIdProperty =
        typeof(Miembro).GetProperty(nameof(Miembro.ClienteExternoId))!;

    private static readonly PropertyInfo ActualizadoEnProperty =
        typeof(Miembro).GetProperty(nameof(Miembro.ActualizadoEn))!;

    private readonly List<Miembro> _miembros = [];

    /// <summary>Test-only: makes the next <see cref="VincularClienteExternoAsync"/> report that it
    /// linked nothing, standing in for a concurrent request that linked the member first.</summary>
    public bool SimularVinculacionPerdida { get; set; }

    /// <summary>Test-only: what <see cref="VincularClienteExternoAsync"/> was last asked to
    /// write, so a test can prove the trimmed id reached the repository.</summary>
    public string? UltimoClienteExternoIdVinculado { get; private set; }

    public IReadOnlyList<Miembro> Miembros => _miembros;

    public void Sembrar(Miembro miembro) => _miembros.Add(miembro);

    /// <summary>Test-only: undoes an <see cref="AddAsync"/>, for tests proving a wrapping
    /// transaction rolled back correctly (ARCHITECTURE §11 — Application runs with no database, so
    /// there is no real ROLLBACK to exercise here; this fake stands in for what one would undo).</summary>
    public void Quitar(Miembro miembro) => _miembros.Remove(miembro);

    /// <summary>Builds and seeds an invented member with just enough to exercise a lookup.</summary>
    public Miembro SembrarNuevo(int negocioId, int id, string nombre = "Socia De Prueba")
    {
        var miembro = new Miembro
        {
            Id = id,
            NegocioId = negocioId,
            Nombre = nombre,
            NombreNormalizado = VipNombres.Normalizar(nombre),
            FechaAlta = new DateOnly(2026, 1, 1),
        };

        Sembrar(miembro);
        return miembro;
    }

    public Task<Miembro?> GetByClienteExternoIdAsync(
        int negocioId, string clienteExternoId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_miembros.SingleOrDefault(
            m => m.NegocioId == negocioId && m.ClienteExternoId == clienteExternoId));

    public Task<Miembro?> GetByIdAsync(int negocioId, int id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_miembros.SingleOrDefault(m => m.NegocioId == negocioId && m.Id == id));

    public Task<IReadOnlyList<Miembro>> BuscarAsync(
        int negocioId, IReadOnlyList<string> palabrasNormalizadas, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Miembro>>(_miembros
            .Where(m => m.NegocioId == negocioId
                && palabrasNormalizadas.All(p => m.NombreNormalizado.Contains(p, StringComparison.Ordinal)))
            .OrderBy(m => m.Nombre)
            .ToList());

    /// <summary>
    /// Assigns an id the way the real repository does — EF populates <c>Miembro.Id</c> on
    /// <c>SaveChangesAsync</c>. Without it every newly added member keeps <c>Id = 0</c>, and a test
    /// asserting a <c>Consentimiento</c> was tied to the member it belongs to would compare 0 to 0
    /// and pass on any member (ARCHITECTURE §11: the fake has to be faithful where the assertion
    /// depends on it).
    /// </summary>
    public Task<Miembro> AddAsync(Miembro miembro, CancellationToken cancellationToken = default)
    {
        IdProperty.SetValue(miembro, _miembros.Count == 0 ? 1 : _miembros.Max(m => m.Id) + 1);
        _miembros.Add(miembro);
        return Task.FromResult(miembro);
    }

    public Task<IReadOnlyList<Miembro>> ListarSinVincularAsync(
        int negocioId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Miembro>>(_miembros
            .Where(m => m.NegocioId == negocioId && m.ClienteExternoId is null)
            .OrderBy(m => m.FechaAlta)
            .ThenBy(m => m.Id)
            .ToList());

    /// <summary>
    /// Mirrors the real repository's <c>ClienteExternoId IS NULL</c> guard, including its return
    /// value: <c>false</c> means nothing was linked, which is the signal
    /// <c>VinculacionMiembroService</c> turns into <c>MIEMBRO_YA_VINCULADO</c>.
    /// </summary>
    public Task<bool> VincularClienteExternoAsync(
        int negocioId,
        int miembroId,
        string clienteExternoId,
        DateTime ahoraUtc,
        CancellationToken cancellationToken = default)
    {
        UltimoClienteExternoIdVinculado = clienteExternoId;

        if (SimularVinculacionPerdida)
        {
            return Task.FromResult(false);
        }

        var miembro = _miembros.SingleOrDefault(
            m => m.NegocioId == negocioId && m.Id == miembroId && m.ClienteExternoId is null);

        if (miembro is null)
        {
            return Task.FromResult(false);
        }

        ClienteExternoIdProperty.SetValue(miembro, clienteExternoId);
        ActualizadoEnProperty.SetValue(miembro, ahoraUtc);
        return Task.FromResult(true);
    }
}

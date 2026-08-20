using System.Reflection;
using Fidelizar.Domain.Entities;
using Fidelizar.Domain.Repositories;
using Fidelizar.MigracionOctaviano.Destino;

namespace Fidelizar.MigracionOctaviano.Tests.Fakes;

/// <summary>
/// In-memory stand-ins for the repositories <see cref="Migracion.MigradorOctaviano"/> depends on
/// — no database, no SQLite file, only invented fixtures (ARCHITECTURE §11, CLAUDE.md). Mirrors
/// the style of <c>Fidelizar.Infrastructure.Tests.Import.Fakes</c>: reflection sets the
/// <c>init</c>/private <c>Id</c> the same way EF Core's materialiser would.
/// </summary>
// Fully qualified: F1-03 adds a real Fidelizar.Domain.Repositories.INegocioRepository with a
// different, narrower contract than this tool-local one (see MigradorOctaviano's constructor).
public sealed class FakeNegocioRepository : Destino.INegocioRepository
{
    private static readonly PropertyInfo IdProperty = typeof(Negocio).GetProperty(nameof(Negocio.Id))!;

    private Negocio? _negocio;

    public Task<Negocio?> ObtenerUnicoAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(_negocio);

    public Task<Negocio> CrearAsync(Negocio negocio, CancellationToken cancellationToken = default)
    {
        IdProperty.SetValue(negocio, 1);
        _negocio = negocio;
        return Task.FromResult(negocio);
    }
}

public sealed class FakeConfiguracionProgramaRepository : IConfiguracionProgramaRepository
{
    private static readonly PropertyInfo IdProperty =
        typeof(ConfiguracionPrograma).GetProperty(nameof(ConfiguracionPrograma.Id))!;

    private readonly List<ConfiguracionPrograma> _configuraciones = [];
    private int _nextId = 1;

    public Task<ConfiguracionPrograma?> ObtenerVigenteAsync(int negocioId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_configuraciones.SingleOrDefault(c => c.NegocioId == negocioId && c.VigenteHasta is null));

    public Task<ConfiguracionPrograma> CrearAsync(
        ConfiguracionPrograma configuracion, CancellationToken cancellationToken = default)
    {
        IdProperty.SetValue(configuracion, _nextId++);
        _configuraciones.Add(configuracion);
        return Task.FromResult(configuracion);
    }
}

public sealed class FakeMiembroRepository : IMiembroRepository
{
    private static readonly PropertyInfo IdProperty = typeof(Miembro).GetProperty(nameof(Miembro.Id))!;

    private readonly List<Miembro> _miembros = [];
    private int _nextId = 1;

    public IReadOnlyList<Miembro> Miembros => _miembros;

    public Task<Miembro?> GetByClienteExternoIdAsync(
        int negocioId, string clienteExternoId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_miembros.FirstOrDefault(
            m => m.NegocioId == negocioId && m.ClienteExternoId == clienteExternoId));

    public Task<Miembro?> GetByIdAsync(int negocioId, int id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_miembros.FirstOrDefault(m => m.NegocioId == negocioId && m.Id == id));

    // Not exercised by the migrator itself — a minimal, correct implementation only, so this
    // fake keeps satisfying IMiembroRepository as it grows (F1-backend-endpoints-pendientes).
    public Task<IReadOnlyList<Miembro>> BuscarAsync(
        int negocioId, IReadOnlyList<string> palabrasNormalizadas, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Miembro>>(_miembros
            .Where(m => m.NegocioId == negocioId
                && palabrasNormalizadas.All(p => m.NombreNormalizado.Contains(p, StringComparison.Ordinal)))
            .ToList());

    public Task<Miembro> AddAsync(Miembro miembro, CancellationToken cancellationToken = default)
    {
        IdProperty.SetValue(miembro, _nextId++);
        _miembros.Add(miembro);
        return Task.FromResult(miembro);
    }
}

public sealed class FakeMovimientoRepository : IMovimientoRepository
{
    private readonly List<MovimientoCredito> _movimientos = [];

    public IReadOnlyList<MovimientoCredito> Movimientos => _movimientos;

    public Task<decimal> GetSaldoAsync(int negocioId, int miembroId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_movimientos
            .Where(m => m.NegocioId == negocioId && m.MiembroId == miembroId)
            .Sum(m => m.Monto));

    public Task<IReadOnlyList<MovimientoCredito>> GetPorMiembroAsync(
        int negocioId, int miembroId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<MovimientoCredito>>(_movimientos
            .Where(m => m.NegocioId == negocioId && m.MiembroId == miembroId)
            .OrderBy(m => m.FechaEfectiva)
            .ToList());

    public Task<IReadOnlyList<MovimientoCredito>> GetPorPeriodoAsync(
        int negocioId, string periodo, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<MovimientoCredito>>(_movimientos
            .Where(m => m.NegocioId == negocioId && m.Periodo == periodo)
            .ToList());

    public Task<bool> TieneMovimientosAsync(int negocioId, int miembroId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_movimientos.Any(m => m.NegocioId == negocioId && m.MiembroId == miembroId));

    // Not exercised by the migrator itself — minimal, correct implementations only, so this fake
    // keeps satisfying IMovimientoRepository as it grows (F1-backend-endpoints-pendientes).
    public Task<MovimientoCredito?> GetByIdAsync(int negocioId, long id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_movimientos.FirstOrDefault(m => m.NegocioId == negocioId && m.Id == id));

    public Task<MovimientoCredito?> GetPorClaveIdempotenciaAsync(
        int negocioId, string claveIdempotencia, CancellationToken cancellationToken = default) =>
        Task.FromResult(_movimientos.FirstOrDefault(
            m => m.NegocioId == negocioId && m.ClaveIdempotencia == claveIdempotencia));

    public Task<IReadOnlyList<MovimientoCredito>> GetPorFechaEfectivaYTipoAsync(
        int negocioId, DateOnly fechaEfectiva, TipoMovimientoCredito tipo, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<MovimientoCredito>>(_movimientos
            .Where(m => m.NegocioId == negocioId && m.FechaEfectiva == fechaEfectiva && m.Tipo == tipo)
            .ToList());

    public Task<MovimientoCredito> AppendAsync(MovimientoCredito movimiento, CancellationToken cancellationToken = default)
    {
        // Same transactional balance computation MovimientoRepository does for real, so the
        // migrator's SUM(Monto) behaviour (I2) is exercised the same way in tests.
        var saldoActual = _movimientos
            .Where(m => m.NegocioId == movimiento.NegocioId && m.MiembroId == movimiento.MiembroId)
            .Sum(m => m.Monto);

        typeof(MovimientoCredito)
            .GetMethod("FijarSaldoResultante", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(movimiento, [saldoActual + movimiento.Monto]);

        _movimientos.Add(movimiento);
        return Task.FromResult(movimiento);
    }
}

public sealed class FakeCorteRepository : ICorteRepository
{
    private readonly Dictionary<int, Corte> _cortes = [];

    public Task<Corte?> ObtenerAsync(int negocioId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_cortes.GetValueOrDefault(negocioId));

    public Task<Corte> DeclararAsync(Corte corte, CancellationToken cancellationToken = default)
    {
        _cortes[corte.NegocioId] = corte;
        return Task.FromResult(corte);
    }
}

// Fully qualified for the same reason as FakeNegocioRepository above: F1-08 adds a real
// Fidelizar.Domain.Repositories.IConsentimientoRepository with a different, richer contract.
// This fake implements only the tool-local Destino.IConsentimientoRepository MigradorOctaviano
// actually depends on.
public sealed class FakeConsentimientoRepository : Destino.IConsentimientoRepository
{
    private static readonly PropertyInfo IdProperty =
        typeof(Consentimiento).GetProperty(nameof(Consentimiento.Id))!;

    private readonly List<Consentimiento> _consentimientos = [];
    private int _nextId = 1;

    public IReadOnlyList<Consentimiento> Consentimientos => _consentimientos;

    public Task<bool> ExisteAsync(
        int negocioId, int miembroId, TipoConsentimiento tipo, CancellationToken cancellationToken = default) =>
        Task.FromResult(_consentimientos.Any(
            c => c.NegocioId == negocioId && c.MiembroId == miembroId && c.Tipo == tipo));

    public Task<Consentimiento> CrearAsync(Consentimiento consentimiento, CancellationToken cancellationToken = default)
    {
        IdProperty.SetValue(consentimiento, _nextId++);
        _consentimientos.Add(consentimiento);
        return Task.FromResult(consentimiento);
    }
}

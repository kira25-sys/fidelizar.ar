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
public sealed class FakeNegocioRepository : INegocioRepository
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

public sealed class FakeConsentimientoRepository : IConsentimientoRepository
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

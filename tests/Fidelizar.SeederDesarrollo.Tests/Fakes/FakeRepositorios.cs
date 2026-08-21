using System.Reflection;
using Fidelizar.Domain.Entities;
using Fidelizar.Domain.Repositories;
using Fidelizar.Domain.Security;
using Fidelizar.SeederDesarrollo.Destino;

namespace Fidelizar.SeederDesarrollo.Tests.Fakes;

/// <summary>
/// In-memory stand-ins for everything <see cref="Sembrado.Sembrador"/> depends on — no Postgres,
/// no connection string, only invented fixtures (ARCHITECTURE §11, CLAUDE.md). Same style as
/// <c>Fidelizar.MigracionOctaviano.Tests.Fakes</c>: reflection assigns the <c>init</c>/private
/// <c>Id</c> the way EF Core's materialiser would.
/// </summary>
public sealed class FakeNegocioSeederRepository : INegocioSeederRepository
{
    private static readonly PropertyInfo IdProperty = typeof(Negocio).GetProperty(nameof(Negocio.Id))!;

    private Negocio? _negocio;

    public int Creaciones { get; private set; }

    public Task<Negocio?> ObtenerPrimeroAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(_negocio);

    public Task<Negocio> CrearAsync(Negocio negocio, CancellationToken cancellationToken = default)
    {
        IdProperty.SetValue(negocio, 1);
        _negocio = negocio;
        Creaciones++;
        return Task.FromResult(negocio);
    }
}

public sealed class FakeSucursalRepository : ISucursalRepository
{
    private static readonly PropertyInfo IdProperty = typeof(Sucursal).GetProperty(nameof(Sucursal.Id))!;

    private readonly List<Sucursal> _sucursales = [];
    private int _proximoId = 1;

    public IReadOnlyList<Sucursal> Sucursales => _sucursales;

    public Task<Sucursal?> GetByIdAsync(int negocioId, int id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_sucursales.FirstOrDefault(s => s.NegocioId == negocioId && s.Id == id));

    public Task<IReadOnlyList<Sucursal>> ListarAsync(int negocioId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Sucursal>>(_sucursales.Where(s => s.NegocioId == negocioId).ToList());

    public Task<Sucursal> AddAsync(Sucursal sucursal, CancellationToken cancellationToken = default)
    {
        IdProperty.SetValue(sucursal, _proximoId++);
        _sucursales.Add(sucursal);
        return Task.FromResult(sucursal);
    }
}

public sealed class FakeUsuarioRepository : IUsuarioRepository
{
    private static readonly PropertyInfo IdProperty = typeof(Usuario).GetProperty(nameof(Usuario.Id))!;

    private readonly List<Usuario> _usuarios = [];
    private int _proximoId = 1;

    public IReadOnlyList<Usuario> Usuarios => _usuarios;

    public Task<Usuario?> ObtenerPorEmailAsync(
        int negocioId, string email, CancellationToken cancellationToken = default) =>
        Task.FromResult(_usuarios.FirstOrDefault(
            u => u.NegocioId == negocioId && string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase)));

    public Task<IReadOnlyList<Usuario>> ListarAsync(int negocioId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Usuario>>(_usuarios.Where(u => u.NegocioId == negocioId).ToList());

    public Task<Usuario> CrearAsync(Usuario usuario, CancellationToken cancellationToken = default)
    {
        IdProperty.SetValue(usuario, _proximoId++);
        _usuarios.Add(usuario);
        return Task.FromResult(usuario);
    }

    public Task DesactivarAsync(Usuario usuario, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
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

public sealed class FakeMiembroRepository : IMiembroRepository
{
    private static readonly PropertyInfo IdProperty = typeof(Miembro).GetProperty(nameof(Miembro.Id))!;

    private readonly List<Miembro> _miembros = [];
    private int _proximoId = 1;

    public IReadOnlyList<Miembro> Miembros => _miembros;

    public Task<Miembro?> GetByClienteExternoIdAsync(
        int negocioId, string clienteExternoId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_miembros.FirstOrDefault(
            m => m.NegocioId == negocioId && m.ClienteExternoId == clienteExternoId));

    public Task<Miembro?> GetByIdAsync(int negocioId, int id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_miembros.FirstOrDefault(m => m.NegocioId == negocioId && m.Id == id));

    public Task<IReadOnlyList<Miembro>> BuscarAsync(
        int negocioId, IReadOnlyList<string> palabrasNormalizadas, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Miembro>>(_miembros
            .Where(m => m.NegocioId == negocioId
                && palabrasNormalizadas.All(p => m.NombreNormalizado.Contains(p, StringComparison.Ordinal)))
            .ToList());

    public Task<Miembro> AddAsync(Miembro miembro, CancellationToken cancellationToken = default)
    {
        IdProperty.SetValue(miembro, _proximoId++);
        _miembros.Add(miembro);
        return Task.FromResult(miembro);
    }

    // Not exercised by the seeder — F1-14's two methods, minimal and correct.
    public Task<IReadOnlyList<Miembro>> ListarSinVincularAsync(
        int negocioId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Miembro>>(_miembros
            .Where(m => m.NegocioId == negocioId && m.ClienteExternoId is null)
            .OrderBy(m => m.FechaAlta)
            .ToList());

    public Task<bool> VincularClienteExternoAsync(
        int negocioId,
        int miembroId,
        string clienteExternoId,
        DateTime ahoraUtc,
        CancellationToken cancellationToken = default)
    {
        var miembro = _miembros.FirstOrDefault(
            m => m.NegocioId == negocioId && m.Id == miembroId && m.ClienteExternoId is null);

        if (miembro is null)
        {
            return Task.FromResult(false);
        }

        typeof(Miembro).GetProperty(nameof(Miembro.ClienteExternoId))!.SetValue(miembro, clienteExternoId);
        typeof(Miembro).GetProperty(nameof(Miembro.ActualizadoEn))!.SetValue(miembro, ahoraUtc);
        return Task.FromResult(true);
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

    public Task<MovimientoCredito?> GetByIdAsync(int negocioId, long id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_movimientos.FirstOrDefault(m => m.NegocioId == negocioId && m.Id == id));

    public Task<MovimientoCredito?> GetPorClaveIdempotenciaAsync(
        int negocioId, string claveIdempotencia, CancellationToken cancellationToken = default) =>
        Task.FromResult(_movimientos.FirstOrDefault(
            m => m.NegocioId == negocioId && m.ClaveIdempotencia == claveIdempotencia));

    public Task<IReadOnlyList<MovimientoCredito>> GetPorFechaEfectivaYTipoAsync(
        int negocioId, DateOnly fechaEfectiva, TipoMovimientoCredito tipo,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<MovimientoCredito>>(_movimientos
            .Where(m => m.NegocioId == negocioId && m.FechaEfectiva == fechaEfectiva && m.Tipo == tipo)
            .ToList());

    public Task<MovimientoCredito> AppendAsync(
        MovimientoCredito movimiento, CancellationToken cancellationToken = default)
    {
        // Same balance stamp the real MovimientoRepository does inside its transaction (I2).
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

public sealed class FakeConsentimientoRepository : IConsentimientoRepository
{
    private static readonly PropertyInfo IdProperty =
        typeof(Consentimiento).GetProperty(nameof(Consentimiento.Id))!;

    private readonly List<Consentimiento> _consentimientos = [];
    private int _proximoId = 1;

    public IReadOnlyList<Consentimiento> Consentimientos => _consentimientos;

    public Task<Consentimiento?> GetVigenteAsync(
        int negocioId, int miembroId, TipoConsentimiento tipo, CancellationToken cancellationToken = default) =>
        Task.FromResult(_consentimientos
            .Where(c => c.NegocioId == negocioId && c.MiembroId == miembroId && c.Tipo == tipo)
            .OrderByDescending(c => c.Id)
            .FirstOrDefault());

    public Task<IReadOnlyList<Consentimiento>> GetHistorialAsync(
        int negocioId, int miembroId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Consentimiento>>(_consentimientos
            .Where(c => c.NegocioId == negocioId && c.MiembroId == miembroId)
            .OrderByDescending(c => c.Id)
            .ToList());

    public Task<Consentimiento> AppendAsync(
        Consentimiento consentimiento, CancellationToken cancellationToken = default)
    {
        IdProperty.SetValue(consentimiento, _proximoId++);
        _consentimientos.Add(consentimiento);
        return Task.FromResult(consentimiento);
    }
}

/// <summary>
/// A hasher that only records what it was asked to hash. The point of the test that uses it is
/// that the seeder passes the operator's password through <see cref="IPasswordHasher"/> at all —
/// the real algorithm is <c>Fidelizar.Infrastructure</c>'s and is not this tool's to re-test.
/// </summary>
public sealed class FakePasswordHasher : IPasswordHasher
{
    public List<string> PasswordsHasheadas { get; } = [];

    public string Hash(string password)
    {
        PasswordsHasheadas.Add(password);
        return $"hash-de-prueba::{PasswordsHasheadas.Count}";
    }

    public bool Verify(string passwordHash, string providedPassword) =>
        passwordHash.StartsWith("hash-de-prueba::", StringComparison.Ordinal);
}

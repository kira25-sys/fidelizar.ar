using Fidelizar.Domain.Consentimientos;
using Fidelizar.Domain.Entities;
using Fidelizar.Domain.Repositories;
using Fidelizar.Domain.Security;
using Fidelizar.Domain.Texto;
using Fidelizar.SeederDesarrollo.Datos;
using Fidelizar.SeederDesarrollo.Destino;

namespace Fidelizar.SeederDesarrollo.Sembrado;

/// <summary>
/// Writes the invented fixture (<see cref="DatosInventados"/>) into a development database.
///
/// <para>
/// <b>Idempotent, and never destructive. That is the choice, and it is the whole contract.</b>
/// There is no delete and no update anywhere in this class: every item is looked up by its
/// natural key first (<c>Negocio</c> by being the only row, <c>Sucursal</c> by
/// <c>CodigoExterno</c>, <c>Usuario</c> by email, <c>Miembro</c> by <c>ClienteExternoId</c>,
/// <c>Corte</c> by business, a member's ledger by "has any movement at all") and created only when
/// it is absent. Running the tool twice completes whatever was missing and duplicates nothing;
/// running it against a database somebody else populated adds to it without touching a single
/// existing row. The ledger stays append-only here exactly as it does everywhere else (I1) — this
/// tool has no more power over it than a cashier does.
/// </para>
///
/// <para>
/// The one gap, stated rather than hidden: a member's history is seeded only when that member has
/// <i>zero</i> movements, the same gate <c>MigradorOctaviano</c> and <c>VipPadronImporter</c> use.
/// A run interrupted halfway through one member's movements will not resume that member on the
/// next run. In a development database the answer is to drop it and seed again.
/// </para>
/// </summary>
public sealed class Sembrador(
    INegocioSeederRepository negocioRepository,
    ISucursalRepository sucursalRepository,
    IUsuarioRepository usuarioRepository,
    ICorteRepository corteRepository,
    IMiembroRepository miembroRepository,
    IMovimientoRepository movimientoRepository,
    IConsentimientoRepository consentimientoRepository,
    IPasswordHasher passwordHasher)
{
    /// <param name="password">
    /// The operator's <c>FIDELIZAR_SEED_PASSWORD</c>, hashed here through the product's own
    /// <see cref="IPasswordHasher"/> — the same implementation the login path verifies against, so
    /// a seeded account signs in through the real <c>POST /auth/login</c> and this tool never
    /// writes a hash of its own making.
    /// </param>
    /// <param name="corte">
    /// The cutoff to declare when the business has none yet. Invented, like everything else here:
    /// a fabricated business has a fabricated cutoff. F0-07's "never a constant" rule protects a
    /// real import from double-crediting real purchases, which is not a risk that exists for six
    /// invented members — but <c>--corte</c> is still there for when the caller wants a specific
    /// one.
    /// </param>
    public async Task<ResultadoSembrado> EjecutarAsync(
        string password,
        DateOnly corte,
        DateOnly hoy,
        DateTime ahoraUtc,
        CancellationToken cancellationToken = default)
    {
        var (negocio, negocioCreado) = await ObtenerOCrearNegocioAsync(ahoraUtc, cancellationToken);

        var (sucursalesPorCodigo, sucursalesCreadas, sucursalesYaExistian) =
            await SembrarSucursalesAsync(negocio.Id, cancellationToken);

        var (usuariosPorEmail, usuariosCreados, usuariosYaExistian) =
            await SembrarUsuariosAsync(negocio.Id, password, sucursalesPorCodigo, ahoraUtc, cancellationToken);

        // Every movement, cutoff and consent row this tool writes is stamped with the seeded Dueño
        // — a real Usuario row in this same database, not a placeholder id. "Every movement records
        // the real person who caused it" (ARCHITECTURE §8), and in a development database the
        // person who ran the seeder is that person.
        var dueno = usuariosPorEmail[DatosInventados.EmailDueno];

        var (corteVigente, corteDeclarado) =
            await DeclararCorteSiFaltaAsync(negocio.Id, corte, dueno.Id, ahoraUtc, cancellationToken);

        var creados = 0;
        var yaExistian = 0;
        var consentimientosCreados = 0;
        var consentimientosYaExistian = 0;
        var movimientosCreados = 0;
        var conHistorialPrevio = 0;
        var saldos = new List<SaldoSembrado>();

        foreach (var inventado in DatosInventados.Miembros(corteVigente.Fecha, hoy))
        {
            var existente = await miembroRepository.GetByClienteExternoIdAsync(
                negocio.Id, inventado.ClienteExternoId, cancellationToken);

            Miembro miembro;
            if (existente is null)
            {
                miembro = await miembroRepository.AddAsync(
                    new Miembro
                    {
                        NegocioId = negocio.Id,
                        ClienteExternoId = inventado.ClienteExternoId,
                        NumeroSocio = inventado.NumeroSocio,
                        Nombre = inventado.Nombre,
                        // The one place this value may be produced, so the counter search finds
                        // the seeded members exactly as it finds imported ones (DATA-MODEL §3).
                        NombreNormalizado = VipNombres.Normalizar(inventado.Nombre),
                        Telefono = inventado.Telefono,
                        Dni = inventado.Dni,
                        FechaNacimiento = inventado.FechaNacimiento,
                        SucursalId = sucursalesPorCodigo[inventado.CodigoSucursal].Id,
                        FechaAlta = corteVigente.Fecha,
                        Activo = true,
                        ActualizadoEn = ahoraUtc,
                    },
                    cancellationToken);
                creados++;
            }
            else
            {
                miembro = existente;
                yaExistian++;
            }

            if (await SembrarConsentimientoSiFaltaAsync(negocio, miembro, dueno.Id, ahoraUtc, cancellationToken))
            {
                consentimientosCreados++;
            }
            else
            {
                consentimientosYaExistian++;
            }

            if (await movimientoRepository.TieneMovimientosAsync(negocio.Id, miembro.Id, cancellationToken))
            {
                conHistorialPrevio++;
            }
            else
            {
                foreach (var movimiento in inventado.Movimientos)
                {
                    await movimientoRepository.AppendAsync(
                        MovimientoCredito.Crear(
                            negocio.Id,
                            miembro.Id,
                            movimiento.FechaEfectiva,
                            ahoraUtc,
                            movimiento.Tipo,
                            movimiento.Monto,
                            hoy,
                            usuarioId: dueno.Id,
                            motivo: movimiento.Motivo),
                        cancellationToken);
                    movimientosCreados++;
                }
            }

            // I2: read back from SUM(Monto), never from the fixture's own arithmetic. If the two
            // ever disagreed, the number printed at the end would be the database's, not this
            // file's — which is the only one worth printing.
            saldos.Add(new SaldoSembrado(
                inventado.ClienteExternoId,
                inventado.Nombre,
                await movimientoRepository.GetSaldoAsync(negocio.Id, miembro.Id, cancellationToken)));
        }

        return new ResultadoSembrado(
            negocio.Id, negocioCreado, corteVigente.Fecha, corteDeclarado,
            sucursalesCreadas, sucursalesYaExistian,
            usuariosCreados, usuariosYaExistian,
            creados, yaExistian,
            consentimientosCreados, consentimientosYaExistian,
            movimientosCreados, conHistorialPrevio,
            saldos);
    }

    private async Task<(Negocio Negocio, bool Creado)> ObtenerOCrearNegocioAsync(
        DateTime ahoraUtc, CancellationToken cancellationToken)
    {
        var existente = await negocioRepository.ObtenerPrimeroAsync(cancellationToken);
        if (existente is not null)
        {
            return (existente, false);
        }

        var negocio = await negocioRepository.CrearAsync(
            new Negocio
            {
                Nombre = DatosInventados.NegocioNombre,
                Cuit = DatosInventados.NegocioCuit,
                Domicilio = DatosInventados.NegocioDomicilio,
                Activo = true,
                CreadoEn = ahoraUtc,
            },
            cancellationToken);

        return (negocio, true);
    }

    private async Task<(Dictionary<string, Sucursal> PorCodigo, int Creadas, int YaExistian)>
        SembrarSucursalesAsync(int negocioId, CancellationToken cancellationToken)
    {
        var existentes = await sucursalRepository.ListarAsync(negocioId, cancellationToken);
        var porCodigo = existentes
            .Where(s => s.CodigoExterno is not null)
            .ToDictionary(s => s.CodigoExterno!, StringComparer.OrdinalIgnoreCase);

        var creadas = 0;
        var yaExistian = 0;

        foreach (var inventada in DatosInventados.Sucursales)
        {
            if (porCodigo.ContainsKey(inventada.CodigoExterno))
            {
                yaExistian++;
                continue;
            }

            porCodigo[inventada.CodigoExterno] = await sucursalRepository.AddAsync(
                Sucursal.Crear(negocioId, inventada.Nombre, inventada.CodigoExterno), cancellationToken);
            creadas++;
        }

        return (porCodigo, creadas, yaExistian);
    }

    private async Task<(Dictionary<string, Usuario> PorEmail, int Creados, int YaExistian)>
        SembrarUsuariosAsync(
            int negocioId,
            string password,
            IReadOnlyDictionary<string, Sucursal> sucursalesPorCodigo,
            DateTime ahoraUtc,
            CancellationToken cancellationToken)
    {
        // The three accounts share the operator's single FIDELIZAR_SEED_PASSWORD, but each one is
        // hashed on its own: PasswordHasher salts per call, so the three rows carry three
        // different hashes and no two of them can be compared against each other.
        var porEmail = new Dictionary<string, Usuario>(StringComparer.OrdinalIgnoreCase);
        var creados = 0;
        var yaExistian = 0;

        foreach (var inventado in DatosInventados.Usuarios)
        {
            var existente = await usuarioRepository.ObtenerPorEmailAsync(
                negocioId, inventado.Email, cancellationToken);

            if (existente is not null)
            {
                porEmail[inventado.Email] = existente;
                yaExistian++;
                continue;
            }

            porEmail[inventado.Email] = await usuarioRepository.CrearAsync(
                Usuario.Crear(
                    negocioId,
                    inventado.NombreCompleto,
                    inventado.Email,
                    passwordHasher.Hash(password),
                    inventado.Rol,
                    ahoraUtc,
                    inventado.CodigoSucursal is { } codigo ? sucursalesPorCodigo[codigo].Id : null),
                cancellationToken);
            creados++;
        }

        return (porEmail, creados, yaExistian);
    }

    private async Task<(Corte Corte, bool Declarado)> DeclararCorteSiFaltaAsync(
        int negocioId, DateOnly fecha, int declaradoPorUsuarioId, DateTime ahoraUtc,
        CancellationToken cancellationToken)
    {
        var existente = await corteRepository.ObtenerAsync(negocioId, cancellationToken);
        if (existente is not null)
        {
            return (existente, false);
        }

        var corte = await corteRepository.DeclararAsync(
            Corte.Declarar(negocioId, fecha, declaradoPorUsuarioId, ahoraUtc), cancellationToken);

        return (corte, true);
    }

    /// <summary>
    /// Records the <c>DatosPersonales</c> consent every seeded member needs to exist at all:
    /// FUNCTIONAL-SPEC §7 makes it mandatory for an alta, so a member seeded without it would be a
    /// member the product's own alta path could never have produced. <c>DatosSensibles</c> is
    /// deliberately <b>not</b> seeded — it is optional, it gates health data (I10), and no screen
    /// needs it to render.
    /// </summary>
    private async Task<bool> SembrarConsentimientoSiFaltaAsync(
        Negocio negocio, Miembro miembro, int registradoPorUsuarioId, DateTime ahoraUtc,
        CancellationToken cancellationToken)
    {
        var vigente = await consentimientoRepository.GetVigenteAsync(
            negocio.Id, miembro.Id, TipoConsentimiento.DatosPersonales, cancellationToken);

        if (vigente is not null)
        {
            return false;
        }

        await consentimientoRepository.AppendAsync(
            Consentimiento.Registrar(
                negocio.Id,
                miembro.Id,
                TipoConsentimiento.DatosPersonales,
                otorgado: true,
                TextosConsentimiento.DatosPersonalesVersion,
                CanalConsentimiento.Mostrador,
                ahoraUtc,
                registradoPorUsuarioId),
            cancellationToken);

        return true;
    }
}

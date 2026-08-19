using System.Globalization;
using Fidelizar.Domain.Entities;
using Fidelizar.Domain.Money;
using Fidelizar.Domain.Repositories;
using Fidelizar.Domain.Texto;
using Fidelizar.MigracionOctaviano.Destino;
using Fidelizar.MigracionOctaviano.Origen;

namespace Fidelizar.MigracionOctaviano.Migracion;

/// <summary>
/// The one-off migration itself (ROADMAP F0-09): Octaviano's SQLite VIP club → Fidelizar's
/// Postgres, members and their <b>entire</b> ledger, plus one <see cref="Consentimiento"/> per
/// member for every <see cref="TipoConsentimiento"/> (DATA-MODEL §7).
///
/// <para>
/// <b>The ledger is migrated whole, never collapsed.</b> Unlike <c>VipPadronImporter</c> (F0-08),
/// which writes a single <c>SaldoInicial</c> from a spreadsheet's current balance, this reads
/// every row of Octaviano's own <c>VipMovimientos</c> table and writes one
/// <see cref="MovimientoCredito"/> per row, preserving its original type, amount and date — the
/// whole point of the F0-11 gate is recomputing the balance from these movements and getting the
/// same number Octaviano gets today from <c>SUM(Monto)</c> over the same history.
/// </para>
///
/// <para>
/// <b>Idempotent per member, not per movement.</b> A member's entire movement history is migrated
/// only if that member currently has <i>zero</i> movements in Fidelizar
/// (<see cref="IMovimientoRepository.TieneMovimientosAsync"/>) — the same gate
/// <c>VipPadronImporter</c> already uses for its own single <c>SaldoInicial</c> row. Running this
/// tool twice therefore cannot duplicate a member's history. The one gap this leaves, accepted
/// and documented (see the F0-09 report): if a run is interrupted <i>after</i> some of a member's
/// movements were appended but before all of them were, a re-run sees "this member already has
/// movements" and will not resume — the DB-level unique index on
/// <c>(NegocioId, MiembroId, Tipo, ReferenciaVenta)</c> for <c>Acumulacion</c> rows is the other
/// safety net DATA-MODEL §4 already provides, and every append is still wrapped so one bad row
/// cannot silently corrupt the rest.
/// </para>
///
/// <para>
/// <b>No business-rule number is a literal here.</b> ARCHITECTURE §6 forbids writing RN-01's
/// accrual percentage or RN-06's monthly target inline in code —
/// <see cref="EjecutarAsync"/> takes both as parameters, and <c>Program.cs</c> is the one place
/// that reads them (<c>--porcentaje-acumulacion</c>, <c>--objetivo-mensual</c>), required and
/// with no default, the same way <c>Corte</c> is not a constant either (F0-07). Octaviano's own
/// cutoff (<c>VipCortes</c>) is migrated the same way <c>VipPadronImporter</c> would declare one:
/// once, never overwritten on a mismatch.
/// </para>
/// </summary>
public sealed class MigradorOctaviano(
    IOctavianoSource origen,
    // Fully qualified: F1-03 adds a real Fidelizar.Domain.Repositories.INegocioRepository with a
    // different, narrower contract (exactly one active Negocio, or a loud failure — no create,
    // no "not yet migrated" case). This tool keeps its own tool-local Destino.INegocioRepository
    // unchanged — get-or-create is what a one-off bootstrap migration actually needs, and this
    // tool's behaviour is already gate-verified against real production data (ROADMAP, F0-11).
    Destino.INegocioRepository negocioRepository,
    IConfiguracionProgramaRepository configuracionRepository,
    IMiembroRepository miembroRepository,
    IMovimientoRepository movimientoRepository,
    // Fully qualified for the same reason as Destino.INegocioRepository above: F1-08 adds a real
    // Fidelizar.Domain.Repositories.IConsentimientoRepository with a different, richer contract
    // (GetVigenteAsync, GetHistorialAsync — reads the Application layer needs, this tool does
    // not). This tool keeps its own tool-local Destino.IConsentimientoRepository unchanged: its
    // ExisteAsync/CrearAsync shape is already gate-verified against real production data
    // (ROADMAP, F0-11) and 879 migrated rows depend on nothing about it changing.
    Destino.IConsentimientoRepository consentimientoRepository,
    ICorteRepository corteRepository)
{
    // Usuario es F1-03 y todavía no existe (igual que MovimientoCredito.UsuarioId en el resto
    // del código ya portado). 0 representa "migración F0-09", no un usuario real — usado tanto
    // para ConfiguracionPrograma.CreadoPorUsuarioId como para Corte.DeclaradoPorUsuarioId.
    private const int UsuarioPlaceholderMigracion = 0;

    private const string TextoConsentimientoMigracion =
        "Consentimiento verbal registrado al momento del alta como socio del club, migrado desde " +
        "Octaviano (DATA-MODEL §7, F0-09). No hay un formulario digital anterior que citar como versión.";

    /// <param name="porcentajeAcumulacion">
    /// RN-01's percentage (ARCHITECTURE §6: "Accrual percentage → per-business configuration").
    /// A business rule number, so it is never a literal in this tool's own code — it is the
    /// caller's job (Program.cs's <c>--porcentaje-acumulacion</c>) to supply it, exactly like
    /// <c>Corte</c> is not a constant either (F0-07).
    /// </param>
    /// <param name="objetivoMensual">
    /// RN-06's monthly target, or null when the program has none — <c>ObjetivoMensual</c> is
    /// nullable on purpose (DATA-MODEL §1).
    /// </param>
    public async Task<ResultadoMigracion> EjecutarAsync(
        decimal porcentajeAcumulacion,
        decimal? objetivoMensual,
        DateTime ahoraUtc,
        CancellationToken cancellationToken = default)
    {
        var negocio = await ObtenerOCrearNegocioAsync(ahoraUtc, cancellationToken);

        var miembrosOctaviano = await origen.LeerMiembrosAsync(cancellationToken);
        var movimientosOctaviano = await origen.LeerMovimientosAsync(cancellationToken);

        var configuracion = await ObtenerOCrearConfiguracionAsync(
            negocio.Id, porcentajeAcumulacion, objetivoMensual, miembrosOctaviano, movimientosOctaviano,
            ahoraUtc, cancellationToken);

        var (corteFecha, corteAdvertencia) = await MigrarCorteAsync(negocio.Id, ahoraUtc, cancellationToken);

        var (miembrosCreados, miembrosYaExistian, miembrosSalteados, mapaMiembros) =
            await MigrarMiembrosAsync(negocio.Id, miembrosOctaviano, ahoraUtc, cancellationToken);

        var (consentimientosCreados, consentimientosYaExistian) =
            await MigrarConsentimientosAsync(negocio.Id, mapaMiembros.Values, cancellationToken);

        var (movimientosMigrados, sociosConHistoriaPrevia, movimientosSalteados) = await MigrarMovimientosAsync(
            negocio.Id, configuracion.Id, movimientosOctaviano, mapaMiembros, ahoraUtc, cancellationToken);

        return new ResultadoMigracion(
            NegocioId: negocio.Id,
            ConfiguracionId: configuracion.Id,
            CorteFecha: corteFecha,
            CorteAdvertencia: corteAdvertencia,
            MiembrosCreados: miembrosCreados,
            MiembrosYaExistian: miembrosYaExistian,
            MiembrosSalteados: miembrosSalteados,
            MovimientosMigrados: movimientosMigrados,
            SociosConMovimientosYaMigrados: sociosConHistoriaPrevia,
            MovimientosSalteados: movimientosSalteados,
            ConsentimientosCreados: consentimientosCreados,
            ConsentimientosYaExistian: consentimientosYaExistian);
    }

    /// <summary>
    /// Migrates Octaviano's own cutoff (<c>VipCortes</c>, one row) as Fidelizar's
    /// <see cref="Corte"/> — the date the old system genuinely uses today, so the migrated
    /// business is left with a real cutoff instead of none at all (owner's decision, see the
    /// F0-09 follow-up report). <see cref="ICorteRepository"/> has no update method (I1-adjacent
    /// discipline, ARCHITECTURE §3): a mismatch with one already on record is reported, never
    /// overwritten — the same rule <c>VipPadronImporter</c> already follows for its own cutoff.
    /// </summary>
    private async Task<(DateOnly? Fecha, string? Advertencia)> MigrarCorteAsync(
        int negocioId, DateTime ahoraUtc, CancellationToken cancellationToken)
    {
        var corteOctaviano = await origen.LeerCorteAsync(cancellationToken);
        if (corteOctaviano is null)
        {
            return (null, null);
        }

        var existente = await corteRepository.ObtenerAsync(negocioId, cancellationToken);
        if (existente is null)
        {
            var declarado = await corteRepository.DeclararAsync(
                Corte.Declarar(negocioId, corteOctaviano.Fecha, UsuarioPlaceholderMigracion, ahoraUtc),
                cancellationToken);
            return (declarado.Fecha, null);
        }

        if (existente.Fecha != corteOctaviano.Fecha)
        {
            return (existente.Fecha,
                $"el corte de Octaviano ({corteOctaviano.Fecha:yyyy-MM-dd}) no coincide con el ya " +
                $"declarado en Fidelizar ({existente.Fecha:yyyy-MM-dd}); no se sobrescribe.");
        }

        return (existente.Fecha, null);
    }

    private async Task<Negocio> ObtenerOCrearNegocioAsync(DateTime ahoraUtc, CancellationToken cancellationToken)
    {
        var existente = await negocioRepository.ObtenerUnicoAsync(cancellationToken);
        if (existente is not null)
        {
            return existente;
        }

        // Single-tenant deployment: exactly one row (DATA-MODEL §1). The owner renames it once a
        // real business name is on hand — this placeholder is deliberately not a guess at one.
        var nuevo = new Negocio
        {
            Nombre = "Negocio migrado de Octaviano (F0-09)",
            Activo = true,
            CreadoEn = ahoraUtc,
        };

        return await negocioRepository.CrearAsync(nuevo, cancellationToken);
    }

    private async Task<ConfiguracionPrograma> ObtenerOCrearConfiguracionAsync(
        int negocioId,
        decimal porcentajeAcumulacion,
        decimal? objetivoMensual,
        IReadOnlyList<OctavianoMiembro> miembros,
        IReadOnlyList<OctavianoMovimiento> movimientos,
        DateTime ahoraUtc,
        CancellationToken cancellationToken)
    {
        var existente = await configuracionRepository.ObtenerVigenteAsync(negocioId, cancellationToken);
        if (existente is not null)
        {
            return existente;
        }

        var vigenteDesde = movimientos.Count > 0
            ? DateOnly.FromDateTime(movimientos.Min(m => m.Fecha))
            : miembros.Count > 0
                ? miembros.Min(m => m.FechaAlta)
                : DateOnly.FromDateTime(ahoraUtc);

        var nueva = new ConfiguracionPrograma
        {
            NegocioId = negocioId,
            PorcentajeAcumulacion = porcentajeAcumulacion,
            ObjetivoMensual = objetivoMensual,
            GraciaHabilitada = false,
            MesesDeGracia = null,
            UmbralMesMalo = null,
            TopeMesesCongelados = null,
            VigenteDesde = vigenteDesde,
            VigenteHasta = null,
            CreadoPorUsuarioId = UsuarioPlaceholderMigracion,
        };

        return await configuracionRepository.CrearAsync(nueva, cancellationToken);
    }

    private async Task<(int Creados, int YaExistian, IReadOnlyList<RegistroSalteado> Salteados, Dictionary<int, Miembro> Mapa)>
        MigrarMiembrosAsync(
            int negocioId,
            IReadOnlyList<OctavianoMiembro> miembrosOctaviano,
            DateTime ahoraUtc,
            CancellationToken cancellationToken)
    {
        var creados = 0;
        var yaExistian = 0;
        var salteados = new List<RegistroSalteado>();
        var mapa = new Dictionary<int, Miembro>();

        foreach (var om in miembrosOctaviano)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var clienteExternoId = om.ClienteExternoId.ToString(CultureInfo.InvariantCulture);

            if (string.IsNullOrWhiteSpace(om.Nombre))
            {
                salteados.Add(new RegistroSalteado(clienteExternoId, "el nombre está vacío en el origen"));
                continue;
            }

            var existente = await miembroRepository.GetByClienteExternoIdAsync(negocioId, clienteExternoId, cancellationToken);
            if (existente is not null)
            {
                yaExistian++;
                mapa[om.Id] = existente;
                continue;
            }

            var nuevo = new Miembro
            {
                NegocioId = negocioId,
                ClienteExternoId = clienteExternoId,
                NumeroSocio = om.NumeroVip,
                Nombre = om.Nombre,
                NombreNormalizado = VipNombres.Normalizar(om.Nombre),
                Telefono = om.Telefono,
                Dni = om.Dni,
                FechaNacimiento = om.FechaNacimiento,
                // Sucursal queda sin mapear — la misma decisión deliberada que VipPadronImporter
                // (F0-08) ya tomó: mapear el código de sucursal es un asunto de fase 1 (S10).
                SucursalId = null,
                FechaAlta = om.FechaAlta,
                Activo = om.Activo,
                ActualizadoEn = ahoraUtc,
            };

            var creado = await miembroRepository.AddAsync(nuevo, cancellationToken);
            creados++;
            mapa[om.Id] = creado;
        }

        return (creados, yaExistian, salteados, mapa);
    }

    private async Task<(int Creados, int YaExistian)> MigrarConsentimientosAsync(
        int negocioId, IEnumerable<Miembro> miembros, CancellationToken cancellationToken)
    {
        var creados = 0;
        var yaExistian = 0;

        TipoConsentimiento[] tipos =
        [
            TipoConsentimiento.DatosPersonales,
            TipoConsentimiento.DatosSensibles,
            TipoConsentimiento.Comunicaciones,
        ];

        foreach (var miembro in miembros)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var ocurridoEn = new DateTime(
                miembro.FechaAlta.Year, miembro.FechaAlta.Month, miembro.FechaAlta.Day, 0, 0, 0, DateTimeKind.Utc);

            foreach (var tipo in tipos)
            {
                if (await consentimientoRepository.ExisteAsync(negocioId, miembro.Id, tipo, cancellationToken))
                {
                    yaExistian++;
                    continue;
                }

                var consentimiento = Consentimiento.Registrar(
                    negocioId: negocioId,
                    miembroId: miembro.Id,
                    tipo: tipo,
                    otorgado: true,
                    versionTexto: TextoConsentimientoMigracion,
                    canal: CanalConsentimiento.MigracionVerbal,
                    ocurridoEn: ocurridoEn,
                    registradoPorUsuarioId: null);

                await consentimientoRepository.CrearAsync(consentimiento, cancellationToken);
                creados++;
            }
        }

        return (creados, yaExistian);
    }

    private async Task<(int Migrados, int SociosConHistoriaPrevia, IReadOnlyList<RegistroSalteado> Salteados)>
        MigrarMovimientosAsync(
            int negocioId,
            int configuracionId,
            IReadOnlyList<OctavianoMovimiento> movimientosOctaviano,
            IReadOnlyDictionary<int, Miembro> mapaMiembros,
            DateTime ahoraUtc,
            CancellationToken cancellationToken)
    {
        var migrados = 0;
        var sociosConHistoriaPrevia = 0;
        var salteados = new List<RegistroSalteado>();
        var hoy = DateOnly.FromDateTime(ahoraUtc);

        var porMiembro = movimientosOctaviano.GroupBy(m => m.MiembroId);

        foreach (var grupo in porMiembro)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!mapaMiembros.TryGetValue(grupo.Key, out var miembro))
            {
                // No debería pasar (VipMovimientos.MiembroId es FK), pero si la base tiene un
                // huérfano no lo inventamos ni lo saltamos en silencio: se reporta con el id
                // interno de Octaviano — no es un ClienteExternoId, así que se etiqueta distinto.
                salteados.Add(new RegistroSalteado(
                    $"(id interno de Octaviano {grupo.Key})",
                    $"referencia un socio que no existe en VipMiembros — se saltearon {grupo.Count()} movimiento(s)"));
                continue;
            }

            var clienteExternoId = miembro.ClienteExternoId ?? $"(Miembro.Id {miembro.Id})";

            if (await movimientoRepository.TieneMovimientosAsync(negocioId, miembro.Id, cancellationToken))
            {
                sociosConHistoriaPrevia++;
                continue;
            }

            // Cronológico: el historial se aprecia igual sin importar el orden (el saldo es
            // SUM(Monto), I2), pero mantiene SaldoResultante — evidencia histórica, DATA-MODEL §4
            // — coherente con el orden real en que pasaron las cosas.
            var ordenados = grupo.OrderBy(m => m.Fecha).ThenBy(m => m.Id);

            foreach (var om in ordenados)
            {
                if (!Enum.IsDefined(typeof(TipoMovimientoCredito), om.Tipo))
                {
                    salteados.Add(new RegistroSalteado(clienteExternoId, $"tipo de movimiento desconocido: {om.Tipo}"));
                    continue;
                }

                var tipo = (TipoMovimientoCredito)om.Tipo;

                try
                {
                    var movimiento = MovimientoCredito.Crear(
                        negocioId: negocioId,
                        miembroId: miembro.Id,
                        fechaEfectiva: DateOnly.FromDateTime(om.Fecha),
                        registradoEn: ahoraUtc,
                        tipo: tipo,
                        monto: Redondeo.Monto(om.Monto),
                        hoy: hoy,
                        usuarioId: null,
                        motivo: string.IsNullOrWhiteSpace(om.Motivo)
                            ? $"Migrado de Octaviano ({tipo})."
                            : om.Motivo,
                        configuracionId: tipo == TipoMovimientoCredito.Acumulacion ? configuracionId : null,
                        referenciaVenta: om.ReferenciaVenta?.ToString(CultureInfo.InvariantCulture));

                    await movimientoRepository.AppendAsync(movimiento, cancellationToken);
                    migrados++;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // El mensaje de una excepción de dominio (ValidationException) o de EF sobre
                    // una violación de índice único es siempre texto fijo, nunca datos del socio
                    // — seguro de reportar tal cual.
                    salteados.Add(new RegistroSalteado(
                        clienteExternoId,
                        $"no se pudo migrar un movimiento de tipo {tipo} fechado {om.Fecha:yyyy-MM-dd}: {ex.Message}"));
                }
            }
        }

        return (migrados, sociosConHistoriaPrevia, salteados);
    }
}

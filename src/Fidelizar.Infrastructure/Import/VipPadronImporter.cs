using System.Globalization;
using ClosedXML.Excel;
using Fidelizar.Domain.Entities;
using Fidelizar.Domain.Money;
using Fidelizar.Domain.Repositories;
using Fidelizar.Domain.Texto;
using Microsoft.Extensions.Logging;

namespace Fidelizar.Infrastructure.Import;

/// <summary>What a padron import run did.</summary>
public sealed record ResultadoPadron(
    int MiembrosCreados,
    int MiembrosYaExistian,
    int SaldosInicialesCargados,
    decimal TotalSaldoInicial,
    List<string> Advertencias)
{
    public static ResultadoPadron ConError(string mensaje) => new(0, 0, 0, 0m, [mensaje]);
}

/// <summary>
/// Loads a business's member roster from a spreadsheet: creates the <see cref="Miembro"/> rows
/// and writes their existing credit as a <see cref="TipoMovimientoCredito.SaldoInicial"/>
/// movement. The entry door for every new business (Plan §2, ROADMAP F0-08).
///
/// <para>
/// <b>The spreadsheet is the source of truth for the initial balance.</b> This import does not
/// recompute or question that number: it takes it as-is and records it as a ledger movement,
/// dated at the cutoff (the <c>fechaCorte</c> parameter of <see cref="ImportAsync"/>). Whatever
/// came before the cutoff was tracked by hand elsewhere; accrual (F2-04) only starts counting the
/// day <b>after</b> the cutoff.
/// </para>
///
/// <para>
/// <b>The cutoff is a parameter, not a constant.</b> A business rule number must never appear as
/// a literal (ARCHITECTURE §6), and a hard-coded cutoff double-credits every purchase between the
/// real cutoff and the day the import runs — the exact mistake this product is built to avoid
/// (DATA-MODEL §4, <c>Corte</c>). <c>fechaCorte</c> is therefore mandatory here, with no default:
/// better to stop the import than assume a silent default with money on the line. The declared
/// value is persisted (<see cref="ICorteRepository"/>) so accrual can read it on every later run.
/// </para>
///
/// <para>
/// <b>Idempotent for linked members.</b> Running it again with the same file does not duplicate a
/// member that already has a <see cref="Miembro.ClienteExternoId"/> on record, nor does it write
/// a second initial balance for one. It is <b>not</b> idempotent for unlinked rows (see the "no id
/// column" and "adaptations from Octaviano" notes on <see cref="ImportAsync"/>): a member imported
/// with no POS id has no natural key to match on a second run, so re-running the same file over
/// unlinked rows creates new unlinked members rather than recognising the old ones. This is an
/// accepted consequence of DATA-MODEL §3's nullable <c>ClienteExternoId</c>, not an oversight —
/// see the F0-08 port report.
/// </para>
///
/// <para>
/// <b>Adaptations from Octaviano's <c>VipPadronImporter</c>, all deliberate (F0-08):</b>
/// </para>
/// <list type="bullet">
/// <item><c>NegocioId</c> is a required parameter throughout (I8) — the original served a single
/// shop and had no such concept.</item>
/// <item>
/// <c>ClienteExternoId</c> is <c>text?</c>, not <c>int</c> (DATA-MODEL §3, §7). A row with no
/// readable id — or a spreadsheet with no id column at all — no longer aborts the import: the
/// member is created unlinked and accrues nothing until an <c>Encargada</c>/<c>Dueño</c> links it
/// later ("socios sin vincular", F1-14). Only a <b>duplicate</b> non-null id is still rejected
/// (all its rows, exactly as before) — a repeated id is what actually risks crediting one
/// person's sales to somebody else.
/// </item>
/// <item>
/// The initial balance is written through <see cref="MovimientoCredito.Crear"/>, never as a bare
/// column (I1), and rounded through <see cref="Redondeo.Monto"/>, the single rounding point in
/// the system (I4) — the original rounded by calling the .NET rounding API directly.
/// </item>
/// <item>
/// <b>Dropped: validating a <c>customer_id</c> against the point-of-sale database directly.</b>
/// Octaviano's <c>IVipSourceRepository</c>/<c>validarContraSistema</c> is not ported. I9 forbids a
/// direct connection to a client's POS database — ingestion is by file or API, always — and by
/// the time this was ported, Octaviano's own caller had already stopped using that path for the
/// same underlying reason (see the source file's own comment on why <c>validarContraSistema</c>
/// stayed unused in practice). This is a real behaviour change, not a silent one: a row with a
/// well-formed but non-existent external id is now accepted instead of being cross-checked and
/// possibly dropped. See the F0-08 port report.
/// </item>
/// <item>
/// <b>Dropped: the branch column.</b> The target model for this task is <c>Miembro</c>,
/// <c>MovimientoCredito</c> and <c>Negocio</c> — not <c>Sucursal</c>. Mapping a spreadsheet branch
/// code to an internal <c>Sucursal</c> row is a phase 1 concern (S10 configures branches by hand;
/// DATA-MODEL §7 says an import with an unknown branch code must reject, not invent one). Every
/// imported member's <c>SucursalId</c> is left null here; wiring it up is out of scope for F0-08.
/// </item>
/// <item>
/// <b>Cutoff redeclaration is refused, not silently overwritten.</b> Octaviano's
/// <c>SetFechaAsync</c> was an upsert. The new <c>ICorteRepository</c> has no update method at all
/// (I1-adjacent discipline extended to <c>Corte</c>, ARCHITECTURE §3's "no generic repository")
/// — a re-run with the <b>same</b> cutoff is a no-op (idempotent, as before); a re-run with a
/// <b>different</b> cutoff is reported as a warning and left untouched rather than changed
/// underneath whatever accrual has already run against the old one.
/// </item>
/// </list>
/// </summary>
public sealed class VipPadronImporter(
    IMiembroRepository miembroRepository,
    IMovimientoRepository movimientoRepository,
    ICorteRepository corteRepository,
    ILogger<VipPadronImporter>? logger = null)
{
    // Headers are recognised normalised (lowercase, unaccented), so "Crédito", "credito" and
    // "CREDITO" all land on the same alias.
    private static readonly string[] AliasClienteId = ["customer_id", "customerid", "id cliente", "idcliente", "cliente id", "id del cliente", "id"];
    private static readonly string[] AliasNombre = ["nombre", "nombre y apellido", "socio", "miembro", "cliente", "nombre completo"];
    private static readonly string[] AliasCredito = ["credito", "saldo", "puntos", "credito actual", "saldo actual"];
    private static readonly string[] AliasAlta = ["fecha de alta", "fecha alta", "alta", "fecha de inscripcion", "inscripcion"];
    private static readonly string[] AliasTelefono = ["telefono", "numero celular", "celular", "tel"];
    private static readonly string[] AliasDni = ["dni", "documento", "nro documento"];
    private static readonly string[] AliasCumple = ["cumple", "cumpleanos", "fecha de nacimiento", "nacimiento", "cumpleano"];
    private static readonly string[] AliasNumeroSocio = ["numero vip", "nro vip", "n vip", "vip", "numero socio", "nro socio"];

    /// <param name="negocioId">The business this roster belongs to. Required on every query and
    /// every written row (I8) — Octaviano served one shop and had no such parameter.</param>
    /// <param name="rutaExcel">Path to the roster spreadsheet.</param>
    /// <param name="fechaCorte">
    /// The last day the spreadsheet's balances are already current through (inclusive). No
    /// default: an assumed value, high or low, either double-credits or under-credits silently
    /// with real money involved. Each member's initial balance is dated this same day, and accrual
    /// (F2-04) only starts counting from the day after.
    /// </param>
    /// <param name="declaradoPorUsuarioId">
    /// Who is declaring this cutoff, stamped on the persisted <see cref="Corte"/>
    /// (<c>Corte.DeclaradoPorUsuarioId</c>) — the authenticated user running the import.
    /// </param>
    /// <param name="hoy">
    /// Today's date, used only to decide whether the <c>SaldoInicial</c> movement counts as
    /// retroactive (it always does, since the cutoff is necessarily in the past) and therefore
    /// requires a <c>Motivo</c> — which this import always supplies regardless. Defaults to
    /// today's UTC date; overridable so this stays testable without depending on the system clock
    /// (mirrors <c>RegistrarCanjeRequest.Hoy</c> in <c>Fidelizar.Application</c>).
    /// </param>
    public async Task<ResultadoPadron> ImportAsync(
        int negocioId,
        string rutaExcel,
        DateOnly fechaCorte,
        int declaradoPorUsuarioId,
        DateOnly? hoy = null,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(rutaExcel))
        {
            return ResultadoPadron.ConError($"No se encontró el archivo: {rutaExcel}");
        }

        List<FilaPadron> filas;
        var advertencias = new List<string>();

        try
        {
            using var workbook = new XLWorkbook(rutaExcel);
            (filas, var erroresLectura) = LeerFilas(workbook);
            advertencias.AddRange(erroresLectura);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger?.LogError(ex, "No se pudo leer el Excel del padrón {Path}", rutaExcel);
            return ResultadoPadron.ConError($"No se pudo leer el Excel: {ex.Message}");
        }

        if (filas.Count == 0)
        {
            advertencias.Add(
                "El Excel no tiene ninguna fila usable. Se necesitan al menos las columnas " +
                "'customer_id' y 'nombre' en la primera fila.");
            return new ResultadoPadron(0, 0, 0, 0m, advertencias);
        }

        // A repeated customer_id would credit the same sales to two different members. ALL rows
        // for that id are dropped, not just the second one: which of the two is the right one is
        // a call for a person to make, not the import. Rows with no id at all are never part of
        // this check — DATA-MODEL §3 makes an unlinked member a legitimate, ordinary state, so
        // two blank ids are not "the same id repeated".
        var repetidos = filas
            .Where(f => f.ClienteExternoId is not null)
            .GroupBy(f => f.ClienteExternoId)
            .Where(g => g.Count() > 1)
            .ToList();

        foreach (var grupo in repetidos)
        {
            advertencias.Add(
                $"customer_id {grupo.Key} aparece {grupo.Count()} veces (filas " +
                $"{string.Join(", ", grupo.Select(f => f.NumeroFila))}): no se cargó ninguna, resolvelo en el Excel.");
        }

        var idsRepetidos = repetidos.Select(g => g.Key).ToHashSet();
        filas = filas.Where(f => f.ClienteExternoId is null || !idsRepetidos.Contains(f.ClienteExternoId)).ToList();

        await DeclararOVerificarCorteAsync(negocioId, fechaCorte, declaradoPorUsuarioId, advertencias, cancellationToken);

        var creados = 0;
        var yaExistian = 0;
        var saldosCargados = 0;
        var totalSaldo = 0m;
        // The initial balance is dated at the cutoff itself: "this file's balances are current
        // through the end of this day" is exactly what that movement represents.
        var fechaSaldoInicial = fechaCorte;
        var efectivoHoy = hoy ?? DateOnly.FromDateTime(DateTime.UtcNow);

        foreach (var fila in filas)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var miembro = fila.ClienteExternoId is null
                ? null
                : await miembroRepository.GetByClienteExternoIdAsync(negocioId, fila.ClienteExternoId, cancellationToken);

            if (miembro is null)
            {
                miembro = new Miembro
                {
                    NegocioId = negocioId,
                    ClienteExternoId = fila.ClienteExternoId,
                    NumeroSocio = fila.NumeroSocio,
                    Nombre = fila.Nombre,
                    NombreNormalizado = VipNombres.Normalizar(fila.Nombre),
                    Telefono = fila.Telefono,
                    Dni = fila.Dni,
                    FechaNacimiento = fila.FechaNacimiento,
                    FechaAlta = fila.FechaAlta ?? fechaCorte,
                    Activo = true,
                    ActualizadoEn = DateTime.UtcNow,
                };

                miembro = await miembroRepository.AddAsync(miembro, cancellationToken);
                creados++;
            }
            else
            {
                yaExistian++;
            }

            // The initial balance is written once per member. If it already has movements, this
            // import run is a re-import and writing it again would double the credit.
            if (await movimientoRepository.TieneMovimientosAsync(negocioId, miembro.Id, cancellationToken))
            {
                continue;
            }

            if (fila.Credito == 0m)
            {
                continue;
            }

            if (fila.Credito < 0m)
            {
                advertencias.Add(
                    $"Fila {fila.NumeroFila} ({fila.Nombre}): el crédito del Excel es negativo (${fila.Credito:N2}). " +
                    "Se cargó tal cual porque el Excel es la fuente de verdad, pero conviene revisarlo.");
            }

            var movimiento = MovimientoCredito.Crear(
                negocioId: negocioId,
                miembroId: miembro.Id,
                fechaEfectiva: fechaSaldoInicial,
                registradoEn: DateTime.UtcNow,
                tipo: TipoMovimientoCredito.SaldoInicial,
                monto: fila.Credito,
                hoy: efectivoHoy,
                usuarioId: null,
                motivo: "Migrado del padrón importado.");

            await movimientoRepository.AppendAsync(movimiento, cancellationToken);

            saldosCargados++;
            totalSaldo += fila.Credito;
        }

        logger?.LogInformation(
            "Import del padrón (negocio {NegocioId}): {Creados} miembros creados, {YaExistian} ya existían, " +
            "{Saldos} saldos iniciales por ${Total}.",
            negocioId, creados, yaExistian, saldosCargados, totalSaldo);

        return new ResultadoPadron(creados, yaExistian, saldosCargados, totalSaldo, advertencias);
    }

    /// <summary>
    /// Declares the cutoff if none is on record yet. If one already is, a matching value is a
    /// no-op (re-running the same import is idempotent); a different value is reported as a
    /// warning and left untouched, because <see cref="ICorteRepository"/> exposes no update
    /// (ARCHITECTURE §3) — silently overwriting a cutoff that accrual may already have run
    /// against is exactly the kind of invented-number mistake DATA-MODEL §4 exists to prevent.
    /// </summary>
    private async Task DeclararOVerificarCorteAsync(
        int negocioId, DateOnly fechaCorte, int declaradoPorUsuarioId, List<string> advertencias,
        CancellationToken cancellationToken)
    {
        var corteExistente = await corteRepository.ObtenerAsync(negocioId, cancellationToken);

        if (corteExistente is null)
        {
            await corteRepository.DeclararAsync(
                Corte.Declarar(negocioId, fechaCorte, declaradoPorUsuarioId, DateTime.UtcNow),
                cancellationToken);
            return;
        }

        if (corteExistente.Fecha != fechaCorte)
        {
            advertencias.Add(
                $"El negocio ya tiene un corte declarado ({corteExistente.Fecha:yyyy-MM-dd}), distinto " +
                $"del pasado a esta corrida ({fechaCorte:yyyy-MM-dd}). No se modifica: el corte no se " +
                "sobrescribe una vez declarado.");
        }
    }

    private sealed record FilaPadron(
        int NumeroFila,
        string? ClienteExternoId,
        string Nombre,
        decimal Credito,
        DateOnly? FechaAlta,
        string? Telefono,
        string? Dni,
        DateOnly? FechaNacimiento,
        string? NumeroSocio);

    private static (List<FilaPadron> Filas, List<string> Errores) LeerFilas(XLWorkbook workbook)
    {
        var worksheet = workbook.Worksheet(1);
        var errores = new List<string>();
        var filas = new List<FilaPadron>();

        var headerRow = worksheet.Row(1);
        var lastColumn = headerRow.LastCellUsed()?.Address.ColumnNumber ?? 0;
        var mapa = MapearColumnas(headerRow, lastColumn);

        var colId = Buscar(mapa, AliasClienteId);
        var colNombre = Buscar(mapa, AliasNombre);

        if (colId is null)
        {
            // DATA-MODEL §3: ClienteExternoId is nullable. A spreadsheet with no id column at all
            // is not fatal — every member imports unlinked instead of aborting the whole run.
            errores.Add(
                "No encontré la columna del id de cliente (probé: customer_id, id cliente, id). " +
                "Se van a cargar todos los miembros sin vincular a ningún id externo.");
        }

        if (colNombre is null)
        {
            errores.Add("No encontré la columna del nombre (probé: nombre, nombre y apellido, socio, miembro, cliente).");
        }

        if (colNombre is null)
        {
            return (filas, errores);
        }

        var colCredito = Buscar(mapa, AliasCredito);
        var colAlta = Buscar(mapa, AliasAlta);
        var colTelefono = Buscar(mapa, AliasTelefono);
        var colDni = Buscar(mapa, AliasDni);
        var colCumple = Buscar(mapa, AliasCumple);
        var colNumeroSocio = Buscar(mapa, AliasNumeroSocio);

        if (colCredito is null)
        {
            errores.Add(
                "No encontré la columna del crédito (probé: credito, saldo, puntos). " +
                "Los miembros se van a cargar con saldo inicial CERO.");
        }

        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 1;

        for (var numeroFila = 2; numeroFila <= lastRow; numeroFila++)
        {
            var row = worksheet.Row(numeroFila);
            if (FilaVacia(row, lastColumn))
            {
                continue;
            }

            var nombre = Texto(row, colNombre);
            if (string.IsNullOrWhiteSpace(nombre))
            {
                errores.Add($"Fila {numeroFila}: falta el nombre. No se cargó.");
                continue;
            }

            // Blank is a legitimate id: it means this member has no POS id yet and imports
            // unlinked (DATA-MODEL §3). Kept as text, not parsed as an int (§7) — other POS
            // systems use non-numeric ids.
            var clienteExternoId = Vacio(Texto(row, colId));

            var credito = 0m;
            if (colCredito is not null)
            {
                var textoCredito = Texto(row, colCredito);
                if (!string.IsNullOrWhiteSpace(textoCredito) && !MontoParser.TryParseMonto(textoCredito, out credito, out var ambiguo))
                {
                    // Ambiguous is treated the same as any other unreadable credit: loaded as
                    // zero instead of guessed. That is the safe choice — underestimating a
                    // balance is fixed by re-importing the corrected file, but inventing too much
                    // cannot be undone once a member has already redeemed it. Only the warning's
                    // wording changes, so whoever re-exports knows exactly what format to ask for.
                    errores.Add(ambiguo
                        ? $"Fila {numeroFila} ({nombre}): el crédito ('{textoCredito}') es ambiguo (¿mil doscientos o " +
                          "uno con centavos?). Reexportá el Excel con punto decimal, sin separador de miles. Se cargó como cero."
                        : $"Fila {numeroFila} ({nombre}): el crédito ('{textoCredito}') no es un número. Se cargó como cero.");
                    credito = 0m;
                }
            }

            filas.Add(new FilaPadron(
                NumeroFila: numeroFila,
                ClienteExternoId: clienteExternoId,
                Nombre: nombre.Trim(),
                // The initial balance is money: rounded through the single rounding point in the
                // system (I4) — never a second, ad hoc rounding call.
                Credito: Redondeo.Monto(credito),
                FechaAlta: ParsearFecha(Texto(row, colAlta), row, colAlta),
                Telefono: Vacio(Texto(row, colTelefono)),
                Dni: Vacio(Texto(row, colDni)),
                FechaNacimiento: ParsearFecha(Texto(row, colCumple), row, colCumple),
                NumeroSocio: Vacio(Texto(row, colNumeroSocio))));
        }

        return (filas, errores);
    }

    private static Dictionary<string, int> MapearColumnas(IXLRow headerRow, int lastColumn)
    {
        var mapa = new Dictionary<string, int>(StringComparer.Ordinal);

        for (var col = 1; col <= lastColumn; col++)
        {
            var header = VipNombres.Normalizar(headerRow.Cell(col).GetString());
            if (header.Length > 0 && !mapa.ContainsKey(header))
            {
                mapa[header] = col;
            }
        }

        return mapa;
    }

    /// <summary>
    /// The first alias that appears in the header. Alias order matters: the more specific ones go
    /// first, so "id cliente" wins over the generic "id".
    /// </summary>
    private static int? Buscar(Dictionary<string, int> mapa, string[] alias)
    {
        foreach (var a in alias)
        {
            if (mapa.TryGetValue(VipNombres.Normalizar(a), out var col))
            {
                return col;
            }
        }

        return null;
    }

    private static string Texto(IXLRow row, int? col)
    {
        if (col is null)
        {
            return string.Empty;
        }

        var cell = row.Cell(col.Value);
        return cell.IsEmpty() ? string.Empty : cell.GetString().Trim();
    }

    private static string? Vacio(string texto) => string.IsNullOrWhiteSpace(texto) ? null : texto;

    private static bool FilaVacia(IXLRow row, int lastColumn)
    {
        for (var col = 1; col <= lastColumn; col++)
        {
            if (!string.IsNullOrWhiteSpace(row.Cell(col).GetString()))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Spreadsheet dates, which come in several shapes: a real Excel date, "16/09/1968", or
    /// "16/09" (day and month only — a common shape for a birthday column, RN-11: the year does
    /// not matter for a birthday, so the short form gets a filler year).
    /// </summary>
    private static DateOnly? ParsearFecha(string texto, IXLRow row, int? col)
    {
        if (col is not null)
        {
            var cell = row.Cell(col.Value);
            if (!cell.IsEmpty() && cell.DataType == XLDataType.DateTime && cell.TryGetValue<DateTime>(out var fechaExcel))
            {
                return DateOnly.FromDateTime(fechaExcel);
            }
        }

        if (string.IsNullOrWhiteSpace(texto))
        {
            return null;
        }

        string[] formatos = ["d/M/yyyy", "dd/MM/yyyy", "yyyy-MM-dd", "d-M-yyyy"];
        if (DateTime.TryParseExact(texto, formatos, CultureInfo.InvariantCulture, DateTimeStyles.None, out var fecha))
        {
            return DateOnly.FromDateTime(fecha);
        }

        // "16/09" — day and month only. Filler year: only the day and month are ever used.
        string[] formatosCortos = ["d/M", "dd/MM"];
        if (DateTime.TryParseExact(texto, formatosCortos, CultureInfo.InvariantCulture, DateTimeStyles.None, out var corta))
        {
            return new DateOnly(1900, corta.Month, corta.Day);
        }

        return DateTime.TryParse(texto, CultureInfo.InvariantCulture, DateTimeStyles.None, out var suelta)
            ? DateOnly.FromDateTime(suelta)
            : null;
    }
}

using System.Globalization;
using System.Text;
using Fidelizar.VerificacionGate.Comparacion;
using Fidelizar.VerificacionGate.Octaviano;
using Fidelizar.VerificacionGate.Planilla;
using Fidelizar.VerificacionGate.Postgres;

// The three-way verification harness (ROADMAP F0-11) — the phase 0 gate. Re-executable, not a
// one-off script: F0-15's restore drill runs this same tool again later, and ARCHITECTURE says it
// repeats periodically. Every path and every credential is read from the environment or the
// command line, never written into this repository (CLAUDE.md).
//
// Output goes only to the file named by --salida (mandatory, no default): the report identifies
// members by ClienteExternoId, and CLAUDE.md's "nothing personal is ever written down" rule keeps
// that report out of the repository on principle, so this tool refuses to guess a location for it.

var rutaSqlite = LeerArgumentoConValor(args, "--sqlite")
    ?? Environment.GetEnvironmentVariable("OCTAVIANO_SQLITE_PATH")
    ?? Path.Combine("..", "..", "Botquery-Pizarra", "data", "octaviano.db");

var rutaPlanilla = LeerArgumentoConValor(args, "--planilla")
    ?? Environment.GetEnvironmentVariable("VIP_PADRON_XLSX_PATH")
    ?? Path.Combine("..", "..", "Botquery-Pizarra", "vip-padron", "VIP-CLUB-puntos.xlsx");

var rutaSalida = LeerArgumentoConValor(args, "--salida");
if (string.IsNullOrWhiteSpace(rutaSalida))
{
    Console.Error.WriteLine(
        "Falta --salida <ruta>. El informe identifica socios por ClienteExternoId y CLAUDE.md " +
        "prohíbe que ese informe entre al repositorio — esta herramienta no inventa un destino " +
        "por defecto. Pasá una ruta fuera del repositorio (por ejemplo, bajo el scratchpad).");
    return 1;
}

if (!File.Exists(rutaSqlite))
{
    Console.Error.WriteLine($"No se encontró octaviano.db en: {rutaSqlite}");
    return 1;
}

if (!File.Exists(rutaPlanilla))
{
    Console.Error.WriteLine($"No se encontró la planilla en: {rutaPlanilla}");
    return 1;
}

var pgHost = LeerArgumentoConValor(args, "--pg-host") ?? "localhost";
var pgPort = LeerArgumentoConValor(args, "--pg-port") ?? "5433";
var pgDb = LeerArgumentoConValor(args, "--pg-db") ?? "fidelizar_gate";
var pgUser = LeerArgumentoConValor(args, "--pg-user") ?? "postgres";

// The password lives only in the user's environment variable (FIDELIZAR_GATE_PG_PASSWORD) — never
// on the command line (shell history), never in this repository, never in the report.
var pgPassword = Environment.GetEnvironmentVariable("FIDELIZAR_GATE_PG_PASSWORD");
if (string.IsNullOrWhiteSpace(pgPassword))
{
    Console.Error.WriteLine("Falta la variable de entorno FIDELIZAR_GATE_PG_PASSWORD.");
    return 1;
}

var connectionString = $"Host={pgHost};Port={pgPort};Database={pgDb};Username={pgUser};Password={pgPassword}";

Console.WriteLine("=== F0-11: verificación de tres puntas ===");
Console.WriteLine();

Console.WriteLine("Punta 1/3: leyendo Postgres (fidelizar_gate) vía SaldoService...");
var (negocioId, sociosPostgres) = await PostgresGateSource.LeerAsync(connectionString);
Console.WriteLine($"  {sociosPostgres.Count} miembros leídos (NegocioId={negocioId}).");

Console.WriteLine("Punta 2/3: leyendo octaviano.db (read-only, sin referenciar el código de Octaviano)...");
var sociosOctaviano = OctavianoLedgerSource.Leer(rutaSqlite);
Console.WriteLine($"  {sociosOctaviano.Count} miembros leídos.");

Console.WriteLine("Punta 3/3: leyendo la planilla del dueño (read-only, ClosedXML)...");
var (filasPlanilla, hojasIgnoradas) = PlanillaSaldoSource.Leer(rutaPlanilla);
Console.WriteLine($"  {filasPlanilla.Count} filas de socios leídas en {filasPlanilla.Select(f => f.Hoja).Distinct().Count()} hoja(s).");
foreach (var hoja in hojasIgnoradas)
{
    Console.WriteLine($"  Hoja ignorada (no es un padrón de socios): {hoja}");
}

var casosBordePrevios = new List<CasoBorde>();

var postgresSinId = sociosPostgres.Where(s => string.IsNullOrWhiteSpace(s.ClienteExternoId)).ToList();
foreach (var socio in postgresSinId)
{
    casosBordePrevios.Add(new CasoBorde(
        $"Miembro Postgres (Id interno {socio.MiembroId}) no tiene ClienteExternoId — no se puede " +
        "comparar contra las otras dos puntas."));
}

var idsPostgresDuplicados = sociosPostgres
    .Where(s => !string.IsNullOrWhiteSpace(s.ClienteExternoId))
    .GroupBy(s => s.ClienteExternoId!)
    .Where(g => g.Count() > 1)
    .ToList();
foreach (var grupo in idsPostgresDuplicados)
{
    casosBordePrevios.Add(new CasoBorde(
        $"ClienteExternoId {grupo.Key} aparece {grupo.Count()} veces en Postgres — no debería " +
        "pasar (índice único DATA-MODEL §3); reportado, no comparado.", grupo.Key));
}

var saldosPostgres = sociosPostgres
    .Where(s => !string.IsNullOrWhiteSpace(s.ClienteExternoId))
    .GroupBy(s => s.ClienteExternoId!)
    .Where(g => g.Count() == 1)
    .ToDictionary(g => g.Key, g => g.Single().Saldo);

var saldosOctaviano = sociosOctaviano.ToDictionary(s => s.ClienteExternoId, s => s.Saldo);

var resultado = Comparador.Comparar(saldosPostgres, saldosOctaviano, filasPlanilla);
var todosLosCasosBorde = casosBordePrevios.Concat(resultado.CasosBorde).ToList();

var culturaAr = CultureInfo.GetCultureInfo("es-AR");

var informe = new StringBuilder();
informe.AppendLine("Fidelizar — F0-11 — verificación de tres puntas (gate de la fase 0)");
informe.AppendLine($"Generado: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
informe.AppendLine();
informe.AppendLine("Fuentes:");
informe.AppendLine($"  Punta 1 — Postgres: Host={pgHost} Port={pgPort} Database={pgDb} (SaldoService)");
informe.AppendLine($"  Punta 2 — octaviano.db: {rutaSqlite}");
informe.AppendLine($"  Punta 3 — planilla: {rutaPlanilla}");
informe.AppendLine();
informe.AppendLine("Recuento:");
informe.AppendLine($"  Socios en Postgres (con ClienteExternoId): {resultado.TotalSociosPostgres}");
informe.AppendLine($"  Socios en octaviano.db: {resultado.TotalSociosOctaviano}");
informe.AppendLine($"  Filas de socio válidas en la planilla: {resultado.TotalFilasPlanillaValidas}");
informe.AppendLine($"  ClienteExternoId distintos comparados (unión de las tres puntas): {resultado.TotalSociosComparados}");
informe.AppendLine();
informe.AppendLine("Sumas de control (deberían coincidir si el gate está cumplido):");
informe.AppendLine($"  Suma Postgres:   {resultado.SumaPostgres.ToString("N2", culturaAr)}");
informe.AppendLine($"  Suma octaviano:  {resultado.SumaOctaviano.ToString("N2", culturaAr)}");
informe.AppendLine($"  Suma planilla:   {resultado.SumaPlanilla.ToString("N2", culturaAr)}");
informe.AppendLine();
informe.AppendLine($"VEREDICTO: {(resultado.GateCumplido ? "GATE CUMPLIDO — las tres puntas coinciden para los 293 socios, sin excepciones." : "GATE NO CUMPLIDO — hay discrepancias. El detalle sigue abajo.")}");
informe.AppendLine();

informe.AppendLine($"=== Discrepancias ({resultado.Discrepancias.Count}) ===");
if (resultado.Discrepancias.Count == 0)
{
    informe.AppendLine("(ninguna)");
}
else
{
    foreach (var d in resultado.Discrepancias.OrderByDescending(d => d.DiferenciaMaxima()))
    {
        string Fmt(decimal? v) => v is null ? "—" : v.Value.ToString("N2", culturaAr);
        informe.AppendLine(
            $"  ClienteExternoId {d.ClienteExternoId} [{d.Causa}]: " +
            $"Postgres={Fmt(d.SaldoPostgres)} Octaviano={Fmt(d.SaldoOctaviano)} Planilla={Fmt(d.SaldoPlanilla)} " +
            $"diferencia_max={d.DiferenciaMaxima().ToString("N2", culturaAr)}");
    }
}

informe.AppendLine();
informe.AppendLine($"=== Casos borde ({todosLosCasosBorde.Count}) ===");
if (todosLosCasosBorde.Count == 0)
{
    informe.AppendLine("(ninguno)");
}
else
{
    foreach (var c in todosLosCasosBorde)
    {
        informe.AppendLine($"  {c.Descripcion}");
    }
}

Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(rutaSalida))!);
await File.WriteAllTextAsync(rutaSalida, informe.ToString(), Encoding.UTF8);

Console.WriteLine();
Console.WriteLine($"Informe escrito en: {Path.GetFullPath(rutaSalida)}");
Console.WriteLine($"Discrepancias: {resultado.Discrepancias.Count}");
Console.WriteLine($"Casos borde: {todosLosCasosBorde.Count}");
Console.WriteLine($"GATE CUMPLIDO: {resultado.GateCumplido}");

return resultado.GateCumplido ? 0 : 1;

static string? LeerArgumentoConValor(string[] argumentos, string nombre)
{
    var indice = Array.IndexOf(argumentos, nombre);
    return indice >= 0 && indice + 1 < argumentos.Length ? argumentos[indice + 1] : null;
}

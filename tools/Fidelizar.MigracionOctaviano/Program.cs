using System.Globalization;
using Fidelizar.Infrastructure.Persistence;
using Fidelizar.Infrastructure.Repositories;
using Fidelizar.MigracionOctaviano.Destino;
using Fidelizar.MigracionOctaviano.Migracion;
using Fidelizar.MigracionOctaviano.Origen;
using Microsoft.EntityFrameworkCore;

// One-off migration tool (ROADMAP F0-09): Octaviano SQLite → Fidelizar Postgres. Never part of
// the product's own composition root (Fidelizar.Api) — CLAUDE.md's condition that
// Microsoft.Data.Sqlite stays isolated in this project is enforced by this being the only
// project that references it.
//
// The connection string and the path to octaviano.db are read from the environment / the command
// line, never written into this repository (CLAUDE.md: no real connection string or secret is
// ever committed, not even a placeholder-looking one).

var modoEsquema = args.Contains("--esquema", StringComparer.OrdinalIgnoreCase);

var rutaSqlite = LeerArgumentoConValor(args, "--sqlite")
    ?? Environment.GetEnvironmentVariable("OCTAVIANO_SQLITE_PATH")
    // Same relative path CLAUDE.md itself documents as the authorized location for F0-09/F0-11.
    ?? Path.Combine("..", "..", "Botquery-Pizarra", "data", "octaviano.db");

if (!File.Exists(rutaSqlite))
{
    Console.Error.WriteLine($"No se encontró octaviano.db en: {rutaSqlite}");
    return 1;
}

var origen = new SqliteOctavianoSource(rutaSqlite);

if (modoEsquema)
{
    await ImprimirEsquemaAsync(origen);
    return 0;
}

// ARCHITECTURE §6 forbids a business rule number as a literal in code. RN-01 (accrual
// percentage) and RN-06 (monthly target) are per-business configuration — this tool never
// guesses them, exactly like Corte is not a constant (F0-07). Required, no default: a missing
// value fails loudly here instead of silently seeding a wrong ConfiguracionPrograma with real
// money on the line. The example in the message below is an arbitrary placeholder fraction, not
// RN-01's actual value — the caller supplies the real number, this tool never states it.
var textoPorcentaje = LeerArgumentoConValor(args, "--porcentaje-acumulacion");
if (textoPorcentaje is null ||
    !decimal.TryParse(textoPorcentaje, NumberStyles.Number, CultureInfo.InvariantCulture, out var porcentajeAcumulacion))
{
    Console.Error.WriteLine(
        "Falta (o no es un número válido) --porcentaje-acumulacion. Es RN-01, configuración por " +
        "negocio (ARCHITECTURE §6) — esta herramienta no lo inventa. Ejemplo de formato (no es el " +
        "valor real): --porcentaje-acumulacion 0.05");
    return 1;
}

// RN-06: opcional a propósito. ConfiguracionPrograma.ObjetivoMensual es nullable — null significa
// que el programa no tiene objetivo mensual (DATA-MODEL §1), no un valor que falte cargar.
decimal? objetivoMensual = null;
var textoObjetivo = LeerArgumentoConValor(args, "--objetivo-mensual");
if (textoObjetivo is not null)
{
    if (!decimal.TryParse(textoObjetivo, NumberStyles.Number, CultureInfo.InvariantCulture, out var valorObjetivo))
    {
        Console.Error.WriteLine($"--objetivo-mensual no es un número válido: '{textoObjetivo}'.");
        return 1;
    }

    objetivoMensual = valorObjetivo;
}

var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine(
        "Falta la variable de entorno ConnectionStrings__DefaultConnection con la cadena de " +
        "conexión a Postgres. No hay un valor por defecto: CLAUDE.md prohíbe que una cadena de " +
        "conexión real quede en el repositorio, incluso como placeholder.");
    return 1;
}

var options = new DbContextOptionsBuilder<FidelizarDbContext>()
    .UseNpgsql(connectionString)
    .Options;

await using var dbContext = new FidelizarDbContext(options);

Console.WriteLine("Aplicando migraciones pendientes...");
await dbContext.Database.MigrateAsync();

var migrador = new MigradorOctaviano(
    origen,
    new NegocioRepositoryEf(dbContext),
    new ConfiguracionProgramaRepositoryEf(dbContext),
    new MiembroRepository(dbContext),
    new MovimientoRepository(dbContext),
    new ConsentimientoRepositoryEf(dbContext),
    new CorteRepository(dbContext));

Console.WriteLine("Migrando...");
var resultado = await migrador.EjecutarAsync(porcentajeAcumulacion, objetivoMensual, DateTime.UtcNow);

ImprimirResultado(resultado);

return 0;

static string? LeerArgumentoConValor(string[] args, string nombre)
{
    var indice = Array.IndexOf(args, nombre);
    return indice >= 0 && indice + 1 < args.Length ? args[indice + 1] : null;
}

static async Task ImprimirEsquemaAsync(IOctavianoSource origen)
{
    var tablas = await origen.LeerEsquemaAsync();
    Console.WriteLine("Esquema de octaviano.db (solo nombres y tipos de columna, nunca contenido):");
    foreach (var tabla in tablas)
    {
        Console.WriteLine($"  {tabla.Nombre}:");
        foreach (var columna in tabla.Columnas)
        {
            Console.WriteLine($"    {columna.Nombre} {columna.Tipo}");
        }
    }
}

static void ImprimirResultado(ResultadoMigracion resultado)
{
    Console.WriteLine();
    Console.WriteLine("=== Resultado de la migración ===");
    Console.WriteLine($"NegocioId: {resultado.NegocioId}");
    Console.WriteLine($"ConfiguracionId: {resultado.ConfiguracionId}");
    Console.WriteLine($"Corte: {(resultado.CorteFecha is { } fecha ? fecha.ToString("yyyy-MM-dd") : "(sin corte en el origen)")}");
    if (resultado.CorteAdvertencia is not null)
    {
        Console.WriteLine($"  Advertencia: {resultado.CorteAdvertencia}");
    }

    Console.WriteLine($"Miembros creados: {resultado.MiembrosCreados}");
    Console.WriteLine($"Miembros que ya existían: {resultado.MiembrosYaExistian}");
    Console.WriteLine($"Miembros salteados: {resultado.MiembrosSalteados.Count}");
    foreach (var salteado in resultado.MiembrosSalteados)
    {
        Console.WriteLine($"  - ClienteExternoId {salteado.ClienteExternoId}: {salteado.Motivo}");
    }

    Console.WriteLine($"Movimientos migrados: {resultado.MovimientosMigrados}");
    Console.WriteLine($"Socios cuyos movimientos ya estaban migrados (no se reprocesaron): {resultado.SociosConMovimientosYaMigrados}");
    Console.WriteLine($"Movimientos salteados: {resultado.MovimientosSalteados.Count}");
    foreach (var salteado in resultado.MovimientosSalteados)
    {
        Console.WriteLine($"  - ClienteExternoId {salteado.ClienteExternoId}: {salteado.Motivo}");
    }

    Console.WriteLine($"Consentimientos creados: {resultado.ConsentimientosCreados}");
    Console.WriteLine($"Consentimientos que ya existían: {resultado.ConsentimientosYaExistian}");
}

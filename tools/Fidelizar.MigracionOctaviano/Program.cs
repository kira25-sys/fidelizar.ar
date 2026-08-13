using Fidelizar.Infrastructure.Persistence;
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
    new Fidelizar.Infrastructure.Repositories.MiembroRepository(dbContext),
    new Fidelizar.Infrastructure.Repositories.MovimientoRepository(dbContext),
    new ConsentimientoRepositoryEf(dbContext));

Console.WriteLine("Migrando...");
var resultado = await migrador.EjecutarAsync(DateTime.UtcNow);

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

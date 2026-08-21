using System.Globalization;
using Fidelizar.Infrastructure.Persistence;
using Fidelizar.Infrastructure.Repositories;
using Fidelizar.Infrastructure.Security;
using Fidelizar.SeederDesarrollo.Configuracion;
using Fidelizar.SeederDesarrollo.Datos;
using Fidelizar.SeederDesarrollo.Destino;
using Fidelizar.SeederDesarrollo.Sembrado;
using Microsoft.EntityFrameworkCore;

// Development seeder (chore/seeder-de-desarrollo). Solves the bootstrap deadlock: POST /usuarios
// is [Authorize(Policy = DuenoOnly)], so creating the first Dueño requires already being one, and
// S5's consent texts cannot render without a Negocio carrying razón social, CUIT and domicilio.
//
// It lives in tools/ and not in Fidelizar.Api on purpose (owner's decision): not one line of
// seeding code belongs in something that will one day run in production. Nothing in src/ knows
// this project exists.
//
// Everything it writes is invented (see Datos/DatosInventados.cs). The account password comes from
// FIDELIZAR_SEED_PASSWORD and the connection string from ConnectionStrings__DefaultConnection —
// neither has a default, neither is ever printed, and neither is ever written into this repository
// (CLAUDE.md).

if (args.Contains("--ayuda") || args.Contains("--help") || args.Contains("-h"))
{
    ImprimirAyuda();
    return 0;
}

var (opciones, error) = LecturaDeOpciones.Leer(args, Environment.GetEnvironmentVariable);
if (opciones is null)
{
    Console.Error.WriteLine(error);
    return 1;
}

Console.WriteLine("=== Seeder de desarrollo — datos inventados, nunca reales ===");
Console.WriteLine();
Console.WriteLine("Destino:");
Console.WriteLine($"  Host:     {opciones.Host}:{opciones.Puerto}");
Console.WriteLine($"  Base:     {opciones.BaseDeDatos}");
Console.WriteLine($"  Usuario:  {opciones.UsuarioPostgres}");
Console.WriteLine();

var options = new DbContextOptionsBuilder<FidelizarDbContext>()
    .UseNpgsql(opciones.CadenaConexion)
    .Options;

await using var dbContext = new FidelizarDbContext(options);

var estado = new EstadoBaseRepositoryEf(dbContext);
var negocios = new NegocioSeederRepositoryEf(dbContext);

// The emptiness check runs BEFORE any migration is applied. Applying migrations is itself a write,
// and a write to the wrong database is exactly what this tool exists to make impossible.
var tieneEsquema = await estado.TieneEsquemaAsync();
var conteo = tieneEsquema ? await estado.ContarAsync() : ConteoBase.Vacia;
var negocioExistente = tieneEsquema ? await negocios.ObtenerPrimeroAsync() : null;

var decision = Guardas.DecidirSobreBase(conteo, negocioExistente, opciones.PermitirBaseNoVacia);
Console.WriteLine(decision.Mensaje);
Console.WriteLine();

if (!decision.Continuar)
{
    return 1;
}

if (!tieneEsquema || (await dbContext.Database.GetPendingMigrationsAsync()).Any())
{
    Console.WriteLine("Aplicando migraciones pendientes...");
    await dbContext.Database.MigrateAsync();
}

var ahoraUtc = DateTime.UtcNow;
var hoy = DateOnly.FromDateTime(ahoraUtc);

// No --corte: an invented business gets an invented cutoff, six months back, so the seeded history
// has room to sit between the cutoff and today. F0-07's "a cutoff is never a constant" protects a
// real import from double-crediting real purchases — a risk that does not exist for six invented
// members — and --corte is there for when the caller wants a specific date anyway.
var corte = opciones.Corte ?? new DateOnly(hoy.Year, hoy.Month, 1).AddMonths(-6);

var sembrador = new Sembrador(
    negocios,
    new SucursalRepository(dbContext),
    new UsuarioRepository(dbContext),
    new CorteRepository(dbContext),
    new MiembroRepository(dbContext),
    new MovimientoRepository(dbContext),
    new ConsentimientoRepository(dbContext),
    new IdentityPasswordHasher());

Console.WriteLine("Sembrando...");
var resultado = await sembrador.EjecutarAsync(opciones.PasswordSembrada, corte, hoy, ahoraUtc);

ImprimirResultado(resultado);

return 0;

static void ImprimirResultado(ResultadoSembrado resultado)
{
    var culturaAr = CultureInfo.GetCultureInfo("es-AR");

    Console.WriteLine();
    Console.WriteLine("=== Resultado ===");
    Console.WriteLine($"NegocioId: {resultado.NegocioId} ({(resultado.NegocioCreado ? "creado ahora" : "ya existía")})");
    Console.WriteLine($"Corte: {resultado.Corte:yyyy-MM-dd} ({(resultado.CorteDeclarado ? "declarado ahora" : "ya estaba declarado")})");
    Console.WriteLine($"Sucursales: {resultado.SucursalesCreadas} creadas, {resultado.SucursalesYaExistian} ya existían");
    Console.WriteLine($"Usuarios: {resultado.UsuariosCreados} creados, {resultado.UsuariosYaExistian} ya existían");
    Console.WriteLine($"Miembros: {resultado.MiembrosCreados} creados, {resultado.MiembrosYaExistian} ya existían");
    Console.WriteLine(
        $"Consentimientos (DatosPersonales): {resultado.ConsentimientosCreados} creados, " +
        $"{resultado.ConsentimientosYaExistian} ya existían");
    Console.WriteLine(
        $"Movimientos: {resultado.MovimientosCreados} creados, " +
        $"{resultado.MiembrosConHistorialPrevio} socio(s) ya tenían historial y no se reprocesaron");

    Console.WriteLine();
    Console.WriteLine("Saldos resultantes (SUM(Monto), leídos de la base — no de la ficha inventada):");
    foreach (var saldo in resultado.Saldos)
    {
        Console.WriteLine($"  {saldo.ClienteExternoId}  {saldo.Nombre,-20}  $ {saldo.Saldo.ToString("N2", culturaAr),12}");
    }

    Console.WriteLine();
    Console.WriteLine("Cuentas creadas (todas con el valor de FIDELIZAR_SEED_PASSWORD de esta sesión):");
    foreach (var usuario in DatosInventados.Usuarios)
    {
        Console.WriteLine($"  {usuario.Rol,-10} {usuario.Email}");
    }

    Console.WriteLine();
    Console.WriteLine("Listo. Entrá con la cuenta Dueño y desde ahí creá los usuarios reales.");
    Console.WriteLine($"Acordate de borrar {LecturaDeOpciones.VariablePassword} de esta sesión al terminar.");
}

static void ImprimirAyuda()
{
    Console.WriteLine($"""
        Seeder de desarrollo — siembra un Negocio, sucursales, usuarios y socios INVENTADOS
        para poder entrar a la aplicación y ver algo en las pantallas.

        Nunca escribe datos reales y nunca lee la base del gate.

        Variables de entorno (obligatorias, sin valor por defecto, nunca impresas):
          {LecturaDeOpciones.VariableCadenaConexion}
              Cadena de conexión a la base de DESARROLLO.
          {LecturaDeOpciones.VariablePassword}
              Contraseña de las cuentas sembradas. Ponela solo en esta sesión y borrala al terminar.

        Parámetros:
          --base-esperada <nombre>     Obligatorio. El nombre de base contra el que creés estar
                                       corriendo; tiene que coincidir con el que dice la cadena
                                       de conexión, o no se escribe nada.
          --corte <yyyy-MM-dd>         Opcional. Fecha de corte a declarar. Por defecto, el día 1
                                       de seis meses atrás.
          --permitir-base-no-vacia     Opcional. Sigue aunque la base ya tenga datos que no sembró
                                       esta herramienta. Nunca borra ni modifica nada existente.
          --ayuda                      Esto.

        Bases prohibidas, sin bandera que lo habilite: cualquier nombre que contenga
        'gate', 'prod' u 'octaviano'.

        Correrlo dos veces es seguro: es idempotente y solo completa lo que falte.
        """);
}

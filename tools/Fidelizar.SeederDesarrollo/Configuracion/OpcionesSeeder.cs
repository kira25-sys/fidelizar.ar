using System.Globalization;
using Npgsql;

namespace Fidelizar.SeederDesarrollo.Configuracion;

/// <summary>
/// Everything the seeder needs, already read and already validated.
///
/// <para>
/// <b>A class, not a record, and that is deliberate.</b> A record's compiler-generated
/// <c>ToString()</c> prints every member, so a single <c>Console.WriteLine(opciones)</c> anywhere
/// would print the seeded accounts' password and the database password inside the connection
/// string. <c>Program.cs</c> prints <see cref="Host"/>, <see cref="Puerto"/>,
/// <see cref="BaseDeDatos"/> and <see cref="UsuarioPostgres"/> one by one instead, and never
/// prints <see cref="CadenaConexion"/> or <see cref="PasswordSembrada"/> at all (CLAUDE.md: a
/// secret never reaches a log, a commit or a report).
/// </para>
/// </summary>
public sealed class OpcionesSeeder
{
    internal OpcionesSeeder(
        string cadenaConexion,
        string host,
        string puerto,
        string baseDeDatos,
        string usuarioPostgres,
        string passwordSembrada,
        DateOnly? corte,
        bool permitirBaseNoVacia)
    {
        CadenaConexion = cadenaConexion;
        Host = host;
        Puerto = puerto;
        BaseDeDatos = baseDeDatos;
        UsuarioPostgres = usuarioPostgres;
        PasswordSembrada = passwordSembrada;
        Corte = corte;
        PermitirBaseNoVacia = permitirBaseNoVacia;
    }

    /// <summary>Holds the database password. Never printed, never written anywhere.</summary>
    public string CadenaConexion { get; }

    public string Host { get; }

    public string Puerto { get; }

    public string BaseDeDatos { get; }

    public string UsuarioPostgres { get; }

    /// <summary>
    /// The password every seeded account gets, read from <c>FIDELIZAR_SEED_PASSWORD</c>. Hashed
    /// with <c>Fidelizar.Infrastructure</c>'s own <c>IdentityPasswordHasher</c> before it touches
    /// the database, so the seeded accounts log in through the real <c>POST /auth/login</c> path
    /// and this tool never invents a hashing scheme of its own.
    /// </summary>
    public string PasswordSembrada { get; }

    /// <summary>The cutoff to declare, when the caller passed <c>--corte</c>.</summary>
    public DateOnly? Corte { get; }

    public bool PermitirBaseNoVacia { get; }
}

/// <summary>
/// Reads the command line and the environment into <see cref="OpcionesSeeder"/>, or into a
/// message in Spanish explaining exactly what is missing. A pure function of its two arguments —
/// nothing here touches the real environment or the real clock — so every refusal below is
/// covered by a test without setting a single environment variable.
/// </summary>
public static class LecturaDeOpciones
{
    public const string VariablePassword = "FIDELIZAR_SEED_PASSWORD";

    public const string VariableCadenaConexion = "ConnectionStrings__DefaultConnection";

    public static (OpcionesSeeder? Opciones, string? Error) Leer(
        string[] args, Func<string, string?> leerVariableDeEntorno)
    {
        // Checked first, before anything else: a run that would fail on the password should fail
        // before it has looked at, let alone opened, a database.
        var password = leerVariableDeEntorno(VariablePassword);
        if (string.IsNullOrWhiteSpace(password))
        {
            return (null, $"""
                Falta la variable de entorno {VariablePassword}.

                Es la contraseña con la que se crean las cuentas sembradas (Dueño, Encargada y
                Cajero). No hay valor por defecto y esta herramienta no inventa uno: una
                contraseña por defecto en una herramienta de desarrollo termina, tarde o
                temprano, en una base que no es de desarrollo.

                Ponela solo en la sesión donde vas a correr el seeder y borrala al terminar
                (ver tools/Fidelizar.SeederDesarrollo/README.md). No se crea nada.
                """);
        }

        var cadenaConexion = leerVariableDeEntorno(VariableCadenaConexion);
        if (string.IsNullOrWhiteSpace(cadenaConexion))
        {
            return (null, $"""
                Falta la variable de entorno {VariableCadenaConexion} con la cadena de conexión a
                Postgres. No hay valor por defecto: CLAUDE.md prohíbe que una cadena de conexión
                real quede escrita en el repositorio, incluso como placeholder.
                """);
        }

        NpgsqlConnectionStringBuilder constructor;
        try
        {
            constructor = new NpgsqlConnectionStringBuilder(cadenaConexion);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException)
        {
            return (null, $"{VariableCadenaConexion} no es una cadena de conexión de Postgres válida.");
        }

        var baseDeDatos = constructor.Database;
        if (string.IsNullOrWhiteSpace(baseDeDatos))
        {
            return (null, $"{VariableCadenaConexion} no nombra ninguna base de datos (falta 'Database=').");
        }

        // The trap this whole tool is designed around: the environment variable pointing at the
        // wrong database. Requiring the caller to *state* the database they mean, and comparing
        // that against the one the connection string actually names, is what turns a silent
        // disaster into a refusal. See Guardas for the second belt.
        var baseEsperada = LeerArgumentoConValor(args, "--base-esperada");
        if (string.IsNullOrWhiteSpace(baseEsperada))
        {
            return (null, $"""
                Falta --base-esperada <nombre>.

                Es obligatorio y no tiene valor por defecto: tenés que decir contra qué base creés
                que estás corriendo, para que la herramienta pueda comparar eso con la base que
                {VariableCadenaConexion} nombra de verdad. Una variable de entorno mal puesta ya
                hizo que la API se conectara a la base equivocada — este parámetro existe para que
                eso no pueda terminar en una escritura.

                Ejemplo: --base-esperada fidelizar_dev
                """);
        }

        if (!string.Equals(baseDeDatos, baseEsperada, StringComparison.OrdinalIgnoreCase))
        {
            return (null, $"""
                La base que nombra {VariableCadenaConexion} no es la que pasaste en --base-esperada.

                  --base-esperada:            {baseEsperada}
                  {VariableCadenaConexion}:   {baseDeDatos} (en {constructor.Host}:{constructor.Port})

                No se escribe nada. Revisá la variable de entorno de esta sesión antes de volver a
                intentar.
                """);
        }

        if (Guardas.NombreProhibido(baseDeDatos) is { } motivo)
        {
            return (null, motivo);
        }

        DateOnly? corte = null;
        var textoCorte = LeerArgumentoConValor(args, "--corte");
        if (textoCorte is not null)
        {
            if (!DateOnly.TryParseExact(textoCorte, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var corteLeido))
            {
                return (null, $"--corte no es una fecha válida en formato yyyy-MM-dd: '{textoCorte}'.");
            }

            corte = corteLeido;
        }

        var opciones = new OpcionesSeeder(
            cadenaConexion,
            constructor.Host ?? "(sin host)",
            constructor.Port.ToString(CultureInfo.InvariantCulture),
            baseDeDatos,
            constructor.Username ?? "(sin usuario)",
            password,
            corte,
            args.Contains("--permitir-base-no-vacia", StringComparer.OrdinalIgnoreCase));

        return (opciones, null);
    }

    private static string? LeerArgumentoConValor(string[] args, string nombre)
    {
        var indice = Array.FindIndex(args, a => string.Equals(a, nombre, StringComparison.OrdinalIgnoreCase));
        return indice >= 0 && indice + 1 < args.Length ? args[indice + 1] : null;
    }
}

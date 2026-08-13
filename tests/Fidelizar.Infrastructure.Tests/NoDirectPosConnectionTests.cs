namespace Fidelizar.Infrastructure.Tests;

/// <summary>
/// I9 — no direct connection to a client's POS database, ever; ingestion is by file or API only
/// (ARCHITECTURE §4). Inherited from Octaviano's <c>NoDirectMySqlAccessTests</c> policy (Plan
/// §13) and reimplemented here against this codebase.
///
/// Scans both the source tree (for a POS-typical database client type, or a non-Postgres
/// connection-string shape) and every <c>.csproj</c> under <c>src/</c> (for a POS-typical database
/// client package), so this fails the moment either one creeps in — not only when someone
/// remembers to update this test by hand. Only <c>appsettings.json</c> and
/// <c>appsettings.Development.json</c> are read (CLAUDE.md forbids reading
/// <c>appsettings.Production.json</c>, which does not exist in this repository — this test does
/// not open it even if it later does).
/// </summary>
public class NoDirectPosConnectionTests
{
    // Client types/namespaces for a POS-typical database (MySQL, SQL Server, Oracle, ODBC/OleDb).
    // Npgsql — Postgres, this product's own database (ARCHITECTURE §2) — is deliberately absent.
    private static readonly string[] ClientesDeBaseProhibidos =
    [
        "MySqlConnection", "MySqlConnector", "MySql.Data",
        "SqlConnection", "System.Data.SqlClient", "Microsoft.Data.SqlClient",
        "OracleConnection", "OdbcConnection", "OleDbConnection",
    ];

    private static readonly string[] PaquetesDeBaseProhibidos =
    [
        "MySqlConnector", "MySql.Data", "System.Data.SqlClient", "Microsoft.Data.SqlClient",
        "Oracle.ManagedDataAccess", "System.Data.Odbc", "System.Data.OleDb",
    ];

    [Fact]
    public void Ningun_archivo_de_codigo_bajo_src_referencia_un_cliente_de_base_de_datos_de_POS()
    {
        var srcRoot = FindSolutionRoot("src");

        var ofensores = Directory
            .EnumerateFiles(srcRoot, "*.cs", SearchOption.AllDirectories)
            .Where(EsCodigoFuenteDelProducto)
            .SelectMany(path => ClientesDeBaseProhibidos
                .Where(cliente => File.ReadAllText(path).Contains(cliente, StringComparison.Ordinal))
                .Select(cliente => $"{Path.GetFileName(path)}: {cliente}"))
            .ToArray();

        Assert.Empty(ofensores);
    }

    [Fact]
    public void Ningun_csproj_bajo_src_referencia_un_paquete_de_cliente_de_base_de_datos_de_POS()
    {
        var srcRoot = FindSolutionRoot("src");

        var ofensores = Directory
            .EnumerateFiles(srcRoot, "*.csproj", SearchOption.AllDirectories)
            .SelectMany(path => PaquetesDeBaseProhibidos
                .Where(paquete => File.ReadAllText(path).Contains(paquete, StringComparison.OrdinalIgnoreCase))
                .Select(paquete => $"{Path.GetFileName(path)}: {paquete}"))
            .ToArray();

        Assert.Empty(ofensores);
    }

    /// <summary>
    /// Belt-and-suspenders on top of the two structural checks above: even a connection string
    /// typed directly into configuration (no client library needed for, say, a raw ODBC DSN)
    /// would slip past a "no forbidden package" check. Looks for the tell-tale keywords of a
    /// non-Postgres ADO.NET connection string in the two application-settings files this
    /// repository is allowed to read.
    /// </summary>
    [Fact]
    public void Los_appsettings_permitidos_no_tienen_una_cadena_de_conexion_que_no_sea_Postgres_o_un_placeholder()
    {
        var apiRoot = FindSolutionRoot("src", "Fidelizar.Api");
        var palabrasNoPostgres = new[] { "Driver={SQL Server}", "Driver={MySQL", "Data Source=" };

        var archivosPermitidos = new[] { "appsettings.json", "appsettings.Development.json" }
            .Select(nombre => Path.Combine(apiRoot, nombre))
            .Where(File.Exists);

        var ofensores = archivosPermitidos
            .SelectMany(path => palabrasNoPostgres
                .Where(palabra => File.ReadAllText(path).Contains(palabra, StringComparison.OrdinalIgnoreCase))
                .Select(palabra => $"{Path.GetFileName(path)}: {palabra}"))
            .ToArray();

        Assert.Empty(ofensores);
    }

    private static bool EsCodigoFuenteDelProducto(string path) =>
        !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
        !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal);

    private static string FindSolutionRoot(params string[] relativePathFromRoot)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Fidelizar.sln")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new InvalidOperationException(
                "Could not locate Fidelizar.sln by walking up from the test output directory.");
        }

        return Path.Combine([directory.FullName, .. relativePathFromRoot]);
    }
}

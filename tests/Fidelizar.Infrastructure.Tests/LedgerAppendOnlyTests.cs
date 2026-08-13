namespace Fidelizar.Infrastructure.Tests;

/// <summary>
/// I1 — the ledger is append-only: no <c>UPDATE</c> and no <c>DELETE</c> against
/// <c>MovimientoCredito</c>, anywhere, for any reason (ARCHITECTURE §4).
///
/// The interface half of this invariant — "no repository interface exposes a delete or update on
/// the ledger" — is already covered by
/// <c>Fidelizar.Domain.Tests.Repositories.RepositoryInterfacesTests</c> and is deliberately not
/// duplicated here. This file covers the other half the invariant actually names: that no code
/// path anywhere under <c>src/</c> ever issues one, whether through EF Core's change tracker or
/// through raw SQL that would bypass it entirely.
/// </summary>
public class LedgerAppendOnlyTests
{
    // EF Core call shapes that would mutate or remove an existing row in the ledger DbSet
    // (Fidelizar.Infrastructure.Persistence.FidelizarDbContext.MovimientosCredito).
    private static readonly string[] OperacionesProhibidasSobreElLedger =
    [
        "MovimientosCredito.Remove", "MovimientosCredito.RemoveRange",
        "MovimientosCredito.Update", "MovimientosCredito.UpdateRange",
        "MovimientosCredito.ExecuteDelete", "MovimientosCredito.ExecuteUpdate",
    ];

    // Raw SQL shapes, in case a future change bypasses EF's change tracker entirely (FromSqlRaw,
    // ExecuteSqlRaw, a stored procedure, a migration hand-editing data).
    private static readonly string[] SqlProhibidoSobreElLedger =
    [
        "delete from \"movimientoscredito\"", "delete from movimientoscredito",
        "update \"movimientoscredito\"", "update movimientoscredito",
    ];

    [Fact]
    public void Ningun_codigo_bajo_src_llama_Remove_RemoveRange_Update_UpdateRange_ni_ExecuteDelete_ExecuteUpdate_sobre_el_ledger()
    {
        var srcRoot = FindSolutionRoot("src");

        var ofensores = Directory
            .EnumerateFiles(srcRoot, "*.cs", SearchOption.AllDirectories)
            .Where(EsCodigoFuenteDelProducto)
            .SelectMany(path => OperacionesProhibidasSobreElLedger
                .Where(op => File.ReadAllText(path).Contains(op, StringComparison.Ordinal))
                .Select(op => $"{Path.GetFileName(path)}: {op}"))
            .ToArray();

        Assert.Empty(ofensores);
    }

    [Fact]
    public void Ningun_codigo_bajo_src_contiene_SQL_crudo_de_UPDATE_o_DELETE_contra_el_ledger()
    {
        var srcRoot = FindSolutionRoot("src");

        var ofensores = Directory
            .EnumerateFiles(srcRoot, "*.cs", SearchOption.AllDirectories)
            .Where(EsCodigoFuenteDelProducto)
            .SelectMany(path => SqlProhibidoSobreElLedger
                .Where(sql => File.ReadAllText(path).Contains(sql, StringComparison.OrdinalIgnoreCase))
                .Select(sql => $"{Path.GetFileName(path)}: {sql}"))
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

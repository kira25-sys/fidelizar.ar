using System.Text.RegularExpressions;

namespace Fidelizar.Infrastructure.Tests.Repositories;

/// <summary>
/// I8 — <c>NegocioId</c> is on every table, and every query filters by it (ARCHITECTURE §5).
///
/// The schema half — every table has a non-nullable <c>NegocioId</c> column — is already covered
/// by <c>FidelizarDbContextModelTests.NegocioId_no_es_nullable_en_cada_tabla</c> and is not
/// duplicated here. The multi-tenant-isolation half — two businesses never see each other's data —
/// is already covered behaviourally by <c>SaldoServiceTests.Saldo_filtra_por_NegocioId_y_MiembroId</c>,
/// <c>CorteServiceTests.Corte_de_otro_negocio_no_satisface_a_este</c> and
/// <c>VipPadronImporterTests.ImportAsync_FiltraPorNegocioId</c>.
///
/// This file covers what none of those do: that <b>every</b> read method on an EF repository in
/// <c>Fidelizar.Infrastructure.Repositories</c> — not just the ones a test happened to exercise —
/// carries a <c>NegocioId</c> filter in its own source, so a new query method added later cannot
/// quietly skip it and still slip through. This is a source-text heuristic, not a full parser: it
/// finds each <c>public</c> member in a repository file and asserts that any one of them touching
/// a <c>DbSet</c> for a read also mentions <c>NegocioId</c> somewhere in its own body. Insert-only
/// members (<c>.Add(</c>) are exempt — DATA-MODEL §1 makes <c>NegocioId</c> a value the caller
/// supplies on write, not something a query filters by.
/// </summary>
public class NegocioIdFiltroTests
{
    private static readonly string[] NombresDeDbSet =
        ["Negocios", "Sucursales", "Miembros", "MovimientosCredito", "Cortes", "ConfiguracionesPrograma"];

    [Fact]
    public void Toda_consulta_de_lectura_en_los_repositorios_EF_menciona_NegocioId_en_su_propio_cuerpo()
    {
        var repoDir = FindSolutionRoot("src", "Fidelizar.Infrastructure", "Repositories");
        var archivos = Directory.EnumerateFiles(repoDir, "*.cs", SearchOption.TopDirectoryOnly).ToArray();

        // Guards against this test silently passing "for free" if the repositories ever move.
        Assert.NotEmpty(archivos);

        var ofensores = new List<string>();

        foreach (var archivo in archivos)
        {
            var texto = File.ReadAllText(archivo);

            // Split into one chunk per public member (4-space-indented "public" declarations —
            // the style every repository in this solution already uses).
            var miembros = Regex.Split(texto, @"(?=\n {4}public\s)");

            foreach (var miembro in miembros)
            {
                var tocaUnDbSet = NombresDeDbSet.Any(nombre =>
                    miembro.Contains($"dbContext.{nombre}", StringComparison.Ordinal));
                var esInsercion = miembro.Contains(".Add(", StringComparison.Ordinal);

                if (tocaUnDbSet && !esInsercion && !miembro.Contains("NegocioId", StringComparison.Ordinal))
                {
                    var firma = miembro.Trim().Split('\n')[0].Trim();
                    ofensores.Add($"{Path.GetFileName(archivo)}: {firma}");
                }
            }
        }

        Assert.Empty(ofensores);
    }

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

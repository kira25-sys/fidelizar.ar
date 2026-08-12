using Fidelizar.Domain.Money;

namespace Fidelizar.Domain.Tests.Money;

/// <summary>
/// I4: money is <c>decimal</c>, and rounding happens in exactly one place — 2 decimals,
/// <see cref="MidpointRounding.AwayFromZero"/>.
/// </summary>
public class RedondeoTests
{
    [Theory]
    [InlineData("1.005", "1.01")] // AwayFromZero, not banker's rounding (which would give 1.00)
    [InlineData("-1.005", "-1.01")]
    [InlineData("100.004", "100.00")]
    [InlineData("100.006", "100.01")]
    [InlineData("3600.00", "3600.00")] // 3% of 120000 — already exact, must not shift
    public void Monto_redondea_a_2_decimales_AwayFromZero(string valor, string esperado)
    {
        var resultado = Redondeo.Monto(decimal.Parse(valor, System.Globalization.CultureInfo.InvariantCulture));

        Assert.Equal(decimal.Parse(esperado, System.Globalization.CultureInfo.InvariantCulture), resultado);
    }

    /// <summary>
    /// I4 says rounding happens in exactly one place. This is verifiable, not just a comment: it
    /// scans every .cs file under src/ (excluding this rounding function's own file and the
    /// generated EF Core migrations, which stamp historical/default literal values, never round
    /// anything) and fails if a second <c>Math.Round</c> call ever creeps in.
    /// </summary>
    [Fact]
    public void El_redondeo_ocurre_en_un_solo_lugar()
    {
        var srcRoot = FindSolutionRoot("src");

        var archivosConRedondeo = Directory
            .EnumerateFiles(srcRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}Migrations{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path => File.ReadAllText(path).Contains("Math.Round", StringComparison.Ordinal))
            .Select(Path.GetFileName)
            .ToArray();

        var archivoUnico = Assert.Single(archivosConRedondeo);
        Assert.Equal("Redondeo.cs", archivoUnico);
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

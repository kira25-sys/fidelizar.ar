using ClosedXML.Excel;

namespace Fidelizar.Infrastructure.Tests.Import.TestHelpers;

/// <summary>
/// Builds a temporary .xlsx file for a test via a caller-supplied worksheet-building action and
/// deletes it on dispose. ClosedXML is available here transitively through the ProjectReference
/// to <c>Fidelizar.Infrastructure</c> — no separate package reference in this test project
/// (F0-08: ClosedXML is added only to <c>Fidelizar.Infrastructure</c>'s csproj).
/// </summary>
public sealed class TempXlsxFile : IDisposable
{
    public string Path { get; }

    public TempXlsxFile(Action<IXLWorksheet> build, string sheetName = "Padron")
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"fidelizar-tests-{Guid.NewGuid():N}.xlsx");
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add(sheetName);
        build(worksheet);
        workbook.SaveAs(Path);
    }

    public void Dispose()
    {
        if (File.Exists(Path))
        {
            File.Delete(Path);
        }
    }
}

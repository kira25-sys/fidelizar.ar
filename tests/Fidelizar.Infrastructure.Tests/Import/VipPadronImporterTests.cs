using ClosedXML.Excel;
using Fidelizar.Domain.Entities;
using Fidelizar.Infrastructure.Import;
using Fidelizar.Infrastructure.Tests.Import.Fakes;
using Fidelizar.Infrastructure.Tests.Import.TestHelpers;

namespace Fidelizar.Infrastructure.Tests.Import;

/// <summary>
/// The padron import is the entry door for a business's money: these tests cover that it loads
/// exactly what the spreadsheet says, that it never loads twice, and that anything doubtful is
/// reported rather than guessed (I5). Ported from Octaviano's <c>VipPadronImporterTests</c>
/// (F0-08), adapted to the new model — see <see cref="VipPadronImporter"/>'s class remarks for
/// the full list of adaptations.
///
/// All names, ids and amounts below are invented fixtures (CLAUDE.md) — never real member data.
/// </summary>
public class VipPadronImporterTests
{
    private const int NegocioId = 1;
    private const int OtroNegocioId = 2;
    private const int DeclaradoPorUsuarioId = 999;

    // The spreadsheet's balances are current through this day inclusive. An invented date, not
    // tied to any real cutoff.
    private static readonly DateOnly Corte = new(2026, 7, 31);
    private static readonly DateOnly Hoy = new(2026, 8, 12);

    private static TempXlsxFile PadronDeEjemplo(Action<IXLWorksheet>? extra = null) => new(ws =>
    {
        ws.Cell(1, 1).Value = "customer_id";
        ws.Cell(1, 2).Value = "nombre";
        ws.Cell(1, 3).Value = "credito";
        ws.Cell(1, 4).Value = "cumple";

        ws.Cell(2, 1).Value = "9001";
        ws.Cell(2, 2).Value = "Prueba Ficticia Uno";
        ws.Cell(2, 3).Value = 5000;
        ws.Cell(2, 4).Value = "16/09";

        ws.Cell(3, 1).Value = "9002";
        ws.Cell(3, 2).Value = "Prueba Ficticia Dos";
        ws.Cell(3, 3).Value = 3200;

        extra?.Invoke(ws);
    });

    private static (VipPadronImporter Importer, FakeMiembroRepository Miembros, FakeMovimientoRepository Movimientos, FakeCorteRepository Cortes)
        CrearImporter()
    {
        var miembros = new FakeMiembroRepository();
        var movimientos = new FakeMovimientoRepository();
        var cortes = new FakeCorteRepository();
        var importer = new VipPadronImporter(miembros, movimientos, cortes);

        return (importer, miembros, movimientos, cortes);
    }

    private Task<ResultadoPadron> ImportarAsync(
        VipPadronImporter importer, string rutaExcel, int negocioId = NegocioId, DateOnly? corte = null) =>
        importer.ImportAsync(negocioId, rutaExcel, corte ?? Corte, DeclaradoPorUsuarioId, Hoy);

    [Fact]
    public async Task ImportAsync_CargaMiembrosYSuSaldoInicial()
    {
        using var excel = PadronDeEjemplo();
        var (importer, miembros, movimientos, _) = CrearImporter();

        var resultado = await ImportarAsync(importer, excel.Path);

        Assert.Equal(2, resultado.MiembrosCreados);
        Assert.Equal(2, resultado.SaldosInicialesCargados);
        Assert.Equal(8_200m, resultado.TotalSaldoInicial);

        var uno = await miembros.GetByClienteExternoIdAsync(NegocioId, "9001");
        Assert.NotNull(uno);
        Assert.Equal(5_000m, await movimientos.GetSaldoAsync(NegocioId, uno.Id));
    }

    [Fact]
    public async Task ImportAsync_ElSaldoInicialQuedaComoMovimientoConSuOrigen()
    {
        using var excel = PadronDeEjemplo();
        var (importer, miembros, movimientos, _) = CrearImporter();

        await ImportarAsync(importer, excel.Path);

        var uno = await miembros.GetByClienteExternoIdAsync(NegocioId, "9001");
        var movimiento = Assert.Single(movimientos.Movimientos, m => m.MiembroId == uno!.Id);

        Assert.Equal(TipoMovimientoCredito.SaldoInicial, movimiento.Tipo);
        Assert.Equal("Migrado del padrón importado.", movimiento.Motivo);
        Assert.Null(movimiento.UsuarioId);
        // Dated at the cutoff itself: the balance is what there was through the end of that day,
        // BEFORE accrual starts counting (which begins only the day after).
        Assert.Equal(Corte, movimiento.FechaEfectiva);
    }

    [Fact]
    public async Task ImportAsync_PersisteElCorteParaQueLaAcumulacionLoUse()
    {
        using var excel = PadronDeEjemplo();
        var (importer, _, _, cortes) = CrearImporter();

        await ImportarAsync(importer, excel.Path);

        var corteGuardado = await cortes.ObtenerAsync(NegocioId);
        Assert.NotNull(corteGuardado);
        Assert.Equal(Corte, corteGuardado.Fecha);
        Assert.Equal(DeclaradoPorUsuarioId, corteGuardado.DeclaradoPorUsuarioId);
    }

    [Fact]
    public async Task ImportAsync_CorrerDosVeces_NoDuplicaNiMiembrosNiSaldo()
    {
        using var excel = PadronDeEjemplo();
        var (importer, miembros, movimientos, _) = CrearImporter();

        await ImportarAsync(importer, excel.Path);
        var segunda = await ImportarAsync(importer, excel.Path);

        Assert.Equal(0, segunda.MiembrosCreados);
        Assert.Equal(2, segunda.MiembrosYaExistian);
        Assert.Equal(0, segunda.SaldosInicialesCargados);

        var uno = await miembros.GetByClienteExternoIdAsync(NegocioId, "9001");
        Assert.Equal(5_000m, await movimientos.GetSaldoAsync(NegocioId, uno!.Id));
    }

    [Fact]
    public async Task ImportAsync_CorteYaDeclaradoConLaMismaFecha_EsIdempotenteYNoAvisa()
    {
        using var excel = PadronDeEjemplo();
        var (importer, _, _, _) = CrearImporter();

        await ImportarAsync(importer, excel.Path);
        var segunda = await ImportarAsync(importer, excel.Path);

        Assert.DoesNotContain(segunda.Advertencias, a => a.Contains("corte", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ImportAsync_CorteYaDeclaradoConFechaDistinta_NoLoSobrescribeYAvisa()
    {
        // I1-adjacent discipline: ICorteRepository has no update method. A second run declaring a
        // different cutoff must not silently change what accrual may already have used.
        using var excel = PadronDeEjemplo();
        var (importer, _, _, cortes) = CrearImporter();

        await ImportarAsync(importer, excel.Path, corte: Corte);
        var otraFecha = Corte.AddDays(5);
        var resultado = await ImportarAsync(importer, excel.Path, corte: otraFecha);

        var corteGuardado = await cortes.ObtenerAsync(NegocioId);
        Assert.Equal(Corte, corteGuardado!.Fecha);
        Assert.Contains(resultado.Advertencias, a => a.Contains("corte", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ImportAsync_ClienteExternoIdRepetido_NoCargaNingunaDeLasDosYAvisa()
    {
        using var excel = PadronDeEjemplo(ws =>
        {
            ws.Cell(4, 1).Value = "9001"; // repeated
            ws.Cell(4, 2).Value = "Prueba Ficticia Uno (otra fila)";
            ws.Cell(4, 3).Value = 999;
        });
        var (importer, miembros, _, _) = CrearImporter();

        var resultado = await ImportarAsync(importer, excel.Path);

        Assert.Equal(1, resultado.MiembrosCreados);
        Assert.Null(await miembros.GetByClienteExternoIdAsync(NegocioId, "9001"));
        Assert.Contains(resultado.Advertencias, a => a.Contains("9001") && a.Contains("2 veces"));
    }

    [Fact]
    public async Task ImportAsync_FilaSinClienteExternoId_SeImportaSinVincular()
    {
        // DATA-MODEL §3: ClienteExternoId is nullable. A member registered with no POS id is
        // still imported — it just starts unlinked and accrues nothing until linked later.
        using var excel = PadronDeEjemplo(ws =>
        {
            ws.Cell(4, 1).Value = string.Empty;
            ws.Cell(4, 2).Value = "Prueba Ficticia Sin Vincular";
            ws.Cell(4, 3).Value = 100;
        });
        var (importer, miembros, _, _) = CrearImporter();

        var resultado = await ImportarAsync(importer, excel.Path);

        Assert.Equal(3, resultado.MiembrosCreados);
        var sinVincular = Assert.Single(miembros.Miembros, m => m.Nombre == "Prueba Ficticia Sin Vincular");
        Assert.Null(sinVincular.ClienteExternoId);
    }

    [Fact]
    public async Task ImportAsync_DosFilasSinClienteExternoId_NoSeConsideranDuplicadas()
    {
        // Two blank ids are not "the same id repeated" — unlike a genuine duplicate customer_id,
        // both unlinked rows load.
        using var excel = PadronDeEjemplo(ws =>
        {
            ws.Cell(4, 1).Value = string.Empty;
            ws.Cell(4, 2).Value = "Prueba Ficticia Sin Vincular A";
            ws.Cell(5, 1).Value = string.Empty;
            ws.Cell(5, 2).Value = "Prueba Ficticia Sin Vincular B";
        });
        var (importer, _, _, _) = CrearImporter();

        var resultado = await ImportarAsync(importer, excel.Path);

        Assert.Equal(4, resultado.MiembrosCreados);
        Assert.DoesNotContain(resultado.Advertencias, a => a.Contains("veces"));
    }

    [Fact]
    public async Task ImportAsync_ParseaElCumpleDeDiaYMes()
    {
        using var excel = PadronDeEjemplo();
        var (importer, miembros, _, _) = CrearImporter();

        await ImportarAsync(importer, excel.Path);

        var uno = await miembros.GetByClienteExternoIdAsync(NegocioId, "9001");
        Assert.Equal(16, uno!.FechaNacimiento!.Value.Day);
        Assert.Equal(9, uno.FechaNacimiento.Value.Month);
    }

    [Fact]
    public async Task ImportAsync_SinColumnaDeId_ImportaATodosSinVincularYAvisa()
    {
        // Adapted from Octaviano's "no id column aborts the import": here it does not, because a
        // whole roster with no POS integration yet is a legitimate starting state (F1-14).
        using var excel = new TempXlsxFile(ws =>
        {
            ws.Cell(1, 1).Value = "nombre";
            ws.Cell(1, 2).Value = "credito";
            ws.Cell(2, 1).Value = "Prueba Ficticia Sin Columna Id";
            ws.Cell(2, 2).Value = 100;
        });
        var (importer, miembros, _, _) = CrearImporter();

        var resultado = await ImportarAsync(importer, excel.Path);

        Assert.Equal(1, resultado.MiembrosCreados);
        Assert.Contains(resultado.Advertencias, a => a.Contains("id de cliente"));
        Assert.Null(Assert.Single(miembros.Miembros).ClienteExternoId);
    }

    [Fact]
    public async Task ImportAsync_SinColumnaDeNombre_NoCargaNadaYExplicaQueFalta()
    {
        using var excel = new TempXlsxFile(ws =>
        {
            ws.Cell(1, 1).Value = "customer_id";
            ws.Cell(1, 2).Value = "credito";
            ws.Cell(2, 1).Value = "9001";
            ws.Cell(2, 2).Value = 100;
        });
        var (importer, _, _, _) = CrearImporter();

        var resultado = await ImportarAsync(importer, excel.Path);

        Assert.Equal(0, resultado.MiembrosCreados);
        Assert.Contains(resultado.Advertencias, a => a.Contains("nombre"));
    }

    [Fact]
    public async Task ImportAsync_SinColumnaDeCredito_CargaLosMiembrosConSaldoCeroYAvisa()
    {
        using var excel = new TempXlsxFile(ws =>
        {
            ws.Cell(1, 1).Value = "customer_id";
            ws.Cell(1, 2).Value = "nombre";
            ws.Cell(2, 1).Value = "9001";
            ws.Cell(2, 2).Value = "Prueba Ficticia Sin Credito";
        });
        var (importer, _, _, _) = CrearImporter();

        var resultado = await ImportarAsync(importer, excel.Path);

        Assert.Equal(1, resultado.MiembrosCreados);
        Assert.Equal(0, resultado.SaldosInicialesCargados);
        Assert.Contains(resultado.Advertencias, a => a.Contains("crédito") && a.Contains("CERO"));
    }

    [Fact]
    public async Task ImportAsync_CreditoAmbiguo_SeCargaComoCeroYAvisaConLaFilaYComoReexportar()
    {
        using var excel = new TempXlsxFile(ws =>
        {
            ws.Cell(1, 1).Value = "customer_id";
            ws.Cell(1, 2).Value = "nombre";
            ws.Cell(1, 3).Value = "credito";
            ws.Cell(2, 1).Value = "9001";
            ws.Cell(2, 2).Value = "Prueba Ficticia Credito Ambiguo";
            // Genuinely ambiguous: one thousand two hundred thirty-four, or a decimal comma with
            // an extra zero?
            ws.Cell(2, 3).Value = "1,234";
        });
        var (importer, _, _, _) = CrearImporter();

        var resultado = await ImportarAsync(importer, excel.Path);

        Assert.Equal(1, resultado.MiembrosCreados);
        Assert.Equal(0, resultado.SaldosInicialesCargados);
        Assert.Contains(resultado.Advertencias, a =>
            a.Contains("Fila 2") && a.Contains("ambiguo") && a.Contains("punto decimal"));
    }

    [Fact]
    public async Task ImportAsync_CreditoNegativo_SeCargaTalCualYAvisa()
    {
        using var excel = new TempXlsxFile(ws =>
        {
            ws.Cell(1, 1).Value = "customer_id";
            ws.Cell(1, 2).Value = "nombre";
            ws.Cell(1, 3).Value = "credito";
            ws.Cell(2, 1).Value = "9001";
            ws.Cell(2, 2).Value = "Prueba Ficticia Credito Negativo";
            ws.Cell(2, 3).Value = -500;
        });
        var (importer, miembros, movimientos, _) = CrearImporter();

        var resultado = await ImportarAsync(importer, excel.Path);

        var miembro = Assert.Single(miembros.Miembros);
        Assert.Equal(-500m, await movimientos.GetSaldoAsync(NegocioId, miembro.Id));
        Assert.Contains(resultado.Advertencias, a => a.Contains("negativo"));
    }

    [Fact]
    public async Task ImportAsync_RedondeaElCreditoAlLeerlo()
    {
        // I4: the single rounding point in the system. A 4-decimal export value must come out
        // rounded to cents, AwayFromZero.
        using var excel = new TempXlsxFile(ws =>
        {
            ws.Cell(1, 1).Value = "customer_id";
            ws.Cell(1, 2).Value = "nombre";
            ws.Cell(1, 3).Value = "credito";
            ws.Cell(2, 1).Value = "9001";
            ws.Cell(2, 2).Value = "Prueba Ficticia Credito Con Decimales";
            ws.Cell(2, 3).Value = "10.1256";
        });
        var (importer, _, _, _) = CrearImporter();

        var resultado = await ImportarAsync(importer, excel.Path);

        Assert.Equal(10.13m, resultado.TotalSaldoInicial);
    }

    [Fact]
    public async Task ImportAsync_ArchivoInexistente_LoDiceEnVezDeExplotar()
    {
        var (importer, _, _, _) = CrearImporter();

        var resultado = await ImportarAsync(importer, Path.Combine(Path.GetTempPath(), "no-existe-fidelizar.xlsx"));

        Assert.Equal(0, resultado.MiembrosCreados);
        Assert.Contains(resultado.Advertencias, a => a.Contains("No se encontró el archivo"));
    }

    [Fact]
    public async Task ImportAsync_EncabezadosConAcentosYMayusculas_LosReconoceIgual()
    {
        using var excel = new TempXlsxFile(ws =>
        {
            ws.Cell(1, 1).Value = "ID Cliente";
            ws.Cell(1, 2).Value = "Nombre y Apellido";
            ws.Cell(1, 3).Value = "Crédito";
            ws.Cell(2, 1).Value = "9001";
            ws.Cell(2, 2).Value = "Prueba Ficticia Encabezados";
            ws.Cell(2, 3).Value = 7813;
        });
        var (importer, _, _, _) = CrearImporter();

        var resultado = await ImportarAsync(importer, excel.Path);

        Assert.Equal(1, resultado.MiembrosCreados);
        Assert.Equal(7_813m, resultado.TotalSaldoInicial);
    }

    [Fact]
    public async Task ImportAsync_FiltraPorNegocioId()
    {
        // I8: two businesses importing a member with the same customer_id must never collide.
        using var excelUno = PadronDeEjemplo();
        using var excelDos = PadronDeEjemplo();
        var (importer, miembros, _, cortes) = CrearImporter();

        await ImportarAsync(importer, excelUno.Path, negocioId: NegocioId);
        await ImportarAsync(importer, excelDos.Path, negocioId: OtroNegocioId);

        Assert.Equal(4, miembros.Miembros.Count);
        Assert.NotNull(await miembros.GetByClienteExternoIdAsync(NegocioId, "9001"));
        Assert.NotNull(await miembros.GetByClienteExternoIdAsync(OtroNegocioId, "9001"));
        Assert.NotNull(await cortes.ObtenerAsync(NegocioId));
        Assert.NotNull(await cortes.ObtenerAsync(OtroNegocioId));
    }
}

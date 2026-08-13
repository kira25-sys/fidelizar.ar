using ClosedXML.Excel;
using Fidelizar.Infrastructure.Import;
using Fidelizar.Infrastructure.Tests.Import.Fakes;
using Fidelizar.Infrastructure.Tests.Import.TestHelpers;

namespace Fidelizar.Infrastructure.Tests.Import;

/// <summary>
/// I7 — ambiguous identity is resolved by a human; the system never guesses that two members are
/// the same person. <c>VipNombres</c>' own doc comment already says this in words ("this is for
/// finding candidates, never for deciding they are the same member"); this proves it in behaviour,
/// at the one place today where two similarly-named rows could tempt an automatic merge — the
/// padron import (<see cref="VipPadronImporter"/>).
///
/// All names below are invented fixtures (CLAUDE.md) — never real member data.
/// </summary>
public class IdentidadAmbiguaTests
{
    private const int NegocioId = 1;
    private const int DeclaradoPorUsuarioId = 999;
    private static readonly DateOnly Corte = new(2026, 7, 31);
    private static readonly DateOnly Hoy = new(2026, 8, 12);

    [Fact]
    public async Task Dos_clientes_externos_distintos_con_nombre_equivalente_quedan_como_dos_miembros_sin_fusionarse()
    {
        using var excel = new TempXlsxFile(ws =>
        {
            ws.Cell(1, 1).Value = "customer_id";
            ws.Cell(1, 2).Value = "nombre";
            ws.Cell(1, 3).Value = "credito";

            // Same person-looking name (differs only in accent/case, which VipNombres.Normalizar
            // folds to the same value) under two different POS ids: the importer must never
            // decide on its own that these are the same member — a human resolves that later.
            ws.Cell(2, 1).Value = "9001";
            ws.Cell(2, 2).Value = "Prueba Ficticia Homónima";
            ws.Cell(2, 3).Value = 1000;

            ws.Cell(3, 1).Value = "9002";
            ws.Cell(3, 2).Value = "PRUEBA FICTICIA HOMONIMA";
            ws.Cell(3, 3).Value = 2000;
        });

        var miembros = new FakeMiembroRepository();
        var movimientos = new FakeMovimientoRepository();
        var cortes = new FakeCorteRepository();
        var importer = new VipPadronImporter(miembros, movimientos, cortes);

        var resultado = await importer.ImportAsync(NegocioId, excel.Path, Corte, DeclaradoPorUsuarioId, Hoy);

        Assert.Equal(2, resultado.MiembrosCreados);

        var uno = await miembros.GetByClienteExternoIdAsync(NegocioId, "9001");
        var dos = await miembros.GetByClienteExternoIdAsync(NegocioId, "9002");

        Assert.NotNull(uno);
        Assert.NotNull(dos);
        Assert.NotEqual(uno!.Id, dos!.Id);
        Assert.Equal(1_000m, await movimientos.GetSaldoAsync(NegocioId, uno.Id));
        Assert.Equal(2_000m, await movimientos.GetSaldoAsync(NegocioId, dos.Id));
    }
}

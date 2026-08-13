using Fidelizar.Domain.Entities;
using Fidelizar.MigracionOctaviano.Migracion;
using Fidelizar.MigracionOctaviano.Origen;
using Fidelizar.MigracionOctaviano.Tests.Fakes;

namespace Fidelizar.MigracionOctaviano.Tests;

/// <summary>
/// F0-09's migration logic, exercised end to end with a fake <see cref="IOctavianoSource"/> and
/// fake repositories — no SQLite file, no Postgres. <b>Every name, id and amount below is
/// invented</b> (CLAUDE.md, ROADMAP F0-09): a real member is never a test case, and this file is
/// never allowed to import from or reference <c>octaviano.db</c>.
/// </summary>
public class MigradorOctavianoTests
{
    private static readonly DateTime AhoraUtc = new(2026, 8, 12, 10, 0, 0, DateTimeKind.Utc);

    private static (MigradorOctaviano Migrador, FakeNegocioRepository Negocios, FakeConfiguracionProgramaRepository Configuraciones,
        FakeMiembroRepository Miembros, FakeMovimientoRepository Movimientos, FakeConsentimientoRepository Consentimientos)
        CrearMigrador(IReadOnlyList<OctavianoMiembro> miembrosOrigen, IReadOnlyList<OctavianoMovimiento> movimientosOrigen)
    {
        var origen = new FakeOctavianoSource(miembrosOrigen, movimientosOrigen);
        var negocios = new FakeNegocioRepository();
        var configuraciones = new FakeConfiguracionProgramaRepository();
        var miembros = new FakeMiembroRepository();
        var movimientos = new FakeMovimientoRepository();
        var consentimientos = new FakeConsentimientoRepository();

        var migrador = new MigradorOctaviano(origen, negocios, configuraciones, miembros, movimientos, consentimientos);

        return (migrador, negocios, configuraciones, miembros, movimientos, consentimientos);
    }

    // Invented fixture: "9101"/"Prueba Ficticia Uno" is not a real member, the same convention
    // VipPadronImporterTests already uses.
    private static OctavianoMiembro MiembroInventado(
        int id = 1, int clienteExternoId = 9101, string nombre = "Prueba Ficticia Uno", DateOnly? fechaAlta = null) =>
        new(
            Id: id,
            ClienteExternoId: clienteExternoId,
            NumeroVip: "V-001",
            Nombre: nombre,
            Telefono: "1155550000",
            Dni: "30000000",
            FechaNacimiento: new DateOnly(1990, 5, 20),
            SucursalExternaId: 3,
            FechaAlta: fechaAlta ?? new DateOnly(2024, 1, 10),
            Activo: true,
            UpdatedAt: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

    private static OctavianoMovimiento MovimientoInventado(
        int id, int miembroId, DateTime fecha, int tipo, decimal monto,
        long? referenciaVenta = null, string usuario = "sistema", string? motivo = null) =>
        new(id, miembroId, fecha, $"{fecha:yyyy-MM}", tipo, monto, referenciaVenta, usuario, motivo, 0m);

    [Fact]
    public async Task EjecutarAsync_CreaElNegocioYUnaConfiguracionVigente()
    {
        var (migrador, negocios, configuraciones, _, _, _) = CrearMigrador([], []);

        var resultado = await migrador.EjecutarAsync(AhoraUtc);

        Assert.NotNull(await negocios.ObtenerUnicoAsync());
        var config = await configuraciones.ObtenerVigenteAsync(resultado.NegocioId);
        Assert.NotNull(config);
        Assert.Equal(0.03m, config!.PorcentajeAcumulacion);
        Assert.Null(config.VigenteHasta);
    }

    [Fact]
    public async Task EjecutarAsync_MigraUnSocioConSusDatos()
    {
        var origen = MiembroInventado();
        var (migrador, _, _, miembros, _, _) = CrearMigrador([origen], []);

        var resultado = await migrador.EjecutarAsync(AhoraUtc);

        Assert.Equal(1, resultado.MiembrosCreados);
        var creado = Assert.Single(miembros.Miembros);
        Assert.Equal("9101", creado.ClienteExternoId);
        Assert.Equal("Prueba Ficticia Uno", creado.Nombre);
        Assert.Equal("prueba ficticia uno", creado.NombreNormalizado);
        Assert.Null(creado.SucursalId); // deliberately not mapped, same as VipPadronImporter (F0-08)
    }

    [Fact]
    public async Task EjecutarAsync_MigraElLibroMayorCompletoSinColapsarlo()
    {
        var origen = MiembroInventado(id: 1, clienteExternoId: 9101);
        OctavianoMovimiento[] movimientos =
        [
            MovimientoInventado(1, 1, new DateTime(2025, 1, 5), tipo: 0, monto: 1000m), // SaldoInicial
            MovimientoInventado(2, 1, new DateTime(2025, 2, 10), tipo: 1, monto: 30m, referenciaVenta: 555), // Acumulacion
            MovimientoInventado(3, 1, new DateTime(2025, 3, 1), tipo: 2, monto: -200m, motivo: "Canje mostrador"), // Canje
        ];
        var (migrador, _, _, miembros, movimientosRepo, _) = CrearMigrador([origen], movimientos);

        var resultado = await migrador.EjecutarAsync(AhoraUtc);

        Assert.Equal(3, resultado.MovimientosMigrados);
        var miembro = miembros.Miembros.Single();
        var historial = movimientosRepo.Movimientos.Where(m => m.MiembroId == miembro.Id).OrderBy(m => m.FechaEfectiva).ToList();
        Assert.Equal(3, historial.Count); // not collapsed into a single SaldoInicial

        Assert.Equal(TipoMovimientoCredito.SaldoInicial, historial[0].Tipo);
        Assert.Equal(1000m, historial[0].Monto);
        Assert.Equal(new DateOnly(2025, 1, 5), historial[0].FechaEfectiva);

        Assert.Equal(TipoMovimientoCredito.Acumulacion, historial[1].Tipo);
        Assert.Equal("555", historial[1].ReferenciaVenta);
        Assert.NotNull(historial[1].ConfiguracionId);

        Assert.Equal(TipoMovimientoCredito.Canje, historial[2].Tipo);
        Assert.Equal("Canje mostrador", historial[2].Motivo);

        // The balance recomputed as SUM(Monto) (I2) matches hand arithmetic: 1000 + 30 - 200.
        Assert.Equal(830m, await movimientosRepo.GetSaldoAsync(resultado.NegocioId, miembro.Id));
    }

    [Fact]
    public async Task EjecutarAsync_RegistradoEnEsAhora_FechaEfectivaEsLaDelOrigen()
    {
        var origen = MiembroInventado();
        var movimiento = MovimientoInventado(1, 1, new DateTime(2020, 6, 15, 9, 30, 0), tipo: 0, monto: 500m);
        var (migrador, _, _, _, movimientosRepo, _) = CrearMigrador([origen], [movimiento]);

        await migrador.EjecutarAsync(AhoraUtc);

        var migrado = Assert.Single(movimientosRepo.Movimientos);
        Assert.Equal(new DateOnly(2020, 6, 15), migrado.FechaEfectiva);
        Assert.Equal(AhoraUtc, migrado.RegistradoEn);
    }

    [Fact]
    public async Task EjecutarAsync_PeriodoSeDerivaDeFechaEfectiva()
    {
        var origen = MiembroInventado();
        // Origin's own Periodo string is deliberately different from Fecha's month, to prove the
        // migrator recomputes Periodo from FechaEfectiva rather than trusting Octaviano's column.
        var movimiento = new OctavianoMovimiento(1, 1, new DateTime(2021, 3, 20), "2021-01", 0, 500m, null, "sistema", null, 0m);
        var (migrador, _, _, _, movimientosRepo, _) = CrearMigrador([origen], [movimiento]);

        await migrador.EjecutarAsync(AhoraUtc);

        Assert.Equal("2021-03", Assert.Single(movimientosRepo.Movimientos).Periodo);
    }

    [Fact]
    public async Task EjecutarAsync_UsuarioIdQuedaNuloEnTodoMovimientoMigrado()
    {
        var origen = MiembroInventado();
        var movimiento = MovimientoInventado(1, 1, new DateTime(2021, 1, 1), tipo: 3, monto: -50m, motivo: "Ajuste manual"); // Ajuste
        var (migrador, _, _, _, movimientosRepo, _) = CrearMigrador([origen], [movimiento]);

        await migrador.EjecutarAsync(AhoraUtc);

        Assert.Null(Assert.Single(movimientosRepo.Movimientos).UsuarioId);
    }

    [Fact]
    public async Task EjecutarAsync_RedondeaElMontoAlMigrarlo()
    {
        // I4: Redondeo.Monto is the single rounding point in the system.
        var origen = MiembroInventado();
        var movimiento = MovimientoInventado(1, 1, new DateTime(2021, 1, 1), tipo: 0, monto: 10.126m);
        var (migrador, _, _, _, movimientosRepo, _) = CrearMigrador([origen], [movimiento]);

        await migrador.EjecutarAsync(AhoraUtc);

        Assert.Equal(10.13m, Assert.Single(movimientosRepo.Movimientos).Monto);
    }

    [Fact]
    public async Task EjecutarAsync_CreaLosTresConsentimientosPorSocioFechadosEnLaFechaDeAlta()
    {
        var fechaAlta = new DateOnly(2023, 4, 2);
        var origen = MiembroInventado(fechaAlta: fechaAlta);
        var (migrador, _, _, miembros, _, consentimientos) = CrearMigrador([origen], []);

        var resultado = await migrador.EjecutarAsync(AhoraUtc);

        Assert.Equal(3, resultado.ConsentimientosCreados);
        var miembro = miembros.Miembros.Single();
        var propios = consentimientos.Consentimientos.Where(c => c.MiembroId == miembro.Id).ToList();

        Assert.Equal(3, propios.Count);
        Assert.Contains(propios, c => c.Tipo == TipoConsentimiento.DatosPersonales);
        Assert.Contains(propios, c => c.Tipo == TipoConsentimiento.DatosSensibles);
        Assert.Contains(propios, c => c.Tipo == TipoConsentimiento.Comunicaciones);
        Assert.All(propios, c =>
        {
            Assert.True(c.Otorgado);
            Assert.Equal(CanalConsentimiento.MigracionVerbal, c.Canal);
            Assert.Null(c.RegistradoPorUsuarioId);
            Assert.Equal(new DateTime(2023, 4, 2, 0, 0, 0, DateTimeKind.Utc), c.OcurridoEn);
        });
    }

    [Fact]
    public async Task EjecutarAsync_CorrerDosVeces_NoDuplicaMiembrosNiMovimientosNiConsentimientos()
    {
        var origen = MiembroInventado();
        var movimiento = MovimientoInventado(1, 1, new DateTime(2021, 1, 1), tipo: 0, monto: 500m);
        var (migrador, _, _, miembros, movimientosRepo, consentimientos) = CrearMigrador([origen], [movimiento]);

        var primera = await migrador.EjecutarAsync(AhoraUtc);
        var segunda = await migrador.EjecutarAsync(AhoraUtc.AddMinutes(5));

        Assert.Equal(1, primera.MiembrosCreados);
        Assert.Equal(0, segunda.MiembrosCreados);
        Assert.Equal(1, segunda.MiembrosYaExistian);

        Assert.Equal(1, primera.MovimientosMigrados);
        Assert.Equal(0, segunda.MovimientosMigrados);
        Assert.Equal(1, segunda.SociosConMovimientosYaMigrados);

        Assert.Equal(3, primera.ConsentimientosCreados);
        Assert.Equal(0, segunda.ConsentimientosCreados);
        Assert.Equal(3, segunda.ConsentimientosYaExistian);

        Assert.Single(miembros.Miembros);
        Assert.Single(movimientosRepo.Movimientos);
        Assert.Equal(3, consentimientos.Consentimientos.Count);
    }

    [Fact]
    public async Task EjecutarAsync_SocioConNombreVacio_SeSalteaYSeReportaPorClienteExternoId()
    {
        var origen = MiembroInventado(clienteExternoId: 9202, nombre: "   ");
        var (migrador, _, _, miembros, _, _) = CrearMigrador([origen], []);

        var resultado = await migrador.EjecutarAsync(AhoraUtc);

        Assert.Equal(0, resultado.MiembrosCreados);
        Assert.Empty(miembros.Miembros);
        var salteado = Assert.Single(resultado.MiembrosSalteados);
        Assert.Equal("9202", salteado.ClienteExternoId);
        Assert.Contains("nombre", salteado.Motivo, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EjecutarAsync_MovimientoConTipoDesconocido_SeSalteaYSeReportaSinTumbarElResto()
    {
        var origen = MiembroInventado(id: 1, clienteExternoId: 9303);
        OctavianoMovimiento[] movimientos =
        [
            MovimientoInventado(1, 1, new DateTime(2021, 1, 1), tipo: 99, monto: 10m), // unknown type
            MovimientoInventado(2, 1, new DateTime(2021, 2, 1), tipo: 0, monto: 500m),
        ];
        var (migrador, _, _, _, movimientosRepo, _) = CrearMigrador([origen], movimientos);

        var resultado = await migrador.EjecutarAsync(AhoraUtc);

        Assert.Equal(1, resultado.MovimientosMigrados);
        Assert.Single(movimientosRepo.Movimientos);
        var salteado = Assert.Single(resultado.MovimientosSalteados);
        Assert.Equal("9303", salteado.ClienteExternoId);
        Assert.Contains("desconocido", salteado.Motivo, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EjecutarAsync_MovimientoHuerfano_SeSalteaYSeReportaSinCrashear()
    {
        // No member with internal Octaviano id 999 exists — should never happen given the FK in
        // Octaviano's own schema, but the migrator must report it, not throw.
        var movimiento = MovimientoInventado(1, 999, new DateTime(2021, 1, 1), tipo: 0, monto: 10m);
        var (migrador, _, _, _, movimientosRepo, _) = CrearMigrador([], [movimiento]);

        var resultado = await migrador.EjecutarAsync(AhoraUtc);

        Assert.Empty(movimientosRepo.Movimientos);
        Assert.Single(resultado.MovimientosSalteados);
    }

    [Fact]
    public async Task EjecutarAsync_SoloAcumulacionLlevaConfiguracionId()
    {
        var origen = MiembroInventado(id: 1);
        OctavianoMovimiento[] movimientos =
        [
            MovimientoInventado(1, 1, new DateTime(2021, 1, 1), tipo: 0, monto: 500m),
            MovimientoInventado(2, 1, new DateTime(2021, 2, 1), tipo: 1, monto: 15m, referenciaVenta: 42),
            MovimientoInventado(3, 1, new DateTime(2021, 3, 1), tipo: 3, monto: -10m, motivo: "Ajuste"),
        ];
        var (migrador, _, _, _, movimientosRepo, _) = CrearMigrador([origen], movimientos);

        await migrador.EjecutarAsync(AhoraUtc);

        var porTipo = movimientosRepo.Movimientos.ToDictionary(m => m.Tipo);
        Assert.Null(porTipo[TipoMovimientoCredito.SaldoInicial].ConfiguracionId);
        Assert.NotNull(porTipo[TipoMovimientoCredito.Acumulacion].ConfiguracionId);
        Assert.Null(porTipo[TipoMovimientoCredito.Ajuste].ConfiguracionId);
    }

    [Fact]
    public async Task EjecutarAsync_FiltraTodoPorElMismoNegocioId()
    {
        var origen = MiembroInventado(id: 1);
        var movimiento = MovimientoInventado(1, 1, new DateTime(2021, 1, 1), tipo: 0, monto: 500m);
        var (migrador, _, _, miembros, movimientosRepo, _) = CrearMigrador([origen], [movimiento]);

        var resultado = await migrador.EjecutarAsync(AhoraUtc);

        Assert.All(miembros.Miembros, m => Assert.Equal(resultado.NegocioId, m.NegocioId));
        Assert.All(movimientosRepo.Movimientos, m => Assert.Equal(resultado.NegocioId, m.NegocioId));
    }
}

using System.Security.Claims;
using Fidelizar.Api.Controllers;
using Fidelizar.Api.Security;
using Fidelizar.Api.Tests.Controllers.Fakes;
using Fidelizar.Application.Services;
using Fidelizar.Domain.Entities;
using Fidelizar.Shared.Miembros;
using Fidelizar.Shared.Movimientos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using CanjeRequest = Fidelizar.Shared.Movimientos.RegistrarCanjeRequest;

namespace Fidelizar.Api.Tests.Controllers;

/// <summary>
/// Exercises the controller directly with fakes standing in for the Application services — no
/// HTTP pipeline, no database (ARCHITECTURE §11). Full-pipeline authorisation-by-role testing is
/// F1-15's job; this proves each action carries an <c>[Authorize]</c> policy, the state-changing
/// one carries <c>[AntiforgeryTokenRequired]</c>, and every action calls through to the real
/// services with <c>NegocioId</c> (and the acting user's id, where relevant) taken from the
/// token, never from the URL.
/// </summary>
public class MiembrosControllerTests
{
    private const int NegocioId = 7;
    private const int MiembroId = 42;
    private const int UsuarioId = 3;

    private static MiembrosController CrearControlador(
        FakeSaldoService? saldoService = null,
        FakeCorteService? corteService = null,
        FakeMiembroBusquedaService? busquedaService = null,
        FakeFichaMostradorService? fichaMostradorService = null,
        FakeFichaCompletaService? fichaCompletaService = null,
        FakeHistorialMovimientosService? historialMovimientosService = null,
        string rol = "Cajero")
    {
        var controller = new MiembrosController(
            saldoService ?? new FakeSaldoService(),
            corteService ?? new FakeCorteService(),
            busquedaService ?? new FakeMiembroBusquedaService(),
            fichaMostradorService ?? new FakeFichaMostradorService(),
            fichaCompletaService ?? new FakeFichaCompletaService(),
            historialMovimientosService ?? new FakeHistorialMovimientosService());

        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, UsuarioId.ToString()),
            new Claim(ClaimTypes.Role, rol),
            new Claim(JwtTokenService.NegocioIdClaim, NegocioId.ToString()),
        ]));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal },
        };

        return controller;
    }

    [Fact]
    public async Task ObtenerSaldo_usa_el_NegocioId_del_token_y_devuelve_saldo_y_corte()
    {
        var saldoService = new FakeSaldoService { SaldoARetornar = 12_400m };
        var corteService = new FakeCorteService { CorteARetornar = Corte.Declarar(NegocioId, new DateOnly(2026, 8, 4), UsuarioId, DateTime.UtcNow) };
        var controller = CrearControlador(saldoService, corteService);

        var resultado = await controller.ObtenerSaldo(MiembroId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(resultado);
        var respuesta = Assert.IsType<SaldoMiembroResponse>(ok.Value);
        Assert.Equal(MiembroId, respuesta.MiembroId);
        Assert.Equal(12_400m, respuesta.Saldo);
        Assert.Equal(new DateOnly(2026, 8, 4), respuesta.CorteFecha);
    }

    [Fact]
    public async Task RegistrarCanje_propaga_NegocioId_y_UsuarioId_del_token_al_servicio()
    {
        var saldoService = new FakeSaldoService
        {
            MovimientoARetornar = MovimientoCredito.Crear(
                NegocioId, MiembroId, new DateOnly(2026, 8, 18), DateTime.UtcNow,
                TipoMovimientoCredito.Canje, -300m, new DateOnly(2026, 8, 18), UsuarioId, "Canje de prueba"),
        };
        var controller = CrearControlador(saldoService);
        var request = new CanjeRequest(300m, new DateOnly(2026, 8, 18), "Canje de prueba");

        var resultado = await controller.RegistrarCanje(MiembroId, request, CancellationToken.None);

        Assert.IsType<OkObjectResult>(resultado);
        Assert.NotNull(saldoService.UltimoRequest);
        Assert.Equal(NegocioId, saldoService.UltimoRequest!.NegocioId);
        Assert.Equal(MiembroId, saldoService.UltimoRequest.MiembroId);
        Assert.Equal(UsuarioId, saldoService.UltimoRequest.UsuarioId);
        Assert.Equal(300m, saldoService.UltimoRequest.Monto);
    }

    [Fact]
    public async Task RegistrarCanje_devuelve_el_monto_positivo_y_el_saldo_resultante()
    {
        var saldoService = new FakeSaldoService
        {
            MovimientoARetornar = MovimientoCredito.Crear(
                NegocioId, MiembroId, new DateOnly(2026, 8, 18), DateTime.UtcNow,
                TipoMovimientoCredito.Canje, -300m, new DateOnly(2026, 8, 18), UsuarioId, "Canje de prueba"),
        };
        var controller = CrearControlador(saldoService);
        var request = new CanjeRequest(300m, new DateOnly(2026, 8, 18), "Canje de prueba");

        var resultado = await controller.RegistrarCanje(MiembroId, request, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(resultado);
        var respuesta = Assert.IsType<CanjeResponse>(ok.Value);
        Assert.Equal(300m, respuesta.Monto);
    }

    [Fact]
    public async Task BuscarMiembros_usa_el_NegocioId_del_token_y_devuelve_los_resultados_con_el_corte()
    {
        var busquedaService = new FakeMiembroBusquedaService
        {
            ResultadosARetornar = [new MiembroBusquedaResultado(MiembroId, "Ana Gómez", "0142", 12_400m, new DateOnly(2020, 1, 1))],
        };
        var corteService = new FakeCorteService { CorteARetornar = Corte.Declarar(NegocioId, new DateOnly(2026, 8, 4), UsuarioId, DateTime.UtcNow) };
        var controller = CrearControlador(corteService: corteService, busquedaService: busquedaService);

        var resultado = await controller.BuscarMiembros("ana gomez", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(resultado);
        var respuesta = Assert.IsType<List<MiembroResumen>>(ok.Value);
        Assert.Single(respuesta);
        Assert.Equal("Ana Gómez", respuesta[0].Nombre);
        Assert.Equal(new DateOnly(2026, 8, 4), respuesta[0].CorteFecha);
        Assert.Equal("ana gomez", busquedaService.UltimaQuery);
    }

    [Fact]
    public async Task FichaMostrador_devuelve_nombre_saldo_y_alertas()
    {
        var fichaMostradorService = new FakeFichaMostradorService
        {
            FichaARetornar = new FichaMostradorResultado(
                MiembroId, "Ana Gómez", "0142", 12_400m, new DateOnly(2026, 8, 4),
                [new AlertaMiembroResultado(TipoAlertaMiembro.Cumpleanos, "Cumple el 4/9")]),
        };
        var controller = CrearControlador(fichaMostradorService: fichaMostradorService);

        var resultado = await controller.FichaMostrador(MiembroId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(resultado);
        var respuesta = Assert.IsType<FichaMostradorResponse>(ok.Value);
        Assert.Equal("Ana Gómez", respuesta.Nombre);
        Assert.Single(respuesta.Alertas);
        Assert.Equal("Cumpleanos", respuesta.Alertas[0].Tipo);
    }

    [Fact]
    public async Task FichaCompleta_propaga_el_UsuarioId_del_token_para_la_auditoria()
    {
        var fichaCompletaService = new FakeFichaCompletaService
        {
            FichaARetornar = new FichaCompletaResultado(
                MiembroId, "Ana Gómez", "0142", "cliente-1", "11-5555-5555", "30111222", null, null, true),
        };
        var controller = CrearControlador(fichaCompletaService: fichaCompletaService, rol: "Encargada");

        var resultado = await controller.FichaCompleta(MiembroId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(resultado);
        var respuesta = Assert.IsType<FichaCompletaResponse>(ok.Value);
        Assert.Equal("11-5555-5555", respuesta.Telefono);
        Assert.Equal("30111222", respuesta.Dni);
        Assert.Equal(UsuarioId, fichaCompletaService.UltimoUsuarioIdQueLee);
    }

    [Fact]
    public async Task HistorialMovimientos_devuelve_los_movimientos_del_servicio()
    {
        var historialService = new FakeHistorialMovimientosService
        {
            HistorialARetornar =
            [
                new MovimientoHistorialItem(
                    1, TipoMovimientoCredito.Canje, -300m, new DateOnly(2026, 8, 18), DateTime.UtcNow,
                    "Canje de prueba", 700m, "Ana Cajera"),
            ],
        };
        var controller = CrearControlador(historialMovimientosService: historialService, rol: "Encargada");

        var resultado = await controller.HistorialMovimientos(MiembroId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(resultado);
        var respuesta = Assert.IsType<List<Fidelizar.Shared.Movimientos.MovimientoResponse>>(ok.Value);
        Assert.Single(respuesta);
        Assert.Equal("Canje", respuesta[0].Tipo);
        Assert.Equal("Ana Cajera", respuesta[0].UsuarioNombre);
    }

    [Theory]
    [InlineData(nameof(MiembrosController.ObtenerSaldo))]
    [InlineData(nameof(MiembrosController.RegistrarCanje))]
    [InlineData(nameof(MiembrosController.BuscarMiembros))]
    [InlineData(nameof(MiembrosController.FichaMostrador))]
    [InlineData(nameof(MiembrosController.FichaCompleta))]
    [InlineData(nameof(MiembrosController.HistorialMovimientos))]
    public void Cada_accion_exige_una_policy_de_autorizacion(string nombreAccion)
    {
        var metodo = typeof(MiembrosController).GetMethod(nombreAccion)!;

        var autorizaEnClase = typeof(MiembrosController).GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true).Any();
        var autorizaEnMetodo = metodo.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true).Any();

        Assert.True(autorizaEnClase || autorizaEnMetodo);
    }

    [Theory]
    [InlineData(nameof(MiembrosController.FichaCompleta))]
    [InlineData(nameof(MiembrosController.HistorialMovimientos))]
    public void S6_y_S7_exigen_EncargadaOrAbove_explicitamente(string nombreAccion)
    {
        var metodo = typeof(MiembrosController).GetMethod(nombreAccion)!;

        var autorizan = metodo.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>();

        Assert.Contains(autorizan, a => a.Policy == Policies.EncargadaOrAbove);
    }

    [Fact]
    public void RegistrarCanje_exige_token_antifalsificacion()
    {
        var metodo = typeof(MiembrosController).GetMethod(nameof(MiembrosController.RegistrarCanje))!;

        Assert.Contains(
            metodo.GetCustomAttributes(inherit: true),
            attr => attr is IAsyncAuthorizationFilter && attr is AntiforgeryTokenRequiredAttribute);
    }
}

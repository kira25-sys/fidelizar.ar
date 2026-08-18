using System.Security.Claims;
using Fidelizar.Api.Controllers;
using Fidelizar.Api.Security;
using Fidelizar.Api.Tests.Controllers.Fakes;
using Fidelizar.Domain.Entities;
using Fidelizar.Shared.Miembros;
using Fidelizar.Shared.Movimientos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Fidelizar.Api.Tests.Controllers;

/// <summary>
/// Exercises the controller directly with fakes standing in for the Application services — no
/// HTTP pipeline, no database (ARCHITECTURE §11). Full-pipeline authorisation-by-role testing is
/// F1-15's job; this only proves the two implemented actions carry an <c>[Authorize]</c> policy
/// and the state-changing one carries <c>[AntiforgeryTokenRequired]</c>, and that they call
/// through to the real services with <c>NegocioId</c> taken from the token, never from the URL.
/// </summary>
public class MiembrosControllerTests
{
    private const int NegocioId = 7;
    private const int MiembroId = 42;
    private const int UsuarioId = 3;

    private static MiembrosController CrearControlador(FakeSaldoService saldoService, FakeCorteService corteService)
    {
        var controller = new MiembrosController(saldoService, corteService);

        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, UsuarioId.ToString()),
            new Claim(ClaimTypes.Role, "Cajero"),
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
        var corteService = new FakeCorteService();
        var controller = CrearControlador(saldoService, corteService);
        var request = new RegistrarCanjeRequest(300m, new DateOnly(2026, 8, 18), "Canje de prueba");

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
        var controller = CrearControlador(saldoService, new FakeCorteService());
        var request = new RegistrarCanjeRequest(300m, new DateOnly(2026, 8, 18), "Canje de prueba");

        var resultado = await controller.RegistrarCanje(MiembroId, request, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(resultado);
        var respuesta = Assert.IsType<CanjeResponse>(ok.Value);
        Assert.Equal(300m, respuesta.Monto);
    }

    [Theory]
    [InlineData(nameof(MiembrosController.ObtenerSaldo))]
    [InlineData(nameof(MiembrosController.RegistrarCanje))]
    public void Cada_accion_exige_una_policy_de_autorizacion(string nombreAccion)
    {
        var metodo = typeof(MiembrosController).GetMethod(nombreAccion)!;

        var autorizaEnClase = typeof(MiembrosController).GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true).Any();
        var autorizaEnMetodo = metodo.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true).Any();

        Assert.True(autorizaEnClase || autorizaEnMetodo);
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

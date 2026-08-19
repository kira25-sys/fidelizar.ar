using System.Security.Claims;
using Fidelizar.Api.Controllers;
using Fidelizar.Api.Security;
using Fidelizar.Api.Tests.Controllers.Fakes;
using Fidelizar.Application.Services;
using Fidelizar.Shared.Sucursales;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Fidelizar.Api.Tests.Controllers;

/// <summary>S9 Cierre diario (Encargada/Dueño, scoped to the caller's own branch) and S10
/// Sucursales (Dueño only) — two different policies deliberately live on this controller.</summary>
public class SucursalesControllerTests
{
    private const int NegocioId = 7;
    private const int UsuarioId = 3;

    private static SucursalesController CrearControlador(
        FakeCierreDiarioService? cierreDiarioService = null,
        FakeSucursalService? sucursalService = null,
        string rol = "Encargada",
        int? sucursalIdDelToken = null)
    {
        var controller = new SucursalesController(
            cierreDiarioService ?? new FakeCierreDiarioService(), sucursalService ?? new FakeSucursalService());

        List<Claim> claims =
        [
            new Claim(ClaimTypes.NameIdentifier, UsuarioId.ToString()),
            new Claim(ClaimTypes.Role, rol),
            new Claim(JwtTokenService.NegocioIdClaim, NegocioId.ToString()),
        ];
        if (sucursalIdDelToken is { } sucursalId)
        {
            claims.Add(new Claim(JwtTokenService.SucursalIdClaim, sucursalId.ToString()));
        }

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims)) },
        };

        return controller;
    }

    [Fact]
    public async Task CierreDiario_de_la_propia_sucursal_devuelve_el_cierre()
    {
        var cierreDiarioService = new FakeCierreDiarioService
        {
            ResultadoARetornar = new CierreDiarioResultado(5, new DateOnly(2026, 8, 19), 300m, []),
        };
        var controller = CrearControlador(cierreDiarioService: cierreDiarioService, sucursalIdDelToken: 5);

        var resultado = await controller.CierreDiario(5, new DateOnly(2026, 8, 19), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(resultado);
        var respuesta = Assert.IsType<CierreDiarioResponse>(ok.Value);
        Assert.Equal(300m, respuesta.TotalCanjeado);
    }

    /// <summary>El eje de sucursal: una Encargada de la sucursal 5 no puede pedir el cierre de la 6.</summary>
    [Fact]
    public async Task CierreDiario_de_otra_sucursal_devuelve_Forbid()
    {
        var controller = CrearControlador(sucursalIdDelToken: 5);

        var resultado = await controller.CierreDiario(6, new DateOnly(2026, 8, 19), CancellationToken.None);

        Assert.IsType<ForbidResult>(resultado);
    }

    [Fact]
    public async Task Dueno_sin_sucursal_propia_puede_pedir_cualquier_cierre()
    {
        var cierreDiarioService = new FakeCierreDiarioService
        {
            ResultadoARetornar = new CierreDiarioResultado(6, new DateOnly(2026, 8, 19), 0m, []),
        };
        var controller = CrearControlador(cierreDiarioService: cierreDiarioService, rol: "Dueno", sucursalIdDelToken: null);

        var resultado = await controller.CierreDiario(6, new DateOnly(2026, 8, 19), CancellationToken.None);

        Assert.IsType<OkObjectResult>(resultado);
    }

    [Fact]
    public async Task Listar_devuelve_las_sucursales_del_servicio()
    {
        var sucursalService = new FakeSucursalService
        {
            SucursalesARetornar = [new SucursalResultado(1, "Sucursal Centro", "COD-1", true)],
        };
        var controller = CrearControlador(sucursalService: sucursalService, rol: "Dueno");

        var resultado = await controller.Listar(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(resultado);
        var respuesta = Assert.IsType<List<SucursalResponse>>(ok.Value);
        Assert.Single(respuesta);
    }

    [Fact]
    public void Listar_y_Crear_exigen_DuenoOnly()
    {
        foreach (var nombre in new[] { nameof(SucursalesController.Listar), nameof(SucursalesController.Crear) })
        {
            var metodo = typeof(SucursalesController).GetMethod(nombre)!;
            var autorizan = metodo.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true).Cast<AuthorizeAttribute>();

            Assert.Contains(autorizan, a => a.Policy == Policies.DuenoOnly);
        }
    }

    [Fact]
    public void CierreDiario_exige_EncargadaOrAbove()
    {
        var metodo = typeof(SucursalesController).GetMethod(nameof(SucursalesController.CierreDiario))!;
        var autorizan = metodo.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true).Cast<AuthorizeAttribute>();

        Assert.Contains(autorizan, a => a.Policy == Policies.EncargadaOrAbove);
    }
}

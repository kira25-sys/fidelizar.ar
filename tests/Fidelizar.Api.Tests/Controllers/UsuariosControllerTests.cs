using System.Security.Claims;
using Fidelizar.Api.Controllers;
using Fidelizar.Api.Security;
using Fidelizar.Api.Tests.Controllers.Fakes;
using Fidelizar.Application.Services;
using Fidelizar.Domain.Entities;
using Fidelizar.Domain.Exceptions;
using Fidelizar.Shared.Usuarios;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Fidelizar.Api.Tests.Controllers;

/// <summary>S10 Usuarios — Dueño only.</summary>
public class UsuariosControllerTests
{
    private const int NegocioId = 7;
    private const int UsuarioId = 3;

    private static UsuariosController CrearControlador(FakeUsuarioService? servicio = null)
    {
        var controller = new UsuariosController(servicio ?? new FakeUsuarioService());

        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, UsuarioId.ToString()),
            new Claim(ClaimTypes.Role, "Dueno"),
            new Claim(JwtTokenService.NegocioIdClaim, NegocioId.ToString()),
        ]));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal },
        };

        return controller;
    }

    [Fact]
    public async Task Crear_parsea_el_rol_y_propaga_el_NegocioId_del_token()
    {
        var servicio = new FakeUsuarioService
        {
            UsuarioCreadoARetornar = new UsuarioResultado(1, "Ana Cajera", "ana@x.com", RolUsuario.Cajero, 5, true),
        };
        var controller = CrearControlador(servicio);
        var request = new CrearUsuarioRequest("Ana Cajera", "ana@x.com", "una-contrasena", "Cajero", 5);

        var resultado = await controller.Crear(request, CancellationToken.None);

        Assert.IsType<CreatedAtActionResult>(resultado);
        Assert.NotNull(servicio.UltimaSolicitud);
        Assert.Equal(NegocioId, servicio.UltimaSolicitud!.NegocioId);
        Assert.Equal(RolUsuario.Cajero, servicio.UltimaSolicitud.Rol);
    }

    /// <summary>Sistema es un valor válido del enum de Domain pero nunca una cuenta creable
    /// desde este endpoint.</summary>
    [Fact]
    public async Task Crear_con_rol_Sistema_se_rechaza()
    {
        var controller = CrearControlador();
        var request = new CrearUsuarioRequest("Alguien", "alguien@x.com", "una-contrasena", "Sistema", null);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => controller.Crear(request, CancellationToken.None));
        Assert.Equal("ROL_INVALIDO", ex.ErrorCode);
    }

    [Fact]
    public async Task Crear_con_rol_desconocido_se_rechaza()
    {
        var controller = CrearControlador();
        var request = new CrearUsuarioRequest("Alguien", "alguien@x.com", "una-contrasena", "Supervisor", null);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => controller.Crear(request, CancellationToken.None));
        Assert.Equal("ROL_INVALIDO", ex.ErrorCode);
    }

    [Fact]
    public async Task Listar_devuelve_los_usuarios_del_servicio()
    {
        var servicio = new FakeUsuarioService
        {
            UsuariosARetornar = [new UsuarioResultado(1, "Ana Cajera", "ana@x.com", RolUsuario.Cajero, 5, true)],
        };
        var controller = CrearControlador(servicio);

        var resultado = await controller.Listar(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(resultado);
        var respuesta = Assert.IsType<List<UsuarioResponse>>(ok.Value);
        Assert.Single(respuesta);
        Assert.Equal("Cajero", respuesta[0].Rol);
    }

    [Fact]
    public void Clase_exige_DuenoOnly()
    {
        var autorizan = typeof(UsuariosController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>();

        Assert.Contains(autorizan, a => a.Policy == Policies.DuenoOnly);
    }
}

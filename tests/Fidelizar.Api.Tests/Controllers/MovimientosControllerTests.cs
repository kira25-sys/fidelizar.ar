using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Fidelizar.Api.Controllers;
using Fidelizar.Api.Security;
using Fidelizar.Api.Tests.Controllers.Fakes;
using Fidelizar.Domain.Entities;
using Fidelizar.Shared.Movimientos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.DependencyInjection;

namespace Fidelizar.Api.Tests.Controllers;

/// <summary>S8 Anular movimiento — I1 surfaced in the UI (no edit, no delete, only a new
/// Ajuste).</summary>
public class MovimientosControllerTests
{
    private const int NegocioId = 7;
    private const int UsuarioId = 3;

    private static MovimientosController CrearControlador(FakeAnulacionMovimientoService servicio)
    {
        var controller = new MovimientosController(servicio);

        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, UsuarioId.ToString()),
            new Claim(ClaimTypes.Role, "Encargada"),
            new Claim(JwtTokenService.NegocioIdClaim, NegocioId.ToString()),
        ]));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal },
        };

        return controller;
    }

    [Fact]
    public async Task Anular_propaga_NegocioId_y_UsuarioId_del_token()
    {
        var servicio = new FakeAnulacionMovimientoService
        {
            AjusteARetornar = MovimientoCredito.Crear(
                NegocioId, 42, new DateOnly(2026, 8, 19), DateTime.UtcNow,
                TipoMovimientoCredito.Ajuste, 300m, new DateOnly(2026, 8, 19), UsuarioId, "Corrección"),
        };
        var controller = CrearControlador(servicio);

        var resultado = await controller.Anular(100L, new AnularMovimientoRequest("Corrección", "clave-de-la-anulacion"), CancellationToken.None);

        Assert.IsType<OkObjectResult>(resultado);
        Assert.NotNull(servicio.UltimoRequest);
        Assert.Equal(NegocioId, servicio.UltimoRequest!.NegocioId);
        Assert.Equal(UsuarioId, servicio.UltimoRequest.UsuarioId);
        Assert.Equal(100L, servicio.UltimoRequest.MovimientoId);
        Assert.Equal("clave-de-la-anulacion", servicio.UltimoRequest.ClaveIdempotencia);
    }

    [Fact]
    public void Clase_exige_EncargadaOrAbove()
    {
        var autorizan = typeof(MovimientosController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>();

        Assert.Contains(autorizan, a => a.Policy == Policies.EncargadaOrAbove);
    }

    /// <summary>README decisión #6 (S8, 2026-08-21): sin clave el endpoint no puede distinguir un
    /// reintento de una segunda anulación. El body la exige con las mismas anotaciones que S4, así
    /// que una clave ausente la rechaza MVC con 400 antes de llegar al controlador.</summary>
    [Fact]
    public void El_body_exige_ClaveIdempotencia_como_el_de_canje()
    {
        var servicios = new ServiceCollection();
        servicios.AddLogging();
        servicios.AddMvcCore().AddDataAnnotations();
        using var proveedor = servicios.BuildServiceProvider();

        var metadata = proveedor.GetRequiredService<IModelMetadataProvider>()
            .GetMetadataForProperties(typeof(AnularMovimientoRequest))
            .Single(m => m.Name == nameof(AnularMovimientoRequest.ClaveIdempotencia));

        Assert.Contains(metadata.ValidatorMetadata, v => v is RequiredAttribute);
        Assert.Contains(metadata.ValidatorMetadata, v => v is StringLengthAttribute { MaximumLength: 100 });
    }

    [Fact]
    public void Anular_exige_token_antifalsificacion()
    {
        var metodo = typeof(MovimientosController).GetMethod(nameof(MovimientosController.Anular))!;

        Assert.Contains(
            metodo.GetCustomAttributes(inherit: true),
            attr => attr is IAsyncAuthorizationFilter && attr is AntiforgeryTokenRequiredAttribute);
    }
}

using System.Security.Claims;
using Fidelizar.Api.Monitoreo;
using Fidelizar.Api.Security;
using Microsoft.AspNetCore.Http;
using Serilog.Core;
using Serilog.Events;
using Serilog.Parsing;

namespace Fidelizar.Api.Tests.Monitoreo;

/// <summary>
/// F1-18, ARCHITECTURE §14 "structured application logs, retained per client" and I8: a log line
/// that cannot be attributed to a business is useless when one host serves several. Every value
/// here is invented (CLAUDE.md).
/// </summary>
public class EnriquecedorDeNegocioTests
{
    [Fact]
    public void Una_peticion_autenticada_deja_NegocioId_y_UsuarioId_en_la_linea()
    {
        var evento = Enriquecer(PrincipalDe(negocioId: "7", usuarioId: "42", nombre: "Nombre Inventado"));

        Assert.Equal("7", Propiedad(evento, EnriquecedorDeNegocio.PropiedadNegocio));
        Assert.Equal("42", Propiedad(evento, EnriquecedorDeNegocio.PropiedadUsuario));
    }

    /// <summary>
    /// CLAUDE.md: nothing personal in a log. <c>ClaimTypes.Name</c> carries the user's full name
    /// and must never be enriched, however convenient it would be to read.
    /// </summary>
    [Fact]
    public void El_nombre_del_usuario_nunca_llega_a_la_linea()
    {
        var evento = Enriquecer(PrincipalDe(negocioId: "7", usuarioId: "42", nombre: "Nombre Inventado"));

        Assert.DoesNotContain(
            evento.Properties.Values.Select(v => v.ToString()),
            valor => valor.Contains("Nombre Inventado", StringComparison.Ordinal));
    }

    [Fact]
    public void Una_peticion_sin_sesion_no_inventa_un_negocio()
    {
        var evento = Enriquecer(new ClaimsPrincipal(new ClaimsIdentity()));

        Assert.False(evento.Properties.ContainsKey(EnriquecedorDeNegocio.PropiedadNegocio));
        Assert.False(evento.Properties.ContainsKey(EnriquecedorDeNegocio.PropiedadUsuario));
    }

    [Fact]
    public void Sin_HttpContext_el_enriquecedor_no_falla()
    {
        var enriquecedor = new EnriquecedorDeNegocio(new HttpContextAccessor());
        var evento = EventoVacio();

        enriquecedor.Enrich(evento, new PropertyFactory());

        Assert.Empty(evento.Properties);
    }

    private static LogEvent Enriquecer(ClaimsPrincipal principal)
    {
        var contexto = new DefaultHttpContext { User = principal };
        var enriquecedor = new EnriquecedorDeNegocio(new HttpContextAccessor { HttpContext = contexto });
        var evento = EventoVacio();

        enriquecedor.Enrich(evento, new PropertyFactory());

        return evento;
    }

    private static ClaimsPrincipal PrincipalDe(string negocioId, string usuarioId, string nombre) =>
        new(new ClaimsIdentity(
            [
                new Claim(JwtTokenService.NegocioIdClaim, negocioId),
                new Claim(ClaimTypes.NameIdentifier, usuarioId),
                new Claim(ClaimTypes.Name, nombre),
            ],
            authenticationType: "Test"));

    private static LogEvent EventoVacio() =>
        new(DateTimeOffset.UtcNow, LogEventLevel.Information, exception: null,
            new MessageTemplate([]), []);

    private static string? Propiedad(LogEvent evento, string nombre) =>
        evento.Properties.TryGetValue(nombre, out var valor) ? ((ScalarValue)valor).Value?.ToString() : null;

    private sealed class PropertyFactory : ILogEventPropertyFactory
    {
        public LogEventProperty CreateProperty(string name, object? value, bool destructureObjects = false) =>
            new(name, new ScalarValue(value));
    }
}

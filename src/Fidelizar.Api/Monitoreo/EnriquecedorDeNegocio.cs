using System.Security.Claims;
using Fidelizar.Api.Security;
using Serilog.Core;
using Serilog.Events;

namespace Fidelizar.Api.Monitoreo;

/// <summary>
/// Makes every log line attributable to a business (ARCHITECTURE §14: "structured application
/// logs, retained per client"; I8: <c>NegocioId</c> everywhere). Ids only — <c>ClaimTypes.Name</c>
/// holds the user's full name and is deliberately never enriched (CLAUDE.md).
/// </summary>
public sealed class EnriquecedorDeNegocio(IHttpContextAccessor accessor) : ILogEventEnricher
{
    public const string PropiedadNegocio = "NegocioId";
    public const string PropiedadUsuario = "UsuarioId";

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var usuario = accessor.HttpContext?.User;

        if (usuario?.Identity?.IsAuthenticated != true)
        {
            return;
        }

        Agregar(logEvent, propertyFactory, PropiedadNegocio, usuario.FindFirstValue(JwtTokenService.NegocioIdClaim));
        Agregar(logEvent, propertyFactory, PropiedadUsuario, usuario.FindFirstValue(ClaimTypes.NameIdentifier));
    }

    private static void Agregar(
        LogEvent logEvent, ILogEventPropertyFactory propertyFactory, string nombre, string? valor)
    {
        if (!string.IsNullOrEmpty(valor))
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(nombre, valor));
        }
    }
}

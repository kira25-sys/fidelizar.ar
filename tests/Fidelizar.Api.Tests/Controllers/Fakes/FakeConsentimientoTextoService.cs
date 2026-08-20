using Fidelizar.Application.Services;
using Fidelizar.Domain.Entities;

namespace Fidelizar.Api.Tests.Controllers.Fakes;

/// <summary>No database involved (ARCHITECTURE §11).</summary>
public sealed class FakeConsentimientoTextoService : IConsentimientoTextoService
{
    public ConsentimientoTextoResultado? ResultadoARetornar { get; set; }

    /// <summary>What the controller passed — the token's NegocioId, never a value from the URL (I8).</summary>
    public int? NegocioIdRecibido { get; private set; }

    public Task<ConsentimientoTextoResultado> ObtenerAsync(
        int negocioId, TipoConsentimiento tipo, CancellationToken cancellationToken = default)
    {
        NegocioIdRecibido = negocioId;
        return Task.FromResult(ResultadoARetornar ?? new ConsentimientoTextoResultado(tipo, "version-de-prueba", "texto de prueba"));
    }
}

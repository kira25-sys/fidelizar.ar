using Fidelizar.Domain.Consentimientos;
using Fidelizar.Domain.Entities;
using Fidelizar.Domain.Exceptions;
using Fidelizar.Domain.Repositories;

namespace Fidelizar.Application.Services;

/// <summary>See <see cref="IConsentimientoTextoService"/>.</summary>
public sealed class ConsentimientoTextoService(INegocioRepository negocioRepository) : IConsentimientoTextoService
{
    public async Task<ConsentimientoTextoResultado> ObtenerAsync(
        int negocioId, TipoConsentimiento tipo, CancellationToken cancellationToken = default)
    {
        var negocio = await negocioRepository.ObtenerUnicoAsync(cancellationToken);

        // I8 / ARCHITECTURE §5: the tenant is checked, not assumed. One business per deployment
        // makes a mismatch impossible in practice — which is exactly why it must fail loudly if it
        // ever happens, instead of handing a token from elsewhere this business's CUIT and address.
        if (negocio.Id != negocioId)
        {
            throw new ConflictException(
                $"El negocio {negocioId} del token no es el de esta base de datos.", "NEGOCIO_AJENO");
        }

        var (versionTexto, texto) = tipo switch
        {
            TipoConsentimiento.DatosPersonales => TextosConsentimiento.DatosPersonalesPara(negocio),
            TipoConsentimiento.DatosSensibles => TextosConsentimiento.DatosSensiblesPara(negocio),
            _ => throw new ValidationException(
                $"No hay un texto de consentimiento aprobado para '{tipo}'.",
                "TIPO_CONSENTIMIENTO_SIN_TEXTO"),
        };

        return new ConsentimientoTextoResultado(tipo, versionTexto, texto);
    }
}

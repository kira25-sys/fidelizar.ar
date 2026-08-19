using Fidelizar.Domain.Consentimientos;
using Fidelizar.Domain.Entities;
using Fidelizar.Domain.Repositories;

namespace Fidelizar.Application.Services;

/// <summary>
/// Granting, withdrawing and querying consent, plus the one call every future write path for a
/// sensitive field must make first (I10). Knows nothing about HTTP or EF (ARCHITECTURE §3); the
/// actual "is this enough" judgment lives in <see cref="ConsentimientoPolicy"/>, pure and
/// DB-free, so it stays testable with no database and cannot quietly diverge between call sites.
/// </summary>
public sealed class ConsentimientoService(IConsentimientoRepository consentimientoRepository) : IConsentimientoService
{
    public Task<Consentimiento?> ObtenerVigenteAsync(
        int negocioId, int miembroId, TipoConsentimiento tipo, CancellationToken cancellationToken = default) =>
        consentimientoRepository.GetVigenteAsync(negocioId, miembroId, tipo, cancellationToken);

    public async Task<bool> EstaVigenteAsync(
        int negocioId, int miembroId, TipoConsentimiento tipo, CancellationToken cancellationToken = default)
    {
        var vigente = await consentimientoRepository.GetVigenteAsync(negocioId, miembroId, tipo, cancellationToken);
        return ConsentimientoPolicy.PermiteEscritura(vigente, tipo);
    }

    public Task<Consentimiento> OtorgarAsync(
        OtorgarConsentimientoRequest request, CancellationToken cancellationToken = default)
    {
        var consentimiento = Consentimiento.Registrar(
            negocioId: request.NegocioId,
            miembroId: request.MiembroId,
            tipo: request.Tipo,
            otorgado: true,
            versionTexto: request.VersionTexto,
            canal: request.Canal,
            ocurridoEn: request.OcurridoEn,
            registradoPorUsuarioId: request.RegistradoPorUsuarioId);

        return consentimientoRepository.AppendAsync(consentimiento, cancellationToken);
    }

    public Task<Consentimiento> RevocarAsync(
        RevocarConsentimientoRequest request, CancellationToken cancellationToken = default)
    {
        var consentimiento = Consentimiento.Registrar(
            negocioId: request.NegocioId,
            miembroId: request.MiembroId,
            tipo: request.Tipo,
            otorgado: false,
            versionTexto: request.VersionTexto,
            canal: request.Canal,
            ocurridoEn: request.OcurridoEn,
            registradoPorUsuarioId: request.RegistradoPorUsuarioId);

        return consentimientoRepository.AppendAsync(consentimiento, cancellationToken);
    }

    public async Task RequerirVigenteAsync(
        int negocioId, int miembroId, TipoConsentimiento tipo, CancellationToken cancellationToken = default)
    {
        var vigente = await consentimientoRepository.GetVigenteAsync(negocioId, miembroId, tipo, cancellationToken);
        ConsentimientoPolicy.RequerirVigente(vigente, tipo);
    }
}

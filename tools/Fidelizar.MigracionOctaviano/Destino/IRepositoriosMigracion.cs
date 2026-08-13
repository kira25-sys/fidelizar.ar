using Fidelizar.Domain.Entities;

namespace Fidelizar.MigracionOctaviano.Destino;

/// <summary>
/// Tiny, tool-local repository interfaces for the three aggregates that have no
/// <c>Fidelizar.Domain.Repositories</c> interface yet (<c>Negocio</c>, <c>ConfiguracionPrograma</c>,
/// <c>Consentimiento</c> — none is built by any task before F0-09). Kept out of
/// <c>Fidelizar.Domain</c> on purpose: they exist only so this one-off tool's migration logic can
/// be tested against fakes built from invented data (CLAUDE.md), the same way
/// <c>Fidelizar.Application.Tests</c> fakes <c>IMovimientoRepository</c>. They follow the same
/// "no generic repository" discipline as ARCHITECTURE §3 even though that rule technically binds
/// only <c>src/</c> — no reason to build this one differently.
/// </summary>
public interface INegocioRepository
{
    /// <summary>The single business row this migration hangs everything off, or null before the
    /// first run creates it (single-tenant deployment — DATA-MODEL §1).</summary>
    Task<Negocio?> ObtenerUnicoAsync(CancellationToken cancellationToken = default);

    Task<Negocio> CrearAsync(Negocio negocio, CancellationToken cancellationToken = default);
}

public interface IConfiguracionProgramaRepository
{
    Task<ConfiguracionPrograma?> ObtenerVigenteAsync(int negocioId, CancellationToken cancellationToken = default);

    Task<ConfiguracionPrograma> CrearAsync(ConfiguracionPrograma configuracion, CancellationToken cancellationToken = default);
}

public interface IConsentimientoRepository
{
    /// <summary>Whether a consent of this type already exists for this member — append-only, so
    /// "already exists" is enough to make writing a second one on a re-run a no-op.</summary>
    Task<bool> ExisteAsync(
        int negocioId, int miembroId, TipoConsentimiento tipo, CancellationToken cancellationToken = default);

    Task<Consentimiento> CrearAsync(Consentimiento consentimiento, CancellationToken cancellationToken = default);
}

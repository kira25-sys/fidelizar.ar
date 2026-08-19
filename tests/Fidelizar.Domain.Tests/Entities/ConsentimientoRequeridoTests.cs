using Fidelizar.Domain.Consentimientos;
using Fidelizar.Domain.Entities;
using Fidelizar.Domain.Exceptions;

namespace Fidelizar.Domain.Tests.Entities;

/// <summary>
/// I10 — sensitive fields (diet, allergies and other health-adjacent data on
/// <c>PerfilMiembro</c>) cannot be written without a recorded, granted consent of type
/// <c>DatosSensibles</c> (DATA-MODEL §3).
///
/// <c>PerfilMiembro</c> itself is still F3-01 and is out of this task's scope (F1-08's brief:
/// build the guard, not the entity it will guard). What this test demonstrates instead is the
/// guard <c>PerfilMiembroService</c> will be required to call before persisting anything —
/// <see cref="ConsentimientoPolicy.RequerirVigente"/>, exercised here exactly as any future
/// write path must exercise it. See also <c>ConsentimientoPolicyTests</c> for the full set of
/// negative cases (no consent, revoked, wrong type) and <c>ConsentimientoServiceTests</c> for the
/// same rule wired through the repository.
/// </summary>
public class ConsentimientoRequeridoTests
{
    [Fact]
    public void No_se_puede_escribir_PerfilMiembro_sin_un_Consentimiento_DatosSensibles_vigente()
    {
        // Simulates the check a future PerfilMiembroService.ActualizarAsync must make before
        // writing Dieta/Alergias: no Consentimiento of type DatosSensibles on record for this
        // member at all.
        Consentimiento? vigente = null;

        var ex = Assert.Throws<ValidationException>(
            () => ConsentimientoPolicy.RequerirVigente(vigente, TipoConsentimiento.DatosSensibles));

        Assert.Equal("CONSENTIMIENTO_REQUERIDO", ex.ErrorCode);
    }
}

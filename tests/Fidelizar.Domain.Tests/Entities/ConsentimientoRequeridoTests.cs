namespace Fidelizar.Domain.Tests.Entities;

/// <summary>
/// I10 — sensitive fields (diet, allergies, health-adjacent data on <c>PerfilMiembro</c>) cannot
/// be written without a recorded consent (<c>Consentimiento</c> of type <c>DatosSensibles</c>,
/// DATA-MODEL §3). Neither entity exists in <c>src/</c> in this wave — both are phase 1 (F1-08).
///
/// There is nothing to test yet: inventing either entity here to make a green test would test
/// code that does not exist, which qa-data's brief explicitly forbids. This skip is the honest
/// record of the gap until F1-08 lands.
/// </summary>
public class ConsentimientoRequeridoTests
{
    [Fact(Skip = "Requiere Consentimiento y PerfilMiembro — F1-08")]
    public void No_se_puede_escribir_PerfilMiembro_sin_un_Consentimiento_DatosSensibles_vigente()
    {
    }
}

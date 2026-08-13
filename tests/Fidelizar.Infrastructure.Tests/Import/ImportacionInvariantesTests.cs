namespace Fidelizar.Infrastructure.Tests.Import;

/// <summary>
/// I5 — an ambiguous amount is rejected, never guessed. The parsing half of that is already
/// covered exhaustively by <c>Fidelizar.Domain.Tests.Money.MontoParserTests</c>: every case of the
/// genuinely ambiguous "single separator, exactly 3 digits to the right" returns <c>false</c> with
/// <c>ambiguous = true</c>, and is never silently guessed.
///
/// What is NOT testable yet is the other half DATA-MODEL §5 promises: that a rejected row is
/// preserved for a human to review, in <c>FilaRechazada</c>. That table — and the canonical sales
/// importer that would write to it — is phase 2 (F2-02) and does not exist in <c>src/</c> yet.
/// <c>VipPadronImporter</c> (phase 0) already handles an ambiguous credit today, but differently
/// and on purpose: it reports a warning string and loads the row with credit zero (see
/// <c>VipPadronImporterTests.ImportAsync_CreditoAmbiguo_SeCargaComoCeroYAvisaConLaFilaYComoReexportar</c>)
/// — a deliberate, documented behaviour for the one-off phase-0 roster import, not the general
/// <c>FilaRechazada</c> mechanism I5 describes for ongoing sales ingestion.
/// </summary>
public class ImportacionInvariantesTests
{
    [Fact(Skip = "Requiere FilaRechazada y el importador canónico de ventas — F2-02")]
    public void Una_fila_de_ventas_con_monto_ambiguo_se_persiste_en_FilaRechazada_para_revision_humana()
    {
    }
}

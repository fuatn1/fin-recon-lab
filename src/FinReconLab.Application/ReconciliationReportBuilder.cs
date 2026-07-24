using FinReconLab.Domain;

namespace FinReconLab.Application;

public sealed class ReconciliationReportBuilder
{
    public ReconciliationReport Build(
        ScenarioDefinition scenario,
        ReconciliationCutoff cutoff,
        IEnumerable<ExpectedPaymentSnapshot> expectedSnapshots,
        IEnumerable<ObservedPaymentSnapshot> observedSnapshots,
        IEnumerable<ReconciliationFinding> findings)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(expectedSnapshots);
        ArgumentNullException.ThrowIfNull(observedSnapshots);
        ArgumentNullException.ThrowIfNull(findings);

        return new ReconciliationReport(
            ReconciliationReport.SupportedSchemaVersion,
            scenario,
            cutoff,
            expectedSnapshots,
            observedSnapshots,
            findings);
    }
}

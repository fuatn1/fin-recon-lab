namespace FinReconLab.Domain;

public sealed record ReconciliationReport
{
    public const string SupportedSchemaVersion = "reconciliation-report.v1";

    public ReconciliationReport(
        string schemaVersion,
        ScenarioDefinition scenario,
        ReconciliationCutoff cutoff,
        IEnumerable<ExpectedPaymentSnapshot> expectedSnapshots,
        IEnumerable<ObservedPaymentSnapshot> observedSnapshots,
        IEnumerable<ReconciliationFinding> findings)
    {
        if (!string.Equals(schemaVersion, SupportedSchemaVersion, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Unsupported reconciliation report schema version '{schemaVersion}'. Expected '{SupportedSchemaVersion}'.",
                nameof(schemaVersion));
        }

        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(expectedSnapshots);
        ArgumentNullException.ThrowIfNull(observedSnapshots);
        ArgumentNullException.ThrowIfNull(findings);

        var orderedExpectedSnapshots = expectedSnapshots
            .Select(snapshot => snapshot ?? throw new ArgumentException(
                "Expected snapshots cannot contain null values.",
                nameof(expectedSnapshots)))
            .OrderBy(snapshot => snapshot.OrderId, StringComparer.Ordinal)
            .ToArray();

        var orderedObservedSnapshots = observedSnapshots
            .Select(snapshot => snapshot ?? throw new ArgumentException(
                "Observed snapshots cannot contain null values.",
                nameof(observedSnapshots)))
            .OrderBy(snapshot => snapshot.OrderId, StringComparer.Ordinal)
            .ToArray();

        ValidateSnapshotPairs(orderedExpectedSnapshots, orderedObservedSnapshots, cutoff);

        var orderedFindings = findings
            .Select(finding => finding ?? throw new ArgumentException(
                "Findings cannot contain null values.",
                nameof(findings)))
            .OrderBy(finding => finding.OrderId, StringComparer.Ordinal)
            .ThenBy(finding => finding.Category)
            .ThenBy(finding => finding.ExpectedAmount.Amount)
            .ThenBy(finding => finding.ObservedAmount.Amount)
            .ThenBy(finding => finding.SignedDelta.Amount)
            .ToArray();

        ValidateFindings(
            orderedFindings,
            orderedExpectedSnapshots,
            orderedObservedSnapshots,
            cutoff);

        SchemaVersion = schemaVersion;
        Scenario = scenario;
        Cutoff = cutoff;
        ExpectedSnapshots = new ValueReadOnlyList<ExpectedPaymentSnapshot>(orderedExpectedSnapshots);
        ObservedSnapshots = new ValueReadOnlyList<ObservedPaymentSnapshot>(orderedObservedSnapshots);
        Findings = new ValueReadOnlyList<ReconciliationFinding>(orderedFindings);
    }

    public string SchemaVersion { get; }

    public ScenarioDefinition Scenario { get; }

    public ReconciliationCutoff Cutoff { get; }

    public IReadOnlyList<ExpectedPaymentSnapshot> ExpectedSnapshots { get; }

    public IReadOnlyList<ObservedPaymentSnapshot> ObservedSnapshots { get; }

    public IReadOnlyList<ReconciliationFinding> Findings { get; }

    private static void ValidateSnapshotPairs(
        IReadOnlyList<ExpectedPaymentSnapshot> expectedSnapshots,
        IReadOnlyList<ObservedPaymentSnapshot> observedSnapshots,
        ReconciliationCutoff cutoff)
    {
        if (expectedSnapshots.Count != observedSnapshots.Count)
        {
            throw new InvalidOperationException(
                "Expected and observed snapshot collections must contain the same number of orders.");
        }

        for (var index = 0; index < expectedSnapshots.Count; index++)
        {
            var expected = expectedSnapshots[index];
            var observed = observedSnapshots[index];

            if (expected.Cutoff != cutoff || observed.Cutoff != cutoff)
            {
                throw new InvalidOperationException(
                    "Every snapshot must use the report reconciliation cutoff.");
            }

            if (!string.Equals(expected.OrderId, observed.OrderId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Expected and observed snapshots must describe the same ordered set of order ids.");
            }

            if (!string.Equals(
                    expected.CapturedAmount.Currency,
                    observed.CapturedAmount.Currency,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Expected and observed snapshots for order '{expected.OrderId}' must use the same currency.");
            }

            if (index > 0 &&
                string.Equals(expectedSnapshots[index - 1].OrderId, expected.OrderId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"The report cannot contain more than one snapshot pair for order '{expected.OrderId}'.");
            }
        }
    }

    private static void ValidateFindings(
        IReadOnlyList<ReconciliationFinding> findings,
        IReadOnlyList<ExpectedPaymentSnapshot> expectedSnapshots,
        IReadOnlyList<ObservedPaymentSnapshot> observedSnapshots,
        ReconciliationCutoff cutoff)
    {
        var snapshotIndex = expectedSnapshots
            .Select((snapshot, index) => (snapshot.OrderId, Index: index))
            .ToDictionary(pair => pair.OrderId, pair => pair.Index, StringComparer.Ordinal);

        foreach (var finding in findings)
        {
            if (finding.Cutoff != cutoff)
            {
                throw new InvalidOperationException(
                    $"Finding for order '{finding.OrderId}' must use the report reconciliation cutoff.");
            }

            if (!snapshotIndex.TryGetValue(finding.OrderId, out var index))
            {
                throw new InvalidOperationException(
                    $"Finding for order '{finding.OrderId}' does not have a matching snapshot pair.");
            }

            var expected = expectedSnapshots[index];
            var observed = observedSnapshots[index];

            if (finding.ExpectedAmount != expected.CapturedAmount ||
                finding.ObservedAmount != observed.CapturedAmount)
            {
                throw new InvalidOperationException(
                    $"Finding amounts for order '{finding.OrderId}' must match the report snapshots.");
            }

            if (!finding.ExpectedContributions.SequenceEqual(expected.Contributions) ||
                !finding.ObservedContributions.SequenceEqual(observed.Contributions))
            {
                throw new InvalidOperationException(
                    $"Finding contributions for order '{finding.OrderId}' must match the report snapshots.");
            }
        }
    }
}

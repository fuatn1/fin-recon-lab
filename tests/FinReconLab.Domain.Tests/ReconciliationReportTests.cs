using System.Collections.Generic;

namespace FinReconLab.Domain.Tests;

public sealed class ReconciliationReportTests
{
    private static readonly ReconciliationCutoff Cutoff = new(2);

    [Fact]
    public void Constructor_defensively_copies_and_exposes_read_only_ordered_collections()
    {
        var orderB = CreateSnapshotPair("order-b", 20m, 15m, "payment-b", 2);
        var orderA = CreateSnapshotPair("order-a", 10m, 10m, "payment-a", 1);
        var expectedInput = new[] { orderB.Expected, orderA.Expected };
        var observedInput = new[] { orderB.Observed, orderA.Observed };
        var findingsInput = new[] { orderB.Finding! };

        var report = CreateReport(expectedInput, observedInput, findingsInput);

        expectedInput[0] = orderA.Expected;
        observedInput[0] = orderA.Observed;
        findingsInput[0] = null!;

        Assert.Equal(["order-a", "order-b"], report.ExpectedSnapshots.Select(snapshot => snapshot.OrderId));
        Assert.Equal(["order-a", "order-b"], report.ObservedSnapshots.Select(snapshot => snapshot.OrderId));
        Assert.Equal("order-b", Assert.Single(report.Findings).OrderId);
        Assert.Throws<NotSupportedException>(
            () => ((ICollection<ExpectedPaymentSnapshot>)report.ExpectedSnapshots).Add(orderA.Expected));
        Assert.Throws<NotSupportedException>(
            () => ((ICollection<ObservedPaymentSnapshot>)report.ObservedSnapshots).Clear());
        Assert.Throws<NotSupportedException>(
            () => ((ICollection<ReconciliationFinding>)report.Findings).Remove(orderB.Finding!));
    }

    [Fact]
    public void Structurally_identical_reports_have_value_equality_and_equal_hash_codes()
    {
        var firstPair = CreateSnapshotPair("order-001", 100m, 75m, "payment-001", 1);
        var secondPair = CreateSnapshotPair("order-001", 100m, 75m, "payment-001", 1);

        var first = CreateReport(
            [firstPair.Expected],
            [firstPair.Observed],
            [firstPair.Finding!]);
        var second = CreateReport(
            [secondPair.Expected],
            [secondPair.Observed],
            [secondPair.Finding!]);

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("reconciliation-report.v2")]
    public void Constructor_rejects_unsupported_schema_versions(string? schemaVersion)
    {
        var pair = CreateSnapshotPair("order-001", 100m, 100m, "payment-001", 1);

        Assert.Throws<ArgumentException>(
            () => new ReconciliationReport(
                schemaVersion!,
                CreateScenario(),
                Cutoff,
                [pair.Expected],
                [pair.Observed],
                []));
    }

    [Fact]
    public void Constructor_rejects_null_inputs_and_entries()
    {
        var pair = CreateSnapshotPair("order-001", 100m, 75m, "payment-001", 1);

        Assert.Throws<ArgumentNullException>(
            () => new ReconciliationReport(
                ReconciliationReport.SupportedSchemaVersion,
                null!,
                Cutoff,
                [pair.Expected],
                [pair.Observed],
                [pair.Finding!]));
        Assert.Throws<ArgumentNullException>(
            () => CreateReport(null!, [pair.Observed], [pair.Finding!]));
        Assert.Throws<ArgumentNullException>(
            () => CreateReport([pair.Expected], null!, [pair.Finding!]));
        Assert.Throws<ArgumentNullException>(
            () => CreateReport([pair.Expected], [pair.Observed], null!));
        Assert.Throws<ArgumentException>(
            () => CreateReport([null!], [pair.Observed], [pair.Finding!]));
        Assert.Throws<ArgumentException>(
            () => CreateReport([pair.Expected], [null!], [pair.Finding!]));
        Assert.Throws<ArgumentException>(
            () => CreateReport([pair.Expected], [pair.Observed], [null!]));
    }

    [Fact]
    public void Constructor_rejects_unpaired_snapshots()
    {
        var orderA = CreateSnapshotPair("order-a", 10m, 10m, "payment-a", 1);
        var orderB = CreateSnapshotPair("order-b", 20m, 20m, "payment-b", 2);

        Assert.Throws<InvalidOperationException>(
            () => CreateReport(
                [orderA.Expected],
                [orderA.Observed, orderB.Observed],
                []));
        Assert.Throws<InvalidOperationException>(
            () => CreateReport(
                [orderA.Expected],
                [orderB.Observed],
                []));
    }

    [Fact]
    public void Constructor_rejects_snapshot_or_finding_cutoff_mismatches()
    {
        var pair = CreateSnapshotPair("order-001", 100m, 75m, "payment-001", 1);
        var otherCutoffExpected = new ExpectedPaymentSnapshot(
            pair.Expected.OrderId,
            pair.Expected.CapturedAmount,
            new ReconciliationCutoff(3),
            pair.Expected.Contributions);

        Assert.Throws<InvalidOperationException>(
            () => CreateReport(
                [otherCutoffExpected],
                [pair.Observed],
                [pair.Finding!]));

        var otherCutoffPair = CreateSnapshotPair(
            "order-001",
            100m,
            75m,
            "payment-001",
            1,
            new ReconciliationCutoff(3));

        Assert.Throws<InvalidOperationException>(
            () => CreateReport(
                [pair.Expected],
                [pair.Observed],
                [otherCutoffPair.Finding!]));
    }

    [Fact]
    public void Constructor_rejects_findings_without_matching_report_evidence()
    {
        var reportPair = CreateSnapshotPair("order-001", 100m, 75m, "payment-001", 1);
        var unrelatedPair = CreateSnapshotPair("order-002", 50m, 25m, "payment-002", 2);
        var differentTracePair = CreateSnapshotPair("order-001", 100m, 75m, "payment-other", 1);

        Assert.Throws<InvalidOperationException>(
            () => CreateReport(
                [reportPair.Expected],
                [reportPair.Observed],
                [unrelatedPair.Finding!]));
        Assert.Throws<InvalidOperationException>(
            () => CreateReport(
                [reportPair.Expected],
                [reportPair.Observed],
                [differentTracePair.Finding!]));
    }

    private static ReconciliationReport CreateReport(
        IEnumerable<ExpectedPaymentSnapshot> expectedSnapshots,
        IEnumerable<ObservedPaymentSnapshot> observedSnapshots,
        IEnumerable<ReconciliationFinding> findings)
    {
        return new ReconciliationReport(
            ReconciliationReport.SupportedSchemaVersion,
            CreateScenario(),
            Cutoff,
            expectedSnapshots,
            observedSnapshots,
            findings);
    }

    private static ScenarioDefinition CreateScenario()
    {
        return new ScenarioDefinition(
            ScenarioDefinition.SupportedSchemaVersion,
            "scenario-report",
            42,
            2,
            new Money(100m, "USD"),
            DateTimeOffset.UnixEpoch,
            TimeSpan.FromMinutes(1));
    }

    private static (
        ExpectedPaymentSnapshot Expected,
        ObservedPaymentSnapshot Observed,
        ReconciliationFinding? Finding)
        CreateSnapshotPair(
            string orderId,
            decimal expectedAmount,
            decimal observedAmount,
            string eventId,
            long sequence,
            ReconciliationCutoff? cutoff = null)
    {
        var pairCutoff = cutoff ?? Cutoff;
        var expected = new ExpectedPaymentSnapshot(
            orderId,
            new Money(expectedAmount, "USD"),
            pairCutoff,
            [new ExpectedPaymentContribution(eventId, sequence, new Money(expectedAmount, "USD"))]);
        var observed = new ObservedPaymentSnapshot(
            orderId,
            new Money(observedAmount, "USD"),
            pairCutoff,
            [new ObservedPaymentContribution(eventId, sequence, sequence, 1, new Money(observedAmount, "USD"))]);

        return (
            expected,
            observed,
            expectedAmount == observedAmount
                ? null
                : new ReconciliationFinding(
                    ReconciliationFindingCategory.CapturedAmountMismatch,
                    expected,
                    observed,
                    observed.CapturedAmount - expected.CapturedAmount));
    }
}

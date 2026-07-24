using FinReconLab.Domain;

namespace FinReconLab.Domain.Tests;

public sealed class PaymentTraceabilityTests
{
    private static readonly ReconciliationCutoff Cutoff = new(3);

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Contributions_reject_blank_source_event_ids(string sourceEventId)
    {
        Assert.Throws<ArgumentException>(
            () => new ExpectedPaymentContribution(sourceEventId, 1, new Money(10m, "USD")));
        Assert.Throws<ArgumentException>(
            () => new ObservedPaymentContribution(sourceEventId, 1, 1, 1, new Money(10m, "USD")));
    }

    [Fact]
    public void Contributions_reject_negative_source_logical_sequences()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new ExpectedPaymentContribution("payment-001", -1, new Money(10m, "USD")));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new ObservedPaymentContribution("payment-001", -1, 1, 1, new Money(10m, "USD")));
    }

    [Fact]
    public void Observed_contribution_rejects_invalid_delivery_position_and_attempt()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new ObservedPaymentContribution("payment-001", 1, -1, 1, new Money(10m, "USD")));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new ObservedPaymentContribution("payment-001", 1, 1, 0, new Money(10m, "USD")));
    }

    [Fact]
    public void Contributions_reject_null_money()
    {
        Assert.Throws<ArgumentNullException>(
            () => new ExpectedPaymentContribution("payment-001", 1, null!));
        Assert.Throws<ArgumentNullException>(
            () => new ObservedPaymentContribution("payment-001", 1, 1, 1, null!));
    }

    [Fact]
    public void Snapshots_reject_null_contribution_collections()
    {
        Assert.Throws<ArgumentNullException>(
            () => new ExpectedPaymentSnapshot("order-001", Money.Zero("USD"), Cutoff, null!));
        Assert.Throws<ArgumentNullException>(
            () => new ObservedPaymentSnapshot("order-001", Money.Zero("USD"), Cutoff, null!));
    }

    [Fact]
    public void Snapshots_reject_null_contribution_entries()
    {
        Assert.Throws<ArgumentException>(
            () => new ExpectedPaymentSnapshot(
                "order-001",
                Money.Zero("USD"),
                Cutoff,
                [null!]));
        Assert.Throws<ArgumentException>(
            () => new ObservedPaymentSnapshot(
                "order-001",
                Money.Zero("USD"),
                Cutoff,
                [null!]));
    }

    [Fact]
    public void Snapshots_reject_contribution_currency_mismatch()
    {
        Assert.Throws<InvalidOperationException>(
            () => new ExpectedPaymentSnapshot(
                "order-001",
                new Money(10m, "USD"),
                Cutoff,
                [new ExpectedPaymentContribution("payment-001", 1, new Money(10m, "EUR"))]));
        Assert.Throws<InvalidOperationException>(
            () => new ObservedPaymentSnapshot(
                "order-001",
                new Money(10m, "USD"),
                Cutoff,
                [new ObservedPaymentContribution("payment-001", 1, 1, 1, new Money(10m, "EUR"))]));
    }

    [Fact]
    public void Snapshots_reject_aggregate_that_differs_from_contribution_total()
    {
        Assert.Throws<InvalidOperationException>(
            () => new ExpectedPaymentSnapshot(
                "order-001",
                new Money(20m, "USD"),
                Cutoff,
                [new ExpectedPaymentContribution("payment-001", 1, new Money(10m, "USD"))]));
        Assert.Throws<InvalidOperationException>(
            () => new ObservedPaymentSnapshot(
                "order-001",
                new Money(20m, "USD"),
                Cutoff,
                [new ObservedPaymentContribution("payment-001", 1, 1, 1, new Money(10m, "USD"))]));
    }

    [Fact]
    public void Snapshots_reject_contributions_beyond_their_role_specific_cutoff_sequence()
    {
        var cutoff = new ReconciliationCutoff(1);

        Assert.Throws<InvalidOperationException>(
            () => new ExpectedPaymentSnapshot(
                "order-001",
                new Money(10m, "USD"),
                cutoff,
                [new ExpectedPaymentContribution("payment-001", 2, new Money(10m, "USD"))]));
        Assert.Throws<InvalidOperationException>(
            () => new ObservedPaymentSnapshot(
                "order-001",
                new Money(10m, "USD"),
                cutoff,
                [new ObservedPaymentContribution("payment-001", 1, 2, 1, new Money(10m, "USD"))]));
    }

    [Fact]
    public void Empty_contributions_are_valid_only_for_zero_aggregate()
    {
        var expected = new ExpectedPaymentSnapshot("order-001", Money.Zero("USD"), Cutoff, []);
        var observed = new ObservedPaymentSnapshot("order-001", Money.Zero("USD"), Cutoff, []);

        Assert.Empty(expected.Contributions);
        Assert.Empty(observed.Contributions);
        Assert.Throws<InvalidOperationException>(
            () => new ExpectedPaymentSnapshot("order-001", new Money(1m, "USD"), Cutoff, []));
        Assert.Throws<InvalidOperationException>(
            () => new ObservedPaymentSnapshot("order-001", new Money(1m, "USD"), Cutoff, []));
    }

    [Fact]
    public void Snapshot_contribution_inputs_are_defensively_copied_and_outputs_are_read_only()
    {
        var expectedInput = new List<ExpectedPaymentContribution>
        {
            new("payment-001", 1, new Money(10m, "USD"))
        };
        var observedInput = new List<ObservedPaymentContribution>
        {
            new("payment-001", 1, 1, 1, new Money(10m, "USD"))
        };
        var expected = new ExpectedPaymentSnapshot("order-001", new Money(10m, "USD"), Cutoff, expectedInput);
        var observed = new ObservedPaymentSnapshot("order-001", new Money(10m, "USD"), Cutoff, observedInput);

        expectedInput.Clear();
        observedInput.Clear();

        Assert.Single(expected.Contributions);
        Assert.Single(observed.Contributions);
        var expectedCollection = Assert.IsAssignableFrom<ICollection<ExpectedPaymentContribution>>(expected.Contributions);
        var observedCollection = Assert.IsAssignableFrom<ICollection<ObservedPaymentContribution>>(observed.Contributions);
        Assert.True(expectedCollection.IsReadOnly);
        Assert.True(observedCollection.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => expectedCollection.Clear());
        Assert.Throws<NotSupportedException>(() => observedCollection.Clear());
    }

    [Fact]
    public void Snapshot_contributions_are_normalized_to_role_specific_deterministic_order()
    {
        var expected = new ExpectedPaymentSnapshot(
            "order-001",
            new Money(30m, "USD"),
            Cutoff,
            [
                new ExpectedPaymentContribution("payment-b", 2, new Money(10m, "USD")),
                new ExpectedPaymentContribution("payment-c", 1, new Money(10m, "USD")),
                new ExpectedPaymentContribution("payment-a", 1, new Money(10m, "USD"))
            ]);
        var observed = new ObservedPaymentSnapshot(
            "order-001",
            new Money(40m, "USD"),
            Cutoff,
            [
                new ObservedPaymentContribution("payment-b", 2, 2, 1, new Money(10m, "USD")),
                new ObservedPaymentContribution("payment-a", 1, 1, 2, new Money(10m, "USD")),
                new ObservedPaymentContribution("payment-b", 2, 1, 1, new Money(10m, "USD")),
                new ObservedPaymentContribution("payment-a", 1, 1, 1, new Money(10m, "USD"))
            ]);

        Assert.Equal(
            new[] { "payment-a", "payment-c", "payment-b" },
            expected.Contributions.Select(contribution => contribution.SourceEventId));
        Assert.Equal(
            new[] { "payment-a:1", "payment-a:2", "payment-b:1", "payment-b:1" },
            observed.Contributions.Select(
                contribution => $"{contribution.SourceEventId}:{contribution.DeliveryAttempt}"));
    }

    [Fact]
    public void Finding_rejects_null_inputs_and_incorrect_signed_delta()
    {
        var expected = CreateExpectedSnapshot("order-001", Cutoff, 10m);
        var observed = CreateObservedSnapshot("order-001", Cutoff, 20m);

        Assert.Throws<ArgumentNullException>(
            () => new ReconciliationFinding(
                ReconciliationFindingCategory.CapturedAmountMismatch,
                null!,
                observed,
                new Money(10m, "USD")));
        Assert.Throws<ArgumentNullException>(
            () => new ReconciliationFinding(
                ReconciliationFindingCategory.CapturedAmountMismatch,
                expected,
                null!,
                new Money(10m, "USD")));
        Assert.Throws<ArgumentNullException>(
            () => new ReconciliationFinding(
                ReconciliationFindingCategory.CapturedAmountMismatch,
                expected,
                observed,
                null!));
        Assert.Throws<InvalidOperationException>(
            () => new ReconciliationFinding(
                ReconciliationFindingCategory.CapturedAmountMismatch,
                expected,
                observed,
                new Money(5m, "USD")));
    }

    [Fact]
    public void Finding_trace_outputs_are_independent_read_only_snapshots()
    {
        var expected = CreateExpectedSnapshot("order-001", Cutoff, 10m);
        var observed = CreateObservedSnapshot("order-001", Cutoff, 20m);
        var finding = new ReconciliationFinding(
            ReconciliationFindingCategory.CapturedAmountMismatch,
            expected,
            observed,
            new Money(10m, "USD"));

        Assert.NotSame(expected.Contributions, finding.ExpectedContributions);
        Assert.NotSame(observed.Contributions, finding.ObservedContributions);
        var expectedCollection = Assert.IsAssignableFrom<ICollection<ExpectedPaymentContribution>>(
            finding.ExpectedContributions);
        var observedCollection = Assert.IsAssignableFrom<ICollection<ObservedPaymentContribution>>(
            finding.ObservedContributions);
        Assert.True(expectedCollection.IsReadOnly);
        Assert.True(observedCollection.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => expectedCollection.Clear());
        Assert.Throws<NotSupportedException>(() => observedCollection.Clear());
    }

    [Fact]
    public void Independently_created_structurally_identical_expected_snapshots_are_equal()
    {
        var first = CreateExpectedPairSnapshot(
            new ExpectedPaymentContribution("payment-a", 1, new Money(10m, "USD")),
            new ExpectedPaymentContribution("payment-b", 2, new Money(20m, "USD")));
        var second = CreateExpectedPairSnapshot(
            new ExpectedPaymentContribution("payment-a", 1, new Money(10m, "USD")),
            new ExpectedPaymentContribution("payment-b", 2, new Money(20m, "USD")));

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void Independently_created_structurally_identical_observed_snapshots_are_equal()
    {
        var first = CreateObservedPairSnapshot(
            new ObservedPaymentContribution("payment-a", 1, 1, 1, new Money(10m, "USD")),
            new ObservedPaymentContribution("payment-b", 2, 2, 1, new Money(20m, "USD")));
        var second = CreateObservedPairSnapshot(
            new ObservedPaymentContribution("payment-a", 1, 1, 1, new Money(10m, "USD")),
            new ObservedPaymentContribution("payment-b", 2, 2, 1, new Money(20m, "USD")));

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void Independently_created_structurally_identical_findings_and_lists_are_equal()
    {
        var firstFinding = CreateValueFinding();
        var secondFinding = CreateValueFinding();

        Assert.Equal(firstFinding, secondFinding);
        Assert.Equal(firstFinding.GetHashCode(), secondFinding.GetHashCode());
        Assert.Equal(new[] { firstFinding }, new[] { secondFinding });
    }

    [Fact]
    public void Expected_snapshot_equality_includes_contribution_values_and_order()
    {
        var baseline = CreateExpectedPairSnapshot(
            new ExpectedPaymentContribution("payment-a", 1, new Money(10m, "USD")),
            new ExpectedPaymentContribution("payment-b", 2, new Money(20m, "USD")));
        var amountChanged = CreateExpectedPairSnapshot(
            new ExpectedPaymentContribution("payment-a", 1, new Money(11m, "USD")),
            new ExpectedPaymentContribution("payment-b", 2, new Money(19m, "USD")));
        var identityChanged = CreateExpectedPairSnapshot(
            new ExpectedPaymentContribution("payment-c", 1, new Money(10m, "USD")),
            new ExpectedPaymentContribution("payment-b", 2, new Money(20m, "USD")));
        var logicalSequenceChanged = CreateExpectedPairSnapshot(
            new ExpectedPaymentContribution("payment-a", 2, new Money(10m, "USD")),
            new ExpectedPaymentContribution("payment-b", 2, new Money(20m, "USD")));
        var firstOrdering = CreateExpectedPairSnapshot(
            new ExpectedPaymentContribution("payment-same", 1, new Money(10m, "USD")),
            new ExpectedPaymentContribution("payment-same", 1, new Money(20m, "USD")));
        var secondOrdering = CreateExpectedPairSnapshot(
            new ExpectedPaymentContribution("payment-same", 1, new Money(20m, "USD")),
            new ExpectedPaymentContribution("payment-same", 1, new Money(10m, "USD")));

        Assert.NotEqual(baseline, amountChanged);
        Assert.NotEqual(baseline, identityChanged);
        Assert.NotEqual(baseline, logicalSequenceChanged);
        Assert.NotEqual(firstOrdering, secondOrdering);
    }

    [Fact]
    public void Observed_snapshot_equality_includes_all_delivery_contribution_values_and_order()
    {
        var baseline = CreateObservedPairSnapshot(
            new ObservedPaymentContribution("payment-a", 1, 1, 1, new Money(10m, "USD")),
            new ObservedPaymentContribution("payment-b", 2, 2, 1, new Money(20m, "USD")));
        var amountChanged = CreateObservedPairSnapshot(
            new ObservedPaymentContribution("payment-a", 1, 1, 1, new Money(11m, "USD")),
            new ObservedPaymentContribution("payment-b", 2, 2, 1, new Money(19m, "USD")));
        var identityChanged = CreateObservedPairSnapshot(
            new ObservedPaymentContribution("payment-c", 1, 1, 1, new Money(10m, "USD")),
            new ObservedPaymentContribution("payment-b", 2, 2, 1, new Money(20m, "USD")));
        var logicalSequenceChanged = CreateObservedPairSnapshot(
            new ObservedPaymentContribution("payment-a", 2, 1, 1, new Money(10m, "USD")),
            new ObservedPaymentContribution("payment-b", 2, 2, 1, new Money(20m, "USD")));
        var deliverySequenceChanged = CreateObservedPairSnapshot(
            new ObservedPaymentContribution("payment-a", 1, 2, 1, new Money(10m, "USD")),
            new ObservedPaymentContribution("payment-b", 2, 2, 1, new Money(20m, "USD")));
        var deliveryAttemptChanged = CreateObservedPairSnapshot(
            new ObservedPaymentContribution("payment-a", 1, 1, 2, new Money(10m, "USD")),
            new ObservedPaymentContribution("payment-b", 2, 2, 1, new Money(20m, "USD")));
        var firstOrdering = CreateObservedPairSnapshot(
            new ObservedPaymentContribution("payment-same", 1, 1, 1, new Money(10m, "USD")),
            new ObservedPaymentContribution("payment-same", 1, 1, 1, new Money(20m, "USD")));
        var secondOrdering = CreateObservedPairSnapshot(
            new ObservedPaymentContribution("payment-same", 1, 1, 1, new Money(20m, "USD")),
            new ObservedPaymentContribution("payment-same", 1, 1, 1, new Money(10m, "USD")));

        Assert.NotEqual(baseline, amountChanged);
        Assert.NotEqual(baseline, identityChanged);
        Assert.NotEqual(baseline, logicalSequenceChanged);
        Assert.NotEqual(baseline, deliverySequenceChanged);
        Assert.NotEqual(baseline, deliveryAttemptChanged);
        Assert.NotEqual(firstOrdering, secondOrdering);
    }

    [Fact]
    public void Finding_equality_includes_expected_and_observed_contribution_values()
    {
        var baselineExpected = CreateExpectedPairSnapshot(
            new ExpectedPaymentContribution("payment-a", 1, new Money(10m, "USD")),
            new ExpectedPaymentContribution("payment-b", 2, new Money(20m, "USD")));
        var changedExpected = CreateExpectedPairSnapshot(
            new ExpectedPaymentContribution("payment-a", 1, new Money(11m, "USD")),
            new ExpectedPaymentContribution("payment-b", 2, new Money(19m, "USD")));
        var baselineObserved = new ObservedPaymentSnapshot(
            "order-001",
            new Money(40m, "USD"),
            Cutoff,
            [new ObservedPaymentContribution("payment-a", 1, 1, 1, new Money(40m, "USD"))]);
        var baseline = new ReconciliationFinding(
            ReconciliationFindingCategory.CapturedAmountMismatch,
            baselineExpected,
            baselineObserved,
            new Money(10m, "USD"));
        var changed = new ReconciliationFinding(
            ReconciliationFindingCategory.CapturedAmountMismatch,
            changedExpected,
            baselineObserved,
            new Money(10m, "USD"));

        Assert.NotEqual(baseline, changed);
    }

    private static ExpectedPaymentSnapshot CreateExpectedSnapshot(
        string orderId,
        ReconciliationCutoff cutoff,
        decimal amount)
    {
        var money = new Money(amount, "USD");
        return new ExpectedPaymentSnapshot(
            orderId,
            money,
            cutoff,
            [new ExpectedPaymentContribution("payment-001", 1, money)]);
    }

    private static ObservedPaymentSnapshot CreateObservedSnapshot(
        string orderId,
        ReconciliationCutoff cutoff,
        decimal amount)
    {
        var money = new Money(amount, "USD");
        return new ObservedPaymentSnapshot(
            orderId,
            money,
            cutoff,
            [new ObservedPaymentContribution("payment-001", 1, 1, 1, money)]);
    }

    private static ExpectedPaymentSnapshot CreateExpectedPairSnapshot(
        ExpectedPaymentContribution first,
        ExpectedPaymentContribution second)
    {
        return new ExpectedPaymentSnapshot(
            "order-001",
            new Money(30m, "USD"),
            Cutoff,
            [first, second]);
    }

    private static ObservedPaymentSnapshot CreateObservedPairSnapshot(
        ObservedPaymentContribution first,
        ObservedPaymentContribution second)
    {
        return new ObservedPaymentSnapshot(
            "order-001",
            new Money(30m, "USD"),
            Cutoff,
            [first, second]);
    }

    private static ReconciliationFinding CreateValueFinding()
    {
        var expected = CreateExpectedPairSnapshot(
            new ExpectedPaymentContribution("payment-a", 1, new Money(10m, "USD")),
            new ExpectedPaymentContribution("payment-b", 2, new Money(20m, "USD")));
        var observed = new ObservedPaymentSnapshot(
            "order-001",
            new Money(40m, "USD"),
            Cutoff,
            [new ObservedPaymentContribution("payment-a", 1, 1, 1, new Money(40m, "USD"))]);

        return new ReconciliationFinding(
            ReconciliationFindingCategory.CapturedAmountMismatch,
            expected,
            observed,
            new Money(10m, "USD"));
    }
}

using FinReconLab.Domain;

namespace FinReconLab.Application.Tests;

public sealed class MissingPaymentDeliveryFaultInjectorTests
{
    private static readonly DateTimeOffset SuppliedTimestamp = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Selected_payment_is_absent_from_delivered_event_stream()
    {
        var truthStream = CreateThreePayments();

        var result = InjectMissing(truthStream, sourceEventId: "payment-captured-002");

        Assert.DoesNotContain(result.DeliveredEventStream, delivery => delivery.SourceEventId == "payment-captured-002");
    }

    [Fact]
    public void Non_selected_events_remain_present_with_original_delivery_sequence_and_attempt()
    {
        var truthStream = CreateThreePayments();

        var result = InjectMissing(truthStream, sourceEventId: "payment-captured-002");

        Assert.Collection(
            result.DeliveredEventStream,
            first =>
            {
                Assert.Equal("payment-captured-001", first.SourceEventId);
                Assert.Equal(1, first.DeliverySequence);
                Assert.Equal(1, first.DeliveryAttempt);
            },
            second =>
            {
                Assert.Equal("payment-captured-003", second.SourceEventId);
                Assert.Equal(3, second.DeliverySequence);
                Assert.Equal(1, second.DeliveryAttempt);
            });
    }

    [Fact]
    public void Delivery_sequence_gaps_are_preserved()
    {
        var truthStream = CreateThreePayments();

        var result = InjectMissing(truthStream, sourceEventId: "payment-captured-002");

        Assert.Equal(new long[] { 1, 3 }, result.DeliveredEventStream.Select(delivery => delivery.DeliverySequence));
    }

    [Fact]
    public void Manifest_contains_one_missing_delivery_entry()
    {
        var truthStream = CreateThreePayments();

        var result = InjectMissing(truthStream, sourceEventId: "payment-captured-002");

        var entry = Assert.IsType<MissingDeliveryFaultManifestEntry>(Assert.Single(result.FaultManifest.Entries));
        Assert.Equal("fault-missing-payment-001", entry.FaultId);
        Assert.Equal(FaultKind.MissingDelivery, entry.Kind);
        Assert.Equal("payment-captured-002", entry.SourceEventId);
    }

    [Fact]
    public void One_payment_missing_delivery_produces_captured_amount_mismatch()
    {
        var truthStream = new[] { CreatePayment("payment-captured-001", "order-001", 1) };
        var result = InjectMissing(truthStream, sourceEventId: "payment-captured-001");
        var cutoff = new ReconciliationCutoff(1);
        var expected = new ExpectedPaymentProjection().Build("order-001", "USD", cutoff, truthStream);
        var observed = new NonIdempotentObservedPaymentProjectionForExperiment()
            .Build("order-001", "USD", cutoff, result.DeliveredEventStream);

        var finding = Assert.Single(new PaymentReconciliationEngine().Reconcile(expected, observed));

        Assert.Equal(ReconciliationFindingCategory.CapturedAmountMismatch, finding.Category);
        Assert.Equal(new Money(100m, "USD"), finding.ExpectedAmount);
        Assert.Equal(new Money(0m, "USD"), finding.ObservedAmount);
        Assert.Equal(new Money(-100m, "USD"), finding.SignedDelta);
    }

    [Fact]
    public void Missing_source_event_beyond_cutoff_produces_no_finding_at_earlier_cutoff()
    {
        var truthStream = new[] { CreatePayment("payment-captured-002", "order-002", 2) };
        var result = InjectMissing(truthStream, sourceEventId: "payment-captured-002");
        var cutoff = new ReconciliationCutoff(1);
        var expected = new ExpectedPaymentProjection().Build("order-002", "USD", cutoff, truthStream);
        var observed = new NonIdempotentObservedPaymentProjectionForExperiment()
            .Build("order-002", "USD", cutoff, result.DeliveredEventStream);

        var findings = new PaymentReconciliationEngine().Reconcile(expected, observed);

        Assert.Equal(new Money(0m, "USD"), expected.CapturedAmount);
        Assert.Equal(new Money(0m, "USD"), observed.CapturedAmount);
        Assert.Empty(findings);
    }

    [Fact]
    public void Repeating_identical_inputs_produces_identical_delivered_streams_manifests_and_findings()
    {
        var firstTruthStream = CreateThreePayments();
        var secondTruthStream = CreateThreePayments();
        var firstResult = InjectMissing(firstTruthStream, sourceEventId: "payment-captured-002");
        var secondResult = InjectMissing(secondTruthStream, sourceEventId: "payment-captured-002");
        var cutoff = new ReconciliationCutoff(3);

        var firstFindings = Reconcile(firstTruthStream, firstResult.DeliveredEventStream, "order-002", cutoff);
        var secondFindings = Reconcile(secondTruthStream, secondResult.DeliveredEventStream, "order-002", cutoff);

        AssertDeliveredStreamsEqual(firstResult.DeliveredEventStream, secondResult.DeliveredEventStream);
        AssertFaultManifestsEqual(firstResult.FaultManifest, secondResult.FaultManifest);
        Assert.Equal(firstFindings, secondFindings);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Fault_request_rejects_blank_fault_id(string faultId)
    {
        Assert.Throws<ArgumentException>(() => new MissingPaymentFaultRequest(faultId, "payment-captured-001"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Fault_request_rejects_blank_source_event_id(string sourceEventId)
    {
        Assert.Throws<ArgumentException>(() => new MissingPaymentFaultRequest("fault-missing-payment-001", sourceEventId));
    }

    [Fact]
    public void Injector_rejects_unknown_source_event_id()
    {
        var request = new MissingPaymentFaultRequest("fault-missing-payment-001", "missing-event");

        Assert.Throws<InvalidOperationException>(
            () => new MissingPaymentDeliveryFaultInjector().Inject(CreateThreePayments(), request));
    }

    [Fact]
    public void Injector_rejects_duplicate_source_event_identities()
    {
        var truthStream = new[]
        {
            CreatePayment("payment-captured-001", "order-001", 1),
            CreatePayment("payment-captured-001", "order-002", 2)
        };

        Assert.Throws<InvalidOperationException>(
            () => InjectMissing(truthStream, sourceEventId: "payment-captured-001"));
    }

    [Fact]
    public void Returned_collections_are_read_only()
    {
        var result = InjectMissing(CreateThreePayments(), sourceEventId: "payment-captured-002");
        var deliveredCollection = Assert.IsAssignableFrom<ICollection<DeliveredPaymentCaptured>>(result.DeliveredEventStream);
        var manifestCollection = Assert.IsAssignableFrom<ICollection<FaultManifestEntry>>(result.FaultManifest.Entries);

        Assert.True(deliveredCollection.IsReadOnly);
        Assert.True(manifestCollection.IsReadOnly);
        Assert.Throws<NotSupportedException>(
            () => deliveredCollection.Add(
                new DeliveredPaymentCaptured(
                    CreatePayment("payment-captured-004", "order-004", 4),
                    deliverySequence: 4,
                    deliveryAttempt: 1)));
        Assert.Throws<NotSupportedException>(
            () => manifestCollection.Add(
                new MissingDeliveryFaultManifestEntry(
                    "fault-missing-payment-002",
                    "payment-captured-004")));
    }

    private static IReadOnlyList<ReconciliationFinding> Reconcile(
        IReadOnlyList<PaymentCaptured> truthStream,
        IReadOnlyList<DeliveredPaymentCaptured> deliveredStream,
        string orderId,
        ReconciliationCutoff cutoff)
    {
        var expected = new ExpectedPaymentProjection().Build(orderId, "USD", cutoff, truthStream);
        var observed = new NonIdempotentObservedPaymentProjectionForExperiment()
            .Build(orderId, "USD", cutoff, deliveredStream);

        return new PaymentReconciliationEngine().Reconcile(expected, observed);
    }

    private static MissingPaymentFaultInjectionResult InjectMissing(
        IReadOnlyList<PaymentCaptured> truthStream,
        string sourceEventId)
    {
        return new MissingPaymentDeliveryFaultInjector().Inject(
            truthStream,
            new MissingPaymentFaultRequest("fault-missing-payment-001", sourceEventId));
    }

    private static PaymentCaptured[] CreateThreePayments()
    {
        return
        [
            CreatePayment("payment-captured-001", "order-001", 1),
            CreatePayment("payment-captured-002", "order-002", 2),
            CreatePayment("payment-captured-003", "order-003", 3)
        ];
    }

    private static PaymentCaptured CreatePayment(string eventId, string orderId, long logicalSequence)
    {
        return new PaymentCaptured(
            eventId,
            orderId,
            new Money(100m, "USD"),
            logicalSequence,
            SuppliedTimestamp.AddMinutes(logicalSequence - 1));
    }

    private static void AssertDeliveredStreamsEqual(
        IReadOnlyList<DeliveredPaymentCaptured> expected,
        IReadOnlyList<DeliveredPaymentCaptured> actual)
    {
        Assert.Equal(expected.Count, actual.Count);

        for (var index = 0; index < expected.Count; index++)
        {
            Assert.Equal(expected[index], actual[index]);
        }
    }

    private static void AssertFaultManifestsEqual(FaultManifest expected, FaultManifest actual)
    {
        Assert.Equal(expected.Entries.Count, actual.Entries.Count);

        for (var index = 0; index < expected.Entries.Count; index++)
        {
            Assert.Equal(expected.Entries[index], actual.Entries[index]);
        }
    }
}

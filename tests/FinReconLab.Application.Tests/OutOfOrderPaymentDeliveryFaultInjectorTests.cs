using System.Reflection;
using FinReconLab.Domain;

namespace FinReconLab.Application.Tests;

public sealed class OutOfOrderPaymentDeliveryFaultInjectorTests
{
    private static readonly DateTimeOffset SuppliedTimestamp = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Every_source_event_remains_in_delivered_stream_exactly_once()
    {
        var truthStream = CreateThreePayments();

        var result = InjectOutOfOrder(truthStream);

        Assert.Equal(truthStream.Length, result.DeliveredEventStream.Count);
        foreach (var payment in truthStream)
        {
            Assert.Single(result.DeliveredEventStream, delivery => delivery.SourceEventId == payment.EventId);
        }
    }

    [Fact]
    public void Earlier_event_receives_later_event_baseline_delivery_sequence()
    {
        var result = InjectOutOfOrder(CreateThreePayments());

        var earlierDelivery = Assert.Single(
            result.DeliveredEventStream,
            delivery => delivery.SourceEventId == "payment-captured-001");
        Assert.Equal(2, earlierDelivery.DeliverySequence);
    }

    [Fact]
    public void Later_event_receives_earlier_event_baseline_delivery_sequence()
    {
        var result = InjectOutOfOrder(CreateThreePayments());

        var laterDelivery = Assert.Single(
            result.DeliveredEventStream,
            delivery => delivery.SourceEventId == "payment-captured-002");
        Assert.Equal(1, laterDelivery.DeliverySequence);
    }

    [Fact]
    public void Unaffected_events_retain_original_delivery_sequences()
    {
        var result = InjectOutOfOrder(CreateThreePayments());

        var unaffectedDelivery = Assert.Single(
            result.DeliveredEventStream,
            delivery => delivery.SourceEventId == "payment-captured-003");
        Assert.Equal(3, unaffectedDelivery.DeliverySequence);
    }

    [Fact]
    public void Every_event_retains_delivery_attempt_one()
    {
        var result = InjectOutOfOrder(CreateThreePayments());

        Assert.All(result.DeliveredEventStream, delivery => Assert.Equal(1, delivery.DeliveryAttempt));
    }

    [Fact]
    public void Returned_stream_is_deterministically_ordered()
    {
        var input = new[]
        {
            CreatePayment("payment-captured-003", "order-C", 3),
            CreatePayment("payment-captured-001", "order-A", 1),
            CreatePayment("payment-captured-002", "order-B", 2)
        };

        var result = InjectOutOfOrder(input);

        Assert.Equal(
            new[] { "payment-captured-002", "payment-captured-001", "payment-captured-003" },
            result.DeliveredEventStream.Select(delivery => delivery.SourceEventId));
        Assert.Equal(new long[] { 1, 2, 3 }, result.DeliveredEventStream.Select(delivery => delivery.DeliverySequence));
    }

    [Fact]
    public void No_event_is_added_removed_duplicated_or_silently_renumbered()
    {
        var result = InjectOutOfOrder(CreateThreePayments());

        Assert.Equal(
            new[] { "payment-captured-001", "payment-captured-002", "payment-captured-003" },
            result.DeliveredEventStream
                .Select(delivery => delivery.SourceEventId)
                .OrderBy(sourceEventId => sourceEventId, StringComparer.Ordinal));
        Assert.Equal(
            new long[] { 1, 2, 3 },
            result.DeliveredEventStream
                .Select(delivery => delivery.DeliverySequence)
                .OrderBy(deliverySequence => deliverySequence));
    }

    [Fact]
    public void Manifest_contains_one_out_of_order_delivery_entry()
    {
        var result = InjectOutOfOrder(CreateThreePayments());

        var entry = Assert.IsType<OutOfOrderDeliveryFaultManifestEntry>(Assert.Single(result.FaultManifest.Entries));
        Assert.Equal("fault-out-of-order-payment-001", entry.FaultId);
        Assert.Equal(FaultKind.OutOfOrderDelivery, entry.Kind);
    }

    [Fact]
    public void Manifest_records_source_ids_and_original_and_delivered_sequences()
    {
        var result = InjectOutOfOrder(CreateThreePayments());

        var entry = Assert.IsType<OutOfOrderDeliveryFaultManifestEntry>(Assert.Single(result.FaultManifest.Entries));
        Assert.Equal("payment-captured-001", entry.EarlierSourceEventId);
        Assert.Equal("payment-captured-002", entry.LaterSourceEventId);
        Assert.Equal(1, entry.EarlierOriginalDeliverySequence);
        Assert.Equal(2, entry.EarlierDeliveredSequence);
        Assert.Equal(2, entry.LaterOriginalDeliverySequence);
        Assert.Equal(1, entry.LaterDeliveredSequence);
    }

    [Fact]
    public void Intermediate_cutoff_produces_negative_mismatch_for_earlier_order()
    {
        var truthStream = CreateTwoOrderPayments();
        var result = InjectOutOfOrder(truthStream);
        var cutoff = new ReconciliationCutoff(1);

        var finding = Assert.Single(Reconcile(truthStream, result.DeliveredEventStream, "order-A", cutoff));

        Assert.Equal(ReconciliationFindingCategory.CapturedAmountMismatch, finding.Category);
        Assert.Equal(new Money(100m, "USD"), finding.ExpectedAmount);
        Assert.Equal(new Money(0m, "USD"), finding.ObservedAmount);
        Assert.Equal(new Money(-100m, "USD"), finding.SignedDelta);
    }

    [Fact]
    public void Intermediate_cutoff_produces_positive_mismatch_for_later_order()
    {
        var truthStream = CreateTwoOrderPayments();
        var result = InjectOutOfOrder(truthStream);
        var cutoff = new ReconciliationCutoff(1);

        var finding = Assert.Single(Reconcile(truthStream, result.DeliveredEventStream, "order-B", cutoff));

        Assert.Equal(ReconciliationFindingCategory.CapturedAmountMismatch, finding.Category);
        Assert.Equal(new Money(0m, "USD"), finding.ExpectedAmount);
        Assert.Equal(new Money(100m, "USD"), finding.ObservedAmount);
        Assert.Equal(new Money(100m, "USD"), finding.SignedDelta);
    }

    [Fact]
    public void Later_cutoff_reconciles_both_orders()
    {
        var truthStream = CreateTwoOrderPayments();
        var result = InjectOutOfOrder(truthStream);
        var cutoff = new ReconciliationCutoff(2);

        var earlierFindings = Reconcile(truthStream, result.DeliveredEventStream, "order-A", cutoff);
        var laterFindings = Reconcile(truthStream, result.DeliveredEventStream, "order-B", cutoff);

        Assert.Empty(earlierFindings);
        Assert.Empty(laterFindings);
    }

    [Fact]
    public void Repeating_identical_inputs_produces_identical_delivered_streams_manifests_and_findings()
    {
        var firstTruthStream = CreateTwoOrderPayments();
        var secondTruthStream = CreateTwoOrderPayments();
        var firstResult = InjectOutOfOrder(firstTruthStream);
        var secondResult = InjectOutOfOrder(secondTruthStream);
        var cutoff = new ReconciliationCutoff(1);

        var firstFindings = ReconcileBothOrders(firstTruthStream, firstResult.DeliveredEventStream, cutoff);
        var secondFindings = ReconcileBothOrders(secondTruthStream, secondResult.DeliveredEventStream, cutoff);

        AssertDeliveredStreamsEqual(firstResult.DeliveredEventStream, secondResult.DeliveredEventStream);
        AssertFaultManifestsEqual(firstResult.FaultManifest, secondResult.FaultManifest);
        Assert.Equal(firstFindings, secondFindings);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Fault_request_rejects_blank_fault_id(string faultId)
    {
        Assert.Throws<ArgumentException>(
            () => new OutOfOrderPaymentFaultRequest(
                faultId,
                "payment-captured-001",
                "payment-captured-002"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Fault_request_rejects_blank_earlier_source_event_id(string earlierSourceEventId)
    {
        Assert.Throws<ArgumentException>(
            () => new OutOfOrderPaymentFaultRequest(
                "fault-out-of-order-payment-001",
                earlierSourceEventId,
                "payment-captured-002"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Fault_request_rejects_blank_later_source_event_id(string laterSourceEventId)
    {
        Assert.Throws<ArgumentException>(
            () => new OutOfOrderPaymentFaultRequest(
                "fault-out-of-order-payment-001",
                "payment-captured-001",
                laterSourceEventId));
    }

    [Fact]
    public void Fault_request_rejects_selecting_the_same_source_event_twice()
    {
        Assert.Throws<ArgumentException>(
            () => new OutOfOrderPaymentFaultRequest(
                "fault-out-of-order-payment-001",
                "payment-captured-001",
                "payment-captured-001"));
    }

    [Fact]
    public void Injector_rejects_unknown_earlier_source_event_id()
    {
        var request = new OutOfOrderPaymentFaultRequest(
            "fault-out-of-order-payment-001",
            "missing-event",
            "payment-captured-002");

        Assert.Throws<InvalidOperationException>(
            () => new OutOfOrderPaymentDeliveryFaultInjector().Inject(CreateThreePayments(), request));
    }

    [Fact]
    public void Injector_rejects_unknown_later_source_event_id()
    {
        var request = new OutOfOrderPaymentFaultRequest(
            "fault-out-of-order-payment-001",
            "payment-captured-001",
            "missing-event");

        Assert.Throws<InvalidOperationException>(
            () => new OutOfOrderPaymentDeliveryFaultInjector().Inject(CreateThreePayments(), request));
    }

    [Fact]
    public void Injector_rejects_duplicate_source_event_identities()
    {
        var truthStream = new[]
        {
            CreatePayment("payment-captured-001", "order-A", 1),
            CreatePayment("payment-captured-001", "order-B", 2)
        };

        Assert.Throws<InvalidOperationException>(
            () => InjectOutOfOrder(truthStream));
    }

    [Fact]
    public void Injector_rejects_duplicate_logical_sequences()
    {
        var truthStream = new[]
        {
            CreatePayment("payment-captured-001", "order-A", 1),
            CreatePayment("payment-captured-002", "order-B", 1)
        };

        Assert.Throws<InvalidOperationException>(
            () => InjectOutOfOrder(truthStream));
    }

    [Fact]
    public void Injector_rejects_request_when_earlier_source_has_later_sequence()
    {
        Assert.Throws<InvalidOperationException>(
            () => InjectOutOfOrder(
                CreateTwoOrderPayments(),
                earlierSourceEventId: "payment-captured-002",
                laterSourceEventId: "payment-captured-001"));
    }

    [Fact]
    public void Manifest_entry_rejects_non_swap_delivery_sequences()
    {
        Assert.Throws<ArgumentException>(
            () => CreateManifestEntry(earlierDeliveredSequence: 3));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Manifest_entry_rejects_blank_earlier_source_event_id(string earlierSourceEventId)
    {
        Assert.Throws<ArgumentException>(
            () => CreateManifestEntry(earlierSourceEventId: earlierSourceEventId));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Manifest_entry_rejects_blank_later_source_event_id(string laterSourceEventId)
    {
        Assert.Throws<ArgumentException>(
            () => CreateManifestEntry(laterSourceEventId: laterSourceEventId));
    }

    [Fact]
    public void Manifest_entry_rejects_identical_source_event_ids()
    {
        Assert.Throws<ArgumentException>(
            () => CreateManifestEntry(
                earlierSourceEventId: "payment-captured-001",
                laterSourceEventId: "payment-captured-001"));
    }

    [Theory]
    [InlineData(-1, 2, 2, 1)]
    [InlineData(1, -1, 2, 1)]
    [InlineData(1, 2, -1, 1)]
    [InlineData(1, 2, 2, -1)]
    public void Manifest_entry_rejects_negative_sequences(
        long earlierOriginalDeliverySequence,
        long earlierDeliveredSequence,
        long laterOriginalDeliverySequence,
        long laterDeliveredSequence)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CreateManifestEntry(
                earlierOriginalDeliverySequence: earlierOriginalDeliverySequence,
                earlierDeliveredSequence: earlierDeliveredSequence,
                laterOriginalDeliverySequence: laterOriginalDeliverySequence,
                laterDeliveredSequence: laterDeliveredSequence));
    }

    [Fact]
    public void Manifest_entry_rejects_equal_original_delivery_sequences()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CreateManifestEntry(
                earlierOriginalDeliverySequence: 2,
                earlierDeliveredSequence: 2,
                laterOriginalDeliverySequence: 2,
                laterDeliveredSequence: 2));
    }

    [Fact]
    public void Manifest_entry_rejects_earlier_original_sequence_greater_than_later_original_sequence()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CreateManifestEntry(
                earlierOriginalDeliverySequence: 3,
                earlierDeliveredSequence: 2,
                laterOriginalDeliverySequence: 2,
                laterDeliveredSequence: 3));
    }

    [Fact]
    public void Returned_collections_are_read_only()
    {
        var result = InjectOutOfOrder(CreateThreePayments());
        var deliveredCollection = Assert.IsAssignableFrom<ICollection<DeliveredPaymentCaptured>>(result.DeliveredEventStream);
        var manifestCollection = Assert.IsAssignableFrom<ICollection<FaultManifestEntry>>(result.FaultManifest.Entries);

        Assert.True(deliveredCollection.IsReadOnly);
        Assert.True(manifestCollection.IsReadOnly);
        Assert.Throws<NotSupportedException>(
            () => deliveredCollection.Add(
                new DeliveredPaymentCaptured(
                    CreatePayment("payment-captured-004", "order-D", 4),
                    deliverySequence: 4,
                    deliveryAttempt: 1)));
        Assert.Throws<NotSupportedException>(
            () => manifestCollection.Add(
                new OutOfOrderDeliveryFaultManifestEntry(
                    "fault-out-of-order-payment-002",
                    "payment-captured-004",
                    "payment-captured-005",
                    earlierOriginalDeliverySequence: 4,
                    earlierDeliveredSequence: 5,
                    laterOriginalDeliverySequence: 5,
                    laterDeliveredSequence: 4)));
    }

    [Fact]
    public void Reconciliation_engine_public_methods_do_not_accept_fault_manifest_or_fault_results()
    {
        var methodParameters = typeof(PaymentReconciliationEngine)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .SelectMany(method => method.GetParameters())
            .Select(parameter => parameter.ParameterType)
            .ToArray();

        Assert.DoesNotContain(typeof(FaultManifest), methodParameters);
        Assert.DoesNotContain(typeof(DuplicatePaymentFaultInjectionResult), methodParameters);
        Assert.DoesNotContain(typeof(MissingPaymentFaultInjectionResult), methodParameters);
        Assert.DoesNotContain(typeof(DelayedPaymentFaultInjectionResult), methodParameters);
        Assert.DoesNotContain(typeof(OutOfOrderPaymentFaultInjectionResult), methodParameters);
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

    private static IReadOnlyList<ReconciliationFinding> ReconcileBothOrders(
        IReadOnlyList<PaymentCaptured> truthStream,
        IReadOnlyList<DeliveredPaymentCaptured> deliveredStream,
        ReconciliationCutoff cutoff)
    {
        return
        [
            .. Reconcile(truthStream, deliveredStream, "order-A", cutoff),
            .. Reconcile(truthStream, deliveredStream, "order-B", cutoff)
        ];
    }

    private static OutOfOrderPaymentFaultInjectionResult InjectOutOfOrder(
        IReadOnlyList<PaymentCaptured> truthStream,
        string earlierSourceEventId = "payment-captured-001",
        string laterSourceEventId = "payment-captured-002")
    {
        return new OutOfOrderPaymentDeliveryFaultInjector().Inject(
            truthStream,
            new OutOfOrderPaymentFaultRequest(
                "fault-out-of-order-payment-001",
                earlierSourceEventId,
                laterSourceEventId));
    }

    private static PaymentCaptured[] CreateThreePayments()
    {
        return
        [
            CreatePayment("payment-captured-001", "order-A", 1),
            CreatePayment("payment-captured-002", "order-B", 2),
            CreatePayment("payment-captured-003", "order-C", 3)
        ];
    }

    private static PaymentCaptured[] CreateTwoOrderPayments()
    {
        return
        [
            CreatePayment("payment-captured-001", "order-A", 1),
            CreatePayment("payment-captured-002", "order-B", 2)
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

    private static OutOfOrderDeliveryFaultManifestEntry CreateManifestEntry(
        string earlierSourceEventId = "payment-captured-001",
        string laterSourceEventId = "payment-captured-002",
        long earlierOriginalDeliverySequence = 1,
        long earlierDeliveredSequence = 2,
        long laterOriginalDeliverySequence = 2,
        long laterDeliveredSequence = 1)
    {
        return new OutOfOrderDeliveryFaultManifestEntry(
            "fault-out-of-order-payment-001",
            earlierSourceEventId,
            laterSourceEventId,
            earlierOriginalDeliverySequence,
            earlierDeliveredSequence,
            laterOriginalDeliverySequence,
            laterDeliveredSequence);
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

using System.Reflection;
using FinReconLab.Domain;

namespace FinReconLab.Application.Tests;

public sealed class DelayedPaymentDeliveryFaultInjectorTests
{
    private static readonly DateTimeOffset SuppliedTimestamp = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Selected_event_remains_in_delivered_stream_exactly_once()
    {
        var result = InjectDelayed(CreateThreePayments(), sourceEventId: "payment-captured-002");

        Assert.Single(result.DeliveredEventStream, delivery => delivery.SourceEventId == "payment-captured-002");
    }

    [Fact]
    public void Selected_event_receives_requested_delayed_delivery_sequence()
    {
        var result = InjectDelayed(CreateThreePayments(), sourceEventId: "payment-captured-002", delayedDeliverySequence: 4);

        var selectedDelivery = Assert.Single(
            result.DeliveredEventStream,
            delivery => delivery.SourceEventId == "payment-captured-002");
        Assert.Equal(4, selectedDelivery.DeliverySequence);
    }

    [Fact]
    public void Selected_event_keeps_delivery_attempt_one()
    {
        var result = InjectDelayed(CreateThreePayments(), sourceEventId: "payment-captured-002");

        var selectedDelivery = Assert.Single(
            result.DeliveredEventStream,
            delivery => delivery.SourceEventId == "payment-captured-002");
        Assert.Equal(1, selectedDelivery.DeliveryAttempt);
    }

    [Fact]
    public void Non_selected_events_retain_original_delivery_sequences_and_attempts()
    {
        var result = InjectDelayed(CreateThreePayments(), sourceEventId: "payment-captured-002");

        Assert.Collection(
            result.DeliveredEventStream.Where(delivery => delivery.SourceEventId != "payment-captured-002"),
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
    public void Events_are_returned_in_deterministic_delivery_order()
    {
        var input = new[]
        {
            CreatePayment("payment-captured-003", "order-003", 3),
            CreatePayment("payment-captured-001", "order-001", 1),
            CreatePayment("payment-captured-002", "order-002", 2)
        };

        var result = InjectDelayed(input, sourceEventId: "payment-captured-002", delayedDeliverySequence: 4);

        Assert.Equal(
            new[] { "payment-captured-001", "payment-captured-003", "payment-captured-002" },
            result.DeliveredEventStream.Select(delivery => delivery.SourceEventId));
        Assert.Equal(new long[] { 1, 3, 4 }, result.DeliveredEventStream.Select(delivery => delivery.DeliverySequence));
    }

    [Fact]
    public void No_event_is_silently_renumbered()
    {
        var result = InjectDelayed(CreateThreePayments(), sourceEventId: "payment-captured-002", delayedDeliverySequence: 4);

        Assert.Collection(
            result.DeliveredEventStream.OrderBy(delivery => delivery.SourceEventId, StringComparer.Ordinal),
            first =>
            {
                Assert.Equal("payment-captured-001", first.SourceEventId);
                Assert.Equal(1, first.DeliverySequence);
            },
            second =>
            {
                Assert.Equal("payment-captured-002", second.SourceEventId);
                Assert.Equal(4, second.DeliverySequence);
            },
            third =>
            {
                Assert.Equal("payment-captured-003", third.SourceEventId);
                Assert.Equal(3, third.DeliverySequence);
            });
    }

    [Fact]
    public void Manifest_contains_one_delayed_delivery_entry()
    {
        var result = InjectDelayed(CreateThreePayments(), sourceEventId: "payment-captured-002", delayedDeliverySequence: 4);

        var entry = Assert.IsType<DelayedDeliveryFaultManifestEntry>(Assert.Single(result.FaultManifest.Entries));
        Assert.Equal("fault-delayed-payment-001", entry.FaultId);
        Assert.Equal(FaultKind.DelayedDelivery, entry.Kind);
        Assert.Equal("payment-captured-002", entry.SourceEventId);
    }

    [Fact]
    public void Manifest_records_original_and_delayed_sequences()
    {
        var result = InjectDelayed(CreateThreePayments(), sourceEventId: "payment-captured-002", delayedDeliverySequence: 4);

        var entry = Assert.IsType<DelayedDeliveryFaultManifestEntry>(Assert.Single(result.FaultManifest.Entries));
        Assert.Equal(2, entry.OriginalDeliverySequence);
        Assert.Equal(4, entry.DelayedDeliverySequence);
    }

    [Fact]
    public void Early_cutoff_produces_captured_amount_mismatch_for_delayed_delivery()
    {
        var truthStream = new[] { CreatePayment("payment-captured-002", "order-002", 2) };
        var result = InjectDelayed(truthStream, sourceEventId: "payment-captured-002", delayedDeliverySequence: 4);
        var cutoff = new ReconciliationCutoff(3);
        var expected = new ExpectedPaymentProjection().Build("order-002", "USD", cutoff, truthStream);
        var observed = new NonIdempotentObservedPaymentProjectionForExperiment()
            .Build("order-002", "USD", cutoff, result.DeliveredEventStream);

        var finding = Assert.Single(new PaymentReconciliationEngine().Reconcile(expected, observed));

        Assert.Equal(ReconciliationFindingCategory.CapturedAmountMismatch, finding.Category);
        Assert.Equal(new Money(100m, "USD"), finding.ExpectedAmount);
        Assert.Equal(new Money(0m, "USD"), finding.ObservedAmount);
        Assert.Equal(new Money(-100m, "USD"), finding.SignedDelta);
    }

    [Fact]
    public void Later_cutoff_includes_delayed_delivery_and_produces_no_finding()
    {
        var truthStream = new[] { CreatePayment("payment-captured-002", "order-002", 2) };
        var result = InjectDelayed(truthStream, sourceEventId: "payment-captured-002", delayedDeliverySequence: 4);
        var cutoff = new ReconciliationCutoff(4);
        var expected = new ExpectedPaymentProjection().Build("order-002", "USD", cutoff, truthStream);
        var observed = new NonIdempotentObservedPaymentProjectionForExperiment()
            .Build("order-002", "USD", cutoff, result.DeliveredEventStream);

        var findings = new PaymentReconciliationEngine().Reconcile(expected, observed);

        Assert.Equal(new Money(100m, "USD"), expected.CapturedAmount);
        Assert.Equal(new Money(100m, "USD"), observed.CapturedAmount);
        Assert.Empty(findings);
    }

    [Fact]
    public void Repeating_identical_inputs_produces_identical_delivered_streams_manifests_and_findings()
    {
        var firstTruthStream = CreateThreePayments();
        var secondTruthStream = CreateThreePayments();
        var firstResult = InjectDelayed(firstTruthStream, sourceEventId: "payment-captured-002", delayedDeliverySequence: 4);
        var secondResult = InjectDelayed(secondTruthStream, sourceEventId: "payment-captured-002", delayedDeliverySequence: 4);
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
        Assert.Throws<ArgumentException>(
            () => new DelayedPaymentFaultRequest(faultId, "payment-captured-002", 4));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Fault_request_rejects_blank_source_event_id(string sourceEventId)
    {
        Assert.Throws<ArgumentException>(
            () => new DelayedPaymentFaultRequest("fault-delayed-payment-001", sourceEventId, 4));
    }

    [Fact]
    public void Fault_request_rejects_negative_delayed_delivery_sequence()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new DelayedPaymentFaultRequest("fault-delayed-payment-001", "payment-captured-002", -1));
    }

    [Fact]
    public void Injector_rejects_unknown_source_event_id()
    {
        var request = new DelayedPaymentFaultRequest("fault-delayed-payment-001", "missing-event", 4);

        Assert.Throws<InvalidOperationException>(
            () => new DelayedPaymentDeliveryFaultInjector().Inject(CreateThreePayments(), request));
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
            () => InjectDelayed(truthStream, sourceEventId: "payment-captured-001", delayedDeliverySequence: 3));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void Injector_rejects_delayed_sequence_equal_to_or_below_original_sequence(long delayedDeliverySequence)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => InjectDelayed(
                CreateThreePayments(),
                sourceEventId: "payment-captured-002",
                delayedDeliverySequence: delayedDeliverySequence));
    }

    [Fact]
    public void Injector_rejects_delayed_sequence_collision()
    {
        Assert.Throws<InvalidOperationException>(
            () => InjectDelayed(
                CreateThreePayments(),
                sourceEventId: "payment-captured-002",
                delayedDeliverySequence: 3));
    }

    [Fact]
    public void Manifest_entry_rejects_delayed_sequence_at_or_before_original_sequence()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new DelayedDeliveryFaultManifestEntry(
                "fault-delayed-payment-001",
                "payment-captured-002",
                originalDeliverySequence: 2,
                delayedDeliverySequence: 2));
    }

    [Fact]
    public void Returned_collections_are_read_only()
    {
        var result = InjectDelayed(CreateThreePayments(), sourceEventId: "payment-captured-002");
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
                new DelayedDeliveryFaultManifestEntry(
                    "fault-delayed-payment-002",
                    "payment-captured-004",
                    originalDeliverySequence: 4,
                    delayedDeliverySequence: 5)));
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

    private static DelayedPaymentFaultInjectionResult InjectDelayed(
        IReadOnlyList<PaymentCaptured> truthStream,
        string sourceEventId,
        long delayedDeliverySequence = 4)
    {
        return new DelayedPaymentDeliveryFaultInjector().Inject(
            truthStream,
            new DelayedPaymentFaultRequest(
                "fault-delayed-payment-001",
                sourceEventId,
                delayedDeliverySequence));
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

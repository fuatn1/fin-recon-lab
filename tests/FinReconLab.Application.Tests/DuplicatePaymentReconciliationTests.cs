using System.Reflection;
using FinReconLab.Application;
using FinReconLab.Domain;

namespace FinReconLab.Application.Tests;

public sealed class DuplicatePaymentReconciliationTests
{
    private static readonly DateTimeOffset SuppliedTimestamp = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void One_truth_payment_produces_expected_usd_100()
    {
        var truthEventStream = new[] { CreatePayment() };
        var cutoff = new ReconciliationCutoff(1);

        var snapshot = new ExpectedPaymentProjection()
            .Build("order-001", "USD", cutoff, truthEventStream);

        Assert.Equal("order-001", snapshot.OrderId);
        Assert.Equal(new Money(100m, "USD"), snapshot.CapturedAmount);
        Assert.Equal(cutoff, snapshot.Cutoff);
    }

    [Fact]
    public void Duplicate_delivery_produces_observed_usd_200_for_non_idempotent_experiment()
    {
        var truthEventStream = new[] { CreatePayment() };
        var injection = InjectDuplicate(truthEventStream);
        var cutoff = new ReconciliationCutoff(2);

        var observed = new NonIdempotentObservedPaymentProjectionForExperiment()
            .Build("order-001", "USD", cutoff, injection.DeliveredEventStream);

        Assert.Equal(new Money(200m, "USD"), observed.CapturedAmount);
    }

    [Fact]
    public void Reconciliation_produces_one_captured_amount_mismatch_for_duplicate_delivery_experiment()
    {
        var truthEventStream = new[] { CreatePayment() };
        var injection = InjectDuplicate(truthEventStream);
        var cutoff = new ReconciliationCutoff(2);
        var expected = new ExpectedPaymentProjection().Build("order-001", "USD", cutoff, truthEventStream);
        var observed = new NonIdempotentObservedPaymentProjectionForExperiment()
            .Build("order-001", "USD", cutoff, injection.DeliveredEventStream);

        var findings = new PaymentReconciliationEngine().Reconcile(expected, observed);

        var finding = Assert.Single(findings);
        Assert.Equal(ReconciliationFindingCategory.CapturedAmountMismatch, finding.Category);
        Assert.Equal("order-001", finding.OrderId);
        Assert.Equal(new Money(100m, "USD"), finding.ExpectedAmount);
        Assert.Equal(new Money(200m, "USD"), finding.ObservedAmount);
        Assert.Equal(new Money(100m, "USD"), finding.SignedDelta);
    }

    [Fact]
    public void Unchanged_stream_produces_no_finding()
    {
        var payment = CreatePayment();
        var truthEventStream = new[] { payment };
        var deliveredEventStream = new[] { new DeliveredPaymentCaptured(payment, deliverySequence: 1, deliveryAttempt: 1) };
        var cutoff = new ReconciliationCutoff(1);
        var expected = new ExpectedPaymentProjection().Build("order-001", "USD", cutoff, truthEventStream);
        var observed = new NonIdempotentObservedPaymentProjectionForExperiment()
            .Build("order-001", "USD", cutoff, deliveredEventStream);

        var findings = new PaymentReconciliationEngine().Reconcile(expected, observed);

        Assert.Empty(findings);
    }

    [Fact]
    public void Events_beyond_logical_cutoff_are_excluded()
    {
        var truthEventStream = new[] { CreatePayment() };
        var injection = InjectDuplicate(truthEventStream);
        var cutoff = new ReconciliationCutoff(1);
        var expected = new ExpectedPaymentProjection().Build("order-001", "USD", cutoff, truthEventStream);
        var observed = new NonIdempotentObservedPaymentProjectionForExperiment()
            .Build("order-001", "USD", cutoff, injection.DeliveredEventStream);

        var findings = new PaymentReconciliationEngine().Reconcile(expected, observed);

        Assert.Equal(new Money(100m, "USD"), observed.CapturedAmount);
        Assert.Empty(findings);
    }

    [Fact]
    public void Repeating_same_inputs_produces_identical_delivered_streams_and_findings()
    {
        var firstTruthEventStream = new[] { CreatePayment() };
        var secondTruthEventStream = new[] { CreatePayment() };
        var firstInjection = InjectDuplicate(firstTruthEventStream);
        var secondInjection = InjectDuplicate(secondTruthEventStream);
        var cutoff = new ReconciliationCutoff(2);

        var firstFindings = Reconcile(firstTruthEventStream, firstInjection.DeliveredEventStream, cutoff);
        var secondFindings = Reconcile(secondTruthEventStream, secondInjection.DeliveredEventStream, cutoff);

        AssertDeliveredStreamsEqual(firstInjection.DeliveredEventStream, secondInjection.DeliveredEventStream);
        AssertFaultManifestsEqual(firstInjection.FaultManifest, secondInjection.FaultManifest);
        Assert.Equal(firstFindings, secondFindings);
    }

    [Fact]
    public void Fault_manifest_records_duplicate_delivery()
    {
        var truthEventStream = new[] { CreatePayment() };

        var injection = InjectDuplicate(truthEventStream);

        var entry = Assert.IsType<DuplicateDeliveryFaultManifestEntry>(Assert.Single(injection.FaultManifest.Entries));
        Assert.Equal("fault-duplicate-payment-001", entry.FaultId);
        Assert.Equal(FaultKind.DuplicateDelivery, entry.Kind);
        Assert.Equal("payment-captured-001", entry.SourceEventId);
        Assert.Equal(2, entry.DeliverySequence);
    }

    [Fact]
    public void Reconciliation_engine_public_methods_do_not_accept_fault_manifest()
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
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Fault_request_rejects_malformed_fault_id(string faultId)
    {
        Assert.Throws<ArgumentException>(
            () => new DuplicatePaymentFaultRequest(faultId, "payment-captured-001", 2));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Fault_request_rejects_malformed_source_event_id(string sourceEventId)
    {
        Assert.Throws<ArgumentException>(
            () => new DuplicatePaymentFaultRequest("fault-duplicate-payment-001", sourceEventId, 2));
    }

    [Fact]
    public void Fault_request_rejects_negative_delivery_sequence()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new DuplicatePaymentFaultRequest("fault-duplicate-payment-001", "payment-captured-001", -1));
    }

    [Fact]
    public void Fault_injector_rejects_unknown_source_event_id()
    {
        var truthEventStream = new[] { CreatePayment() };
        var request = new DuplicatePaymentFaultRequest("fault-duplicate-payment-001", "missing-event", 2);

        Assert.Throws<InvalidOperationException>(
            () => new DuplicatePaymentDeliveryFaultInjector().Inject(truthEventStream, request));
    }

    [Fact]
    public void Fault_injector_rejects_duplicate_source_event_ids()
    {
        var truthEventStream = new[]
        {
            CreatePayment(logicalSequence: 1),
            CreatePayment(logicalSequence: 2)
        };

        Assert.Throws<InvalidOperationException>(() => InjectDuplicate(truthEventStream));
    }

    [Fact]
    public void Fault_injector_rejects_duplicate_delivery_sequence_at_or_before_source_delivery()
    {
        var truthEventStream = new[] { CreatePayment(logicalSequence: 3) };
        var request = new DuplicatePaymentFaultRequest("fault-duplicate-payment-001", "payment-captured-001", 3);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => new DuplicatePaymentDeliveryFaultInjector().Inject(truthEventStream, request));
    }

    [Fact]
    public void Fault_injector_rejects_duplicate_delivery_sequence_collision()
    {
        var truthEventStream = new[]
        {
            CreatePayment(logicalSequence: 1),
            CreatePayment(eventId: "payment-captured-002", logicalSequence: 2)
        };
        var request = new DuplicatePaymentFaultRequest("fault-duplicate-payment-001", "payment-captured-001", 2);

        Assert.Throws<InvalidOperationException>(
            () => new DuplicatePaymentDeliveryFaultInjector().Inject(truthEventStream, request));
    }

    [Fact]
    public void Reconciliation_rejects_different_order_ids()
    {
        var cutoff = new ReconciliationCutoff(1);
        var expected = new PaymentSnapshot("order-001", new Money(100m, "USD"), cutoff);
        var observed = new PaymentSnapshot("order-002", new Money(100m, "USD"), cutoff);

        Assert.Throws<InvalidOperationException>(() => new PaymentReconciliationEngine().Reconcile(expected, observed));
    }

    [Fact]
    public void Reconciliation_rejects_different_currencies()
    {
        var cutoff = new ReconciliationCutoff(1);
        var expected = new PaymentSnapshot("order-001", new Money(100m, "USD"), cutoff);
        var observed = new PaymentSnapshot("order-001", new Money(100m, "EUR"), cutoff);

        Assert.Throws<InvalidOperationException>(() => new PaymentReconciliationEngine().Reconcile(expected, observed));
    }

    [Fact]
    public void Reconciliation_rejects_different_cutoffs()
    {
        var expected = new PaymentSnapshot("order-001", new Money(100m, "USD"), new ReconciliationCutoff(1));
        var observed = new PaymentSnapshot("order-001", new Money(100m, "USD"), new ReconciliationCutoff(2));

        Assert.Throws<InvalidOperationException>(() => new PaymentReconciliationEngine().Reconcile(expected, observed));
    }

    private static IReadOnlyList<ReconciliationFinding> Reconcile(
        IReadOnlyList<PaymentCaptured> truthEventStream,
        IReadOnlyList<DeliveredPaymentCaptured> deliveredEventStream,
        ReconciliationCutoff cutoff)
    {
        var expected = new ExpectedPaymentProjection().Build("order-001", "USD", cutoff, truthEventStream);
        var observed = new NonIdempotentObservedPaymentProjectionForExperiment()
            .Build("order-001", "USD", cutoff, deliveredEventStream);

        return new PaymentReconciliationEngine().Reconcile(expected, observed);
    }

    private static DuplicatePaymentFaultInjectionResult InjectDuplicate(IReadOnlyList<PaymentCaptured> truthEventStream)
    {
        return new DuplicatePaymentDeliveryFaultInjector().Inject(
            truthEventStream,
            new DuplicatePaymentFaultRequest(
                "fault-duplicate-payment-001",
                "payment-captured-001",
                duplicateDeliverySequence: 2));
    }

    private static PaymentCaptured CreatePayment(
        string eventId = "payment-captured-001",
        string orderId = "order-001",
        long logicalSequence = 1)
    {
        return new PaymentCaptured(
            eventId,
            orderId,
            new Money(100m, "USD"),
            logicalSequence,
            SuppliedTimestamp);
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

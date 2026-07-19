using System.Reflection;
using FinReconLab.Domain;

namespace FinReconLab.Application.Tests;

public sealed class InconsistentPaymentAmountDeliveryFaultInjectorTests
{
    private static readonly DateTimeOffset SuppliedTimestamp = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Selected_delivery_carries_only_the_supplied_inconsistent_amount()
    {
        var truthStream = CreateThreePayments();
        var selectedSource = truthStream[1];

        var result = InjectInconsistentAmount(truthStream, new Money(125m, "USD"));

        var selectedDelivery = Assert.Single(
            result.DeliveredEventStream,
            delivery => delivery.SourceEventId == selectedSource.EventId);
        Assert.Same(selectedSource, selectedDelivery.SourceEvent);
        Assert.Equal(new Money(100m, "USD"), selectedSource.CapturedAmount);
        Assert.Equal(new Money(125m, "USD"), selectedDelivery.DeliveredCapturedAmount);

        Assert.All(
            result.DeliveredEventStream.Where(delivery => delivery.SourceEventId != selectedSource.EventId),
            delivery => Assert.Equal(delivery.SourceEvent.CapturedAmount, delivery.DeliveredCapturedAmount));
    }

    [Fact]
    public void Injection_preserves_event_fields_baseline_delivery_positions_and_attempts()
    {
        var truthStream = CreateThreePayments();

        var result = InjectInconsistentAmount(truthStream, new Money(125m, "USD"));

        Assert.Equal(truthStream.Length, result.DeliveredEventStream.Count);
        foreach (var sourceEvent in truthStream)
        {
            var delivery = Assert.Single(
                result.DeliveredEventStream,
                candidate => candidate.SourceEventId == sourceEvent.EventId);

            Assert.Same(sourceEvent, delivery.SourceEvent);
            Assert.Equal(sourceEvent.EventId, delivery.SourceEvent.EventId);
            Assert.Equal(sourceEvent.OrderId, delivery.SourceEvent.OrderId);
            Assert.Equal(sourceEvent.LogicalSequence, delivery.SourceEvent.LogicalSequence);
            Assert.Equal(sourceEvent.OccurredAt, delivery.SourceEvent.OccurredAt);
            Assert.Equal(sourceEvent.LogicalSequence, delivery.DeliverySequence);
            Assert.Equal(1, delivery.DeliveryAttempt);
        }

        Assert.Equal(new long[] { 1, 2, 3 }, result.DeliveredEventStream.Select(delivery => delivery.DeliverySequence));
    }

    [Fact]
    public void Manifest_records_exact_original_and_delivered_amounts()
    {
        var result = InjectInconsistentAmount(CreateThreePayments(), new Money(125.25m, "USD"));

        var entry = Assert.IsType<InconsistentAmountDeliveryFaultManifestEntry>(
            Assert.Single(result.FaultManifest.Entries));
        Assert.Equal("fault-inconsistent-payment-001", entry.FaultId);
        Assert.Equal("payment-captured-002", entry.SourceEventId);
        Assert.Equal(FaultKind.InconsistentAmountDelivery, entry.Kind);
        Assert.Equal(new Money(100m, "USD"), entry.OriginalCapturedAmount);
        Assert.Equal(new Money(125.25m, "USD"), entry.DeliveredCapturedAmount);
    }

    [Fact]
    public void Higher_delivered_amount_produces_exact_positive_signed_delta()
    {
        var truthStream = CreateThreePayments();
        var result = InjectInconsistentAmount(truthStream, new Money(125m, "USD"));

        var finding = Assert.Single(Reconcile(truthStream, result.DeliveredEventStream, new ReconciliationCutoff(2)));

        Assert.Equal(new Money(110m, "USD"), finding.ExpectedAmount);
        Assert.Equal(new Money(135m, "USD"), finding.ObservedAmount);
        Assert.Equal(new Money(25m, "USD"), finding.SignedDelta);
    }

    [Fact]
    public void Lower_delivered_amount_produces_exact_negative_signed_delta()
    {
        var truthStream = CreateThreePayments();
        var result = InjectInconsistentAmount(truthStream, new Money(75m, "USD"));

        var finding = Assert.Single(Reconcile(truthStream, result.DeliveredEventStream, new ReconciliationCutoff(2)));

        Assert.Equal(new Money(110m, "USD"), finding.ExpectedAmount);
        Assert.Equal(new Money(85m, "USD"), finding.ObservedAmount);
        Assert.Equal(new Money(-25m, "USD"), finding.SignedDelta);
    }

    [Fact]
    public void Cutoff_before_selected_event_produces_no_premature_mismatch()
    {
        var truthStream = CreateThreePayments();
        var result = InjectInconsistentAmount(truthStream, new Money(125m, "USD"));

        var findings = Reconcile(truthStream, result.DeliveredEventStream, new ReconciliationCutoff(1));

        Assert.Empty(findings);
    }

    [Fact]
    public void Cutoff_including_selected_event_produces_mismatch()
    {
        var truthStream = CreateThreePayments();
        var result = InjectInconsistentAmount(truthStream, new Money(125m, "USD"));

        var finding = Assert.Single(Reconcile(truthStream, result.DeliveredEventStream, new ReconciliationCutoff(2)));

        Assert.Equal(ReconciliationFindingCategory.CapturedAmountMismatch, finding.Category);
        Assert.Equal(new Money(25m, "USD"), finding.SignedDelta);
    }

    [Fact]
    public void Structurally_identical_inputs_produce_identical_streams_manifests_and_findings()
    {
        var firstTruthStream = CreateThreePayments();
        var secondTruthStream = CreateThreePayments();
        var firstResult = InjectInconsistentAmount(firstTruthStream, new Money(125m, "USD"));
        var secondResult = InjectInconsistentAmount(secondTruthStream, new Money(125m, "USD"));
        var cutoff = new ReconciliationCutoff(3);

        var firstFindings = Reconcile(firstTruthStream, firstResult.DeliveredEventStream, cutoff);
        var secondFindings = Reconcile(secondTruthStream, secondResult.DeliveredEventStream, cutoff);

        Assert.Equal(firstResult.DeliveredEventStream, secondResult.DeliveredEventStream);
        Assert.Equal(firstResult.FaultManifest.Entries, secondResult.FaultManifest.Entries);
        Assert.Equal(firstFindings, secondFindings);
    }

    [Fact]
    public void Duplicate_logical_sequences_are_supported_with_ordinal_identity_ordering()
    {
        var truthStream = new[]
        {
            CreatePayment("payment-captured-002", 1, 100m),
            CreatePayment("payment-captured-001", 1, 10m)
        };

        var result = InjectInconsistentAmount(
            truthStream,
            new Money(125m, "USD"),
            sourceEventId: "payment-captured-002");

        Assert.Equal(
            new[] { "payment-captured-001", "payment-captured-002" },
            result.DeliveredEventStream.Select(delivery => delivery.SourceEventId));
        Assert.All(result.DeliveredEventStream, delivery => Assert.Equal(1, delivery.DeliverySequence));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Fault_request_rejects_blank_fault_id(string faultId)
    {
        Assert.Throws<ArgumentException>(
            () => new InconsistentPaymentAmountFaultRequest(
                faultId,
                "payment-captured-002",
                new Money(125m, "USD")));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Fault_request_rejects_blank_source_event_id(string sourceEventId)
    {
        Assert.Throws<ArgumentException>(
            () => new InconsistentPaymentAmountFaultRequest(
                "fault-inconsistent-payment-001",
                sourceEventId,
                new Money(125m, "USD")));
    }

    [Fact]
    public void Fault_request_rejects_null_delivered_amount()
    {
        Assert.Throws<ArgumentNullException>(
            () => new InconsistentPaymentAmountFaultRequest(
                "fault-inconsistent-payment-001",
                "payment-captured-002",
                null!));
    }

    [Fact]
    public void Injector_rejects_null_truth_stream()
    {
        var request = CreateRequest(new Money(125m, "USD"));

        Assert.Throws<ArgumentNullException>(
            () => new InconsistentPaymentAmountDeliveryFaultInjector().Inject(null!, request));
    }

    [Fact]
    public void Injector_rejects_null_request()
    {
        Assert.Throws<ArgumentNullException>(
            () => new InconsistentPaymentAmountDeliveryFaultInjector().Inject(CreateThreePayments(), null!));
    }

    [Fact]
    public void Injector_rejects_unknown_source_event_id()
    {
        var request = CreateRequest(new Money(125m, "USD"), sourceEventId: "missing-event");

        Assert.Throws<InvalidOperationException>(
            () => new InconsistentPaymentAmountDeliveryFaultInjector().Inject(CreateThreePayments(), request));
    }

    [Fact]
    public void Injector_rejects_duplicate_source_event_identities()
    {
        var truthStream = new[]
        {
            CreatePayment("payment-captured-002", 1, 100m),
            CreatePayment("payment-captured-002", 2, 150m)
        };

        Assert.Throws<InvalidOperationException>(
            () => InjectInconsistentAmount(truthStream, new Money(125m, "USD")));
    }

    [Fact]
    public void Injector_rejects_delivered_amount_with_different_currency()
    {
        Assert.Throws<InvalidOperationException>(
            () => InjectInconsistentAmount(CreateThreePayments(), new Money(125m, "EUR")));
    }

    [Fact]
    public void Injector_rejects_unchanged_delivered_amount()
    {
        Assert.Throws<ArgumentException>(
            () => InjectInconsistentAmount(CreateThreePayments(), new Money(100m, "USD")));
    }

    [Fact]
    public void Returned_collections_are_read_only()
    {
        var result = InjectInconsistentAmount(CreateThreePayments(), new Money(125m, "USD"));
        var deliveredCollection = Assert.IsAssignableFrom<ICollection<DeliveredPaymentCaptured>>(result.DeliveredEventStream);
        var manifestCollection = Assert.IsAssignableFrom<ICollection<FaultManifestEntry>>(result.FaultManifest.Entries);

        Assert.True(deliveredCollection.IsReadOnly);
        Assert.True(manifestCollection.IsReadOnly);
        Assert.Throws<NotSupportedException>(
            () => deliveredCollection.Add(
                new DeliveredPaymentCaptured(CreatePayment("payment-captured-004", 4, 10m), 4, 1)));
        Assert.Throws<NotSupportedException>(
            () => manifestCollection.Add(
                new InconsistentAmountDeliveryFaultManifestEntry(
                    "fault-inconsistent-payment-002",
                    "payment-captured-004",
                    new Money(10m, "USD"),
                    new Money(20m, "USD"))));
    }

    [Fact]
    public void Delivered_payment_explicit_amount_constructor_rejects_null_inputs()
    {
        var sourceEvent = CreatePayment("payment-captured-001", 1, 10m);

        Assert.Throws<ArgumentNullException>(
            () => new DeliveredPaymentCaptured(null!, new Money(20m, "USD"), 1, 1));
        Assert.Throws<ArgumentNullException>(
            () => new DeliveredPaymentCaptured(sourceEvent, null!, 1, 1));
    }

    [Fact]
    public void Manifest_entry_rejects_null_original_amount()
    {
        Assert.Throws<ArgumentNullException>(
            () => new InconsistentAmountDeliveryFaultManifestEntry(
                "fault-inconsistent-payment-001",
                "payment-captured-002",
                null!,
                new Money(125m, "USD")));
    }

    [Fact]
    public void Manifest_entry_rejects_null_delivered_amount()
    {
        Assert.Throws<ArgumentNullException>(
            () => new InconsistentAmountDeliveryFaultManifestEntry(
                "fault-inconsistent-payment-001",
                "payment-captured-002",
                new Money(100m, "USD"),
                null!));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Manifest_entry_rejects_blank_fault_id(string faultId)
    {
        Assert.Throws<ArgumentException>(
            () => new InconsistentAmountDeliveryFaultManifestEntry(
                faultId,
                "payment-captured-002",
                new Money(100m, "USD"),
                new Money(125m, "USD")));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Manifest_entry_rejects_blank_source_event_id(string sourceEventId)
    {
        Assert.Throws<ArgumentException>(
            () => new InconsistentAmountDeliveryFaultManifestEntry(
                "fault-inconsistent-payment-001",
                sourceEventId,
                new Money(100m, "USD"),
                new Money(125m, "USD")));
    }

    [Fact]
    public void Manifest_entry_rejects_currency_mismatch()
    {
        Assert.Throws<InvalidOperationException>(
            () => CreateManifestEntry(deliveredCapturedAmount: new Money(125m, "EUR")));
    }

    [Fact]
    public void Manifest_entry_rejects_equal_amounts()
    {
        Assert.Throws<ArgumentException>(
            () => CreateManifestEntry(deliveredCapturedAmount: new Money(100m, "USD")));
    }

    [Fact]
    public void Existing_fault_paths_deliver_source_amounts_unchanged()
    {
        var truthStream = CreateThreePayments();
        var deliveredStreams = new IReadOnlyList<DeliveredPaymentCaptured>[]
        {
            new DuplicatePaymentDeliveryFaultInjector().Inject(
                truthStream,
                new DuplicatePaymentFaultRequest("fault-duplicate-payment-001", "payment-captured-001", 4))
                .DeliveredEventStream,
            new MissingPaymentDeliveryFaultInjector().Inject(
                truthStream,
                new MissingPaymentFaultRequest("fault-missing-payment-001", "payment-captured-002"))
                .DeliveredEventStream,
            new DelayedPaymentDeliveryFaultInjector().Inject(
                truthStream,
                new DelayedPaymentFaultRequest("fault-delayed-payment-001", "payment-captured-002", 4))
                .DeliveredEventStream,
            new OutOfOrderPaymentDeliveryFaultInjector().Inject(
                truthStream,
                new OutOfOrderPaymentFaultRequest(
                    "fault-out-of-order-payment-001",
                    "payment-captured-001",
                    "payment-captured-002"))
                .DeliveredEventStream
        };

        Assert.Equal(4, deliveredStreams[0].Count);
        Assert.Equal(2, deliveredStreams[1].Count);
        Assert.Equal(new long[] { 1, 3, 4 }, deliveredStreams[2].Select(delivery => delivery.DeliverySequence));
        Assert.Equal(
            new[] { "payment-captured-002", "payment-captured-001", "payment-captured-003" },
            deliveredStreams[3].Select(delivery => delivery.SourceEventId));

        foreach (var deliveredStream in deliveredStreams)
        {
            Assert.All(
                deliveredStream,
                delivery => Assert.Equal(delivery.SourceEvent.CapturedAmount, delivery.DeliveredCapturedAmount));
        }
    }

    [Fact]
    public void Reconciliation_engine_public_methods_do_not_accept_manifest_or_fault_results()
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
        Assert.DoesNotContain(typeof(InconsistentPaymentAmountFaultInjectionResult), methodParameters);
    }

    private static InconsistentPaymentAmountFaultInjectionResult InjectInconsistentAmount(
        IReadOnlyList<PaymentCaptured> truthStream,
        Money deliveredCapturedAmount,
        string sourceEventId = "payment-captured-002")
    {
        return new InconsistentPaymentAmountDeliveryFaultInjector().Inject(
            truthStream,
            CreateRequest(deliveredCapturedAmount, sourceEventId));
    }

    private static InconsistentPaymentAmountFaultRequest CreateRequest(
        Money deliveredCapturedAmount,
        string sourceEventId = "payment-captured-002")
    {
        return new InconsistentPaymentAmountFaultRequest(
            "fault-inconsistent-payment-001",
            sourceEventId,
            deliveredCapturedAmount);
    }

    private static IReadOnlyList<ReconciliationFinding> Reconcile(
        IReadOnlyList<PaymentCaptured> truthStream,
        IReadOnlyList<DeliveredPaymentCaptured> deliveredStream,
        ReconciliationCutoff cutoff)
    {
        var expected = new ExpectedPaymentProjection().Build("order-001", "USD", cutoff, truthStream);
        var observed = new NonIdempotentObservedPaymentProjectionForExperiment()
            .Build("order-001", "USD", cutoff, deliveredStream);

        return new PaymentReconciliationEngine().Reconcile(expected, observed);
    }

    private static PaymentCaptured[] CreateThreePayments()
    {
        return
        [
            CreatePayment("payment-captured-001", 1, 10m),
            CreatePayment("payment-captured-002", 2, 100m),
            CreatePayment("payment-captured-003", 3, 30m)
        ];
    }

    private static PaymentCaptured CreatePayment(string eventId, long logicalSequence, decimal amount)
    {
        return new PaymentCaptured(
            eventId,
            "order-001",
            new Money(amount, "USD"),
            logicalSequence,
            SuppliedTimestamp.AddMinutes(logicalSequence));
    }

    private static InconsistentAmountDeliveryFaultManifestEntry CreateManifestEntry(
        Money? deliveredCapturedAmount = null)
    {
        return new InconsistentAmountDeliveryFaultManifestEntry(
            "fault-inconsistent-payment-001",
            "payment-captured-002",
            new Money(100m, "USD"),
            deliveredCapturedAmount ?? new Money(125m, "USD"));
    }
}

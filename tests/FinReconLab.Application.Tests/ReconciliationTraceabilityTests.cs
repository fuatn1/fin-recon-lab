using System.Reflection;
using FinReconLab.Domain;

namespace FinReconLab.Application.Tests;

public sealed class ReconciliationTraceabilityTests
{
    private static readonly DateTimeOffset SuppliedTimestamp = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Expected_projection_builds_exact_ordered_trace_and_excludes_events_beyond_cutoff()
    {
        var truthStream = new[]
        {
            CreatePayment("payment-b", "order-001", 1, 20m),
            CreatePayment("payment-c", "order-001", 3, 30m),
            CreatePayment("payment-a", "order-001", 1, 10m),
            CreatePayment("payment-other", "order-002", 1, 50m)
        };

        var snapshot = new ExpectedPaymentProjection().Build(
            "order-001",
            "USD",
            new ReconciliationCutoff(2),
            truthStream);

        Assert.Equal(new Money(30m, "USD"), snapshot.CapturedAmount);
        Assert.Equal(
            new[]
            {
                new ExpectedPaymentContribution("payment-a", 1, new Money(10m, "USD")),
                new ExpectedPaymentContribution("payment-b", 1, new Money(20m, "USD"))
            },
            snapshot.Contributions);
    }

    [Fact]
    public void Observed_projection_builds_exact_ordered_trace_from_delivered_amounts_and_cutoff()
    {
        var paymentA = CreatePayment("payment-a", "order-001", 1, 10m);
        var paymentB = CreatePayment("payment-b", "order-001", 2, 20m);
        var deliveredStream = new[]
        {
            new DeliveredPaymentCaptured(paymentB, new Money(25m, "USD"), 1, 1),
            new DeliveredPaymentCaptured(paymentA, new Money(12m, "USD"), 1, 2),
            new DeliveredPaymentCaptured(paymentA, new Money(11m, "USD"), 1, 1),
            new DeliveredPaymentCaptured(paymentB, new Money(30m, "USD"), 3, 2)
        };

        var snapshot = new NonIdempotentObservedPaymentProjectionForExperiment().Build(
            "order-001",
            "USD",
            new ReconciliationCutoff(2),
            deliveredStream);

        Assert.Equal(new Money(48m, "USD"), snapshot.CapturedAmount);
        Assert.Equal(
            new[]
            {
                new ObservedPaymentContribution("payment-a", 1, 1, 1, new Money(11m, "USD")),
                new ObservedPaymentContribution("payment-a", 1, 1, 2, new Money(12m, "USD")),
                new ObservedPaymentContribution("payment-b", 2, 1, 1, new Money(25m, "USD"))
            },
            snapshot.Contributions);
    }

    [Fact]
    public void Duplicate_delivery_appears_as_distinct_observed_attempt_contributions_on_finding()
    {
        var truthStream = new[] { CreatePayment("payment-001", "order-001", 1, 100m) };
        var injection = new DuplicatePaymentDeliveryFaultInjector().Inject(
            truthStream,
            new DuplicatePaymentFaultRequest("fault-duplicate-001", "payment-001", 2));

        var finding = Assert.Single(Reconcile(
            truthStream,
            injection.DeliveredEventStream,
            "order-001",
            new ReconciliationCutoff(2)));

        Assert.Equal(
            new[] { new ExpectedPaymentContribution("payment-001", 1, new Money(100m, "USD")) },
            finding.ExpectedContributions);
        Assert.Equal(
            new[]
            {
                new ObservedPaymentContribution("payment-001", 1, 1, 1, new Money(100m, "USD")),
                new ObservedPaymentContribution("payment-001", 1, 2, 2, new Money(100m, "USD"))
            },
            finding.ObservedContributions);
    }

    [Fact]
    public void Missing_delivery_remains_in_expected_trace_and_is_absent_from_observed_trace()
    {
        var truthStream = new[] { CreatePayment("payment-001", "order-001", 1, 100m) };
        var injection = new MissingPaymentDeliveryFaultInjector().Inject(
            truthStream,
            new MissingPaymentFaultRequest("fault-missing-001", "payment-001"));

        var finding = Assert.Single(Reconcile(
            truthStream,
            injection.DeliveredEventStream,
            "order-001",
            new ReconciliationCutoff(1)));

        Assert.Single(finding.ExpectedContributions);
        Assert.Equal("payment-001", finding.ExpectedContributions[0].SourceEventId);
        Assert.Empty(finding.ObservedContributions);
    }

    [Fact]
    public void Delayed_delivery_trace_exposes_modified_delivery_sequence()
    {
        var truthStream = new[] { CreatePayment("payment-001", "order-001", 1, 100m) };
        var injection = new DelayedPaymentDeliveryFaultInjector().Inject(
            truthStream,
            new DelayedPaymentFaultRequest("fault-delayed-001", "payment-001", 3));

        var beforeDelivery = Reconcile(
            truthStream,
            injection.DeliveredEventStream,
            "order-001",
            new ReconciliationCutoff(2));
        var includedObserved = new NonIdempotentObservedPaymentProjectionForExperiment().Build(
            "order-001",
            "USD",
            new ReconciliationCutoff(3),
            injection.DeliveredEventStream);

        var finding = Assert.Single(beforeDelivery);
        Assert.Single(finding.ExpectedContributions);
        Assert.Empty(finding.ObservedContributions);
        Assert.Equal(3, Assert.Single(includedObserved.Contributions).DeliverySequence);
    }

    [Fact]
    public void Out_of_order_trace_exposes_swapped_delivery_positions()
    {
        var truthStream = new[]
        {
            CreatePayment("payment-001", "order-A", 1, 100m),
            CreatePayment("payment-002", "order-B", 2, 200m)
        };
        var injection = new OutOfOrderPaymentDeliveryFaultInjector().Inject(
            truthStream,
            new OutOfOrderPaymentFaultRequest("fault-order-001", "payment-001", "payment-002"));

        var orderAFinding = Assert.Single(Reconcile(
            truthStream,
            injection.DeliveredEventStream,
            "order-A",
            new ReconciliationCutoff(1)));
        var orderBFinding = Assert.Single(Reconcile(
            truthStream,
            injection.DeliveredEventStream,
            "order-B",
            new ReconciliationCutoff(1)));

        Assert.Single(orderAFinding.ExpectedContributions);
        Assert.Empty(orderAFinding.ObservedContributions);
        var earlyObserved = Assert.Single(orderBFinding.ObservedContributions);
        Assert.Equal(2, earlyObserved.SourceLogicalSequence);
        Assert.Equal(1, earlyObserved.DeliverySequence);
    }

    [Fact]
    public void Inconsistent_amount_trace_preserves_clean_expected_and_corrupted_observed_amounts()
    {
        var truthStream = new[] { CreatePayment("payment-001", "order-001", 1, 100m) };
        var injection = new InconsistentPaymentAmountDeliveryFaultInjector().Inject(
            truthStream,
            new InconsistentPaymentAmountFaultRequest(
                "fault-amount-001",
                "payment-001",
                new Money(125m, "USD")));

        var finding = Assert.Single(Reconcile(
            truthStream,
            injection.DeliveredEventStream,
            "order-001",
            new ReconciliationCutoff(1)));

        Assert.Equal(new Money(100m, "USD"), Assert.Single(finding.ExpectedContributions).AppliedCapturedAmount);
        Assert.Equal(
            new Money(125m, "USD"),
            Assert.Single(finding.ObservedContributions).AppliedDeliveredCapturedAmount);
        Assert.Equal(new Money(25m, "USD"), finding.SignedDelta);
    }

    [Fact]
    public void Structurally_identical_inputs_produce_identical_traceable_findings()
    {
        var firstTruthStream = new[] { CreatePayment("payment-001", "order-001", 1, 100m) };
        var secondTruthStream = new[] { CreatePayment("payment-001", "order-001", 1, 100m) };
        var firstInjection = new DuplicatePaymentDeliveryFaultInjector().Inject(
            firstTruthStream,
            new DuplicatePaymentFaultRequest("fault-duplicate-001", "payment-001", 2));
        var secondInjection = new DuplicatePaymentDeliveryFaultInjector().Inject(
            secondTruthStream,
            new DuplicatePaymentFaultRequest("fault-duplicate-001", "payment-001", 2));

        var firstFindings = Reconcile(
            firstTruthStream,
            firstInjection.DeliveredEventStream,
            "order-001",
            new ReconciliationCutoff(2));
        var secondFindings = Reconcile(
            secondTruthStream,
            secondInjection.DeliveredEventStream,
            "order-001",
            new ReconciliationCutoff(2));

        Assert.Equal(firstFindings, secondFindings);
    }

    [Fact]
    public void Reconciliation_engine_accepts_only_role_specific_snapshots()
    {
        var methods = typeof(PaymentReconciliationEngine)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
        var reconcile = Assert.Single(methods);
        var parameterTypes = reconcile.GetParameters().Select(parameter => parameter.ParameterType).ToArray();

        Assert.Equal(
            new[] { typeof(ExpectedPaymentSnapshot), typeof(ObservedPaymentSnapshot) },
            parameterTypes);
        Assert.DoesNotContain(typeof(FaultManifest), parameterTypes);
        Assert.DoesNotContain(typeof(DuplicatePaymentFaultInjectionResult), parameterTypes);
        Assert.DoesNotContain(typeof(MissingPaymentFaultInjectionResult), parameterTypes);
        Assert.DoesNotContain(typeof(DelayedPaymentFaultInjectionResult), parameterTypes);
        Assert.DoesNotContain(typeof(OutOfOrderPaymentFaultInjectionResult), parameterTypes);
        Assert.DoesNotContain(typeof(InconsistentPaymentAmountFaultInjectionResult), parameterTypes);
    }

    [Fact]
    public void Reconciliation_engine_rejects_null_role_specific_snapshots()
    {
        var cutoff = new ReconciliationCutoff(1);
        var expected = new ExpectedPaymentSnapshot("order-001", Money.Zero("USD"), cutoff, []);
        var observed = new ObservedPaymentSnapshot("order-001", Money.Zero("USD"), cutoff, []);
        var engine = new PaymentReconciliationEngine();

        Assert.Throws<ArgumentNullException>(() => engine.Reconcile(null!, observed));
        Assert.Throws<ArgumentNullException>(() => engine.Reconcile(expected, null!));
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

    private static PaymentCaptured CreatePayment(
        string eventId,
        string orderId,
        long logicalSequence,
        decimal amount)
    {
        return new PaymentCaptured(
            eventId,
            orderId,
            new Money(amount, "USD"),
            logicalSequence,
            SuppliedTimestamp.AddMinutes(logicalSequence));
    }
}

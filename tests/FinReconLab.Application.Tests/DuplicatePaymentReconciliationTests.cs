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
        Assert.Equal("USD", finding.Currency);
        Assert.Equal(100m, finding.ExpectedAmount);
        Assert.Equal(200m, finding.ObservedAmount);
        Assert.Equal(100m, finding.SignedDelta);
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
        var truthEventStream = new[] { CreatePayment() };
        var firstInjection = InjectDuplicate(truthEventStream);
        var secondInjection = InjectDuplicate(truthEventStream);
        var cutoff = new ReconciliationCutoff(2);

        var firstFindings = Reconcile(truthEventStream, firstInjection.DeliveredEventStream, cutoff);
        var secondFindings = Reconcile(truthEventStream, secondInjection.DeliveredEventStream, cutoff);

        Assert.Equal(firstInjection.DeliveredEventStream, secondInjection.DeliveredEventStream);
        Assert.Equal(firstFindings, secondFindings);
    }

    [Fact]
    public void Fault_manifest_records_duplicate_delivery()
    {
        var truthEventStream = new[] { CreatePayment() };

        var injection = InjectDuplicate(truthEventStream);

        var entry = Assert.Single(injection.FaultManifest.Entries);
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
                DuplicateDeliverySequence: 2));
    }

    private static PaymentCaptured CreatePayment()
    {
        return new PaymentCaptured(
            "payment-captured-001",
            "order-001",
            new Money(100m, "USD"),
            logicalSequence: 1,
            SuppliedTimestamp);
    }
}

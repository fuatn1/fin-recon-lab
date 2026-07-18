using FinReconLab.Application;
using FinReconLab.Domain;

namespace FinReconLab.Application.Tests;

public sealed class PaymentCapturedTruthStreamGeneratorTests
{
    private static readonly DateTimeOffset GoldenStart = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Identical_definitions_generate_identical_event_streams()
    {
        var firstDefinition = CreateDefinition();
        var secondDefinition = CreateDefinition();
        var generator = new PaymentCapturedTruthStreamGenerator();

        var first = generator.Generate(firstDefinition);
        var second = generator.Generate(secondDefinition);

        Assert.Equal(first, second);
    }

    [Fact]
    public void Fixed_small_definition_produces_exact_golden_vector()
    {
        var definition = CreateDefinition(paymentCount: 3, paymentAmount: new Money(12.34m, "USD"));

        var events = new PaymentCapturedTruthStreamGenerator().Generate(definition);

        Assert.Collection(
            events,
            first => AssertPayment(first, 1, "payment-captured:scenario-alpha:seed-42:ordinal-000001", "order:scenario-alpha:seed-42:ordinal-000001", GoldenStart),
            second => AssertPayment(second, 2, "payment-captured:scenario-alpha:seed-42:ordinal-000002", "order:scenario-alpha:seed-42:ordinal-000002", GoldenStart.AddMinutes(5)),
            third => AssertPayment(third, 3, "payment-captured:scenario-alpha:seed-42:ordinal-000003", "order:scenario-alpha:seed-42:ordinal-000003", GoldenStart.AddMinutes(10)));

        static void AssertPayment(
            PaymentCaptured payment,
            long logicalSequence,
            string eventId,
            string orderId,
            DateTimeOffset occurredAt)
        {
            Assert.Equal(eventId, payment.EventId);
            Assert.Equal(orderId, payment.OrderId);
            Assert.Equal(new Money(12.34m, "USD"), payment.CapturedAmount);
            Assert.Equal(logicalSequence, payment.LogicalSequence);
            Assert.Equal(occurredAt, payment.OccurredAt);
        }
    }

    [Fact]
    public void Different_seeds_produce_different_deterministic_identities()
    {
        var generator = new PaymentCapturedTruthStreamGenerator();
        var first = generator.Generate(CreateDefinition(seed: 42));
        var second = generator.Generate(CreateDefinition(seed: 43));

        Assert.NotEqual(first[0].EventId, second[0].EventId);
        Assert.NotEqual(first[0].OrderId, second[0].OrderId);
    }

    [Fact]
    public void Events_are_ordered_and_identities_are_unique()
    {
        var events = new PaymentCapturedTruthStreamGenerator().Generate(CreateDefinition(paymentCount: 5));

        Assert.Equal([1, 2, 3, 4, 5], events.Select(payment => payment.LogicalSequence));
        Assert.Equal(events.Count, events.Select(payment => payment.EventId).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(events.Count, events.Select(payment => payment.OrderId).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Returned_results_cannot_be_mutated_through_public_api()
    {
        var events = new PaymentCapturedTruthStreamGenerator().Generate(CreateDefinition());
        var collection = Assert.IsAssignableFrom<ICollection<PaymentCaptured>>(events);

        Assert.True(collection.IsReadOnly);
        Assert.Throws<NotSupportedException>(
            () => collection.Add(
                new PaymentCaptured(
                    "payment-captured:manual",
                    "order:manual",
                    new Money(100m, "USD"),
                    2,
                    GoldenStart)));
    }

    [Fact]
    public void Generated_stream_works_with_duplicate_delivery_and_reconciliation_pipeline()
    {
        var definition = CreateDefinition(paymentCount: 1, paymentAmount: new Money(100m, "USD"));
        var truthStream = new PaymentCapturedTruthStreamGenerator().Generate(definition);
        var payment = truthStream[0];
        var cutoff = new ReconciliationCutoff(2);
        var injection = new DuplicatePaymentDeliveryFaultInjector().Inject(
            truthStream,
            new DuplicatePaymentFaultRequest(
                "fault-duplicate-payment-001",
                payment.EventId,
                duplicateDeliverySequence: 2));

        var expected = new ExpectedPaymentProjection().Build(payment.OrderId, "USD", cutoff, truthStream);
        var observed = new NonIdempotentObservedPaymentProjectionForExperiment()
            .Build(payment.OrderId, "USD", cutoff, injection.DeliveredEventStream);

        var finding = Assert.Single(new PaymentReconciliationEngine().Reconcile(expected, observed));
        Assert.Equal(ReconciliationFindingCategory.CapturedAmountMismatch, finding.Category);
        Assert.Equal(new Money(100m, "USD"), finding.ExpectedAmount);
        Assert.Equal(new Money(200m, "USD"), finding.ObservedAmount);
        Assert.Equal(new Money(100m, "USD"), finding.SignedDelta);
    }

    [Fact]
    public void Boundary_definition_generates_expected_timestamps()
    {
        var startingTimestamp = DateTimeOffset.MaxValue.AddTicks(-1);
        var definition = new ScenarioDefinition(
            ScenarioDefinition.SupportedSchemaVersion,
            "scenario-boundary",
            42,
            paymentCount: 2,
            new Money(100m, "USD"),
            startingTimestamp,
            TimeSpan.FromTicks(1));

        var events = new PaymentCapturedTruthStreamGenerator().Generate(definition);

        Assert.Equal(startingTimestamp, events[0].OccurredAt);
        Assert.Equal(DateTimeOffset.MaxValue, events[1].OccurredAt);
    }

    private static ScenarioDefinition CreateDefinition(
        ulong seed = 42,
        int paymentCount = 2,
        Money? paymentAmount = null)
    {
        return new ScenarioDefinition(
            ScenarioDefinition.SupportedSchemaVersion,
            "scenario-alpha",
            seed,
            paymentCount,
            paymentAmount ?? new Money(100m, "USD"),
            GoldenStart,
            TimeSpan.FromMinutes(5));
    }
}

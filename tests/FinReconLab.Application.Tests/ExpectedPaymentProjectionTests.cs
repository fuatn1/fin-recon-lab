using FinReconLab.Domain;

namespace FinReconLab.Application.Tests;

public sealed class ExpectedPaymentProjectionTests
{
    private static readonly DateTimeOffset SuppliedTimestamp = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Events_beyond_cutoff_sequence_are_excluded_from_expected_state()
    {
        var truthStream = new[]
        {
            CreatePayment("payment-captured-001", logicalSequence: 1),
            CreatePayment("payment-captured-002", logicalSequence: 2)
        };
        var cutoff = new ReconciliationCutoff(1);

        var expected = new ExpectedPaymentProjection().Build("order-001", "USD", cutoff, truthStream);

        Assert.Equal(new Money(100m, "USD"), expected.CapturedAmount);
        Assert.Equal(cutoff, expected.Cutoff);
    }

    private static PaymentCaptured CreatePayment(string eventId, long logicalSequence)
    {
        return new PaymentCaptured(
            eventId,
            "order-001",
            new Money(100m, "USD"),
            logicalSequence,
            SuppliedTimestamp.AddMinutes(logicalSequence - 1));
    }
}

using FinReconLab.Domain;

namespace FinReconLab.Application;

public sealed class ExpectedPaymentProjection
{
    public ExpectedPaymentSnapshot Build(
        string orderId,
        string currency,
        ReconciliationCutoff cutoff,
        IEnumerable<PaymentCaptured> truthEventStream)
    {
        ArgumentNullException.ThrowIfNull(truthEventStream);

        var contributions = truthEventStream
            .Where(payment => payment.OrderId == orderId)
            .Where(payment => cutoff.Includes(payment.LogicalSequence))
            .OrderBy(payment => payment.LogicalSequence)
            .ThenBy(payment => payment.EventId, StringComparer.Ordinal)
            .Select(payment => new ExpectedPaymentContribution(
                payment.EventId,
                payment.LogicalSequence,
                payment.CapturedAmount))
            .ToArray();

        var capturedAmount = Money.Zero(currency);
        foreach (var contribution in contributions)
        {
            capturedAmount += contribution.AppliedCapturedAmount;
        }

        return new ExpectedPaymentSnapshot(orderId, capturedAmount, cutoff, contributions);
    }
}

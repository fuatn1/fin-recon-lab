using FinReconLab.Domain;

namespace FinReconLab.Application;

public sealed class ExpectedPaymentProjection
{
    public PaymentSnapshot Build(
        string orderId,
        string currency,
        ReconciliationCutoff cutoff,
        IEnumerable<PaymentCaptured> truthEventStream)
    {
        ArgumentNullException.ThrowIfNull(truthEventStream);

        var capturedAmount = Money.Zero(currency);

        foreach (var payment in truthEventStream
            .Where(payment => payment.OrderId == orderId)
            .OrderBy(payment => payment.LogicalSequence)
            .ThenBy(payment => payment.EventId, StringComparer.Ordinal))
        {
            capturedAmount += payment.CapturedAmount;
        }

        return new PaymentSnapshot(orderId, capturedAmount, cutoff);
    }
}

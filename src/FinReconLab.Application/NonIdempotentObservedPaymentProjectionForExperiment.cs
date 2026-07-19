using FinReconLab.Domain;

namespace FinReconLab.Application;

public sealed class NonIdempotentObservedPaymentProjectionForExperiment
{
    public PaymentSnapshot Build(
        string orderId,
        string currency,
        ReconciliationCutoff cutoff,
        IEnumerable<DeliveredPaymentCaptured> deliveredEventStream)
    {
        ArgumentNullException.ThrowIfNull(deliveredEventStream);

        var capturedAmount = Money.Zero(currency);

        foreach (var delivery in deliveredEventStream
            .Where(delivery => cutoff.Includes(delivery.DeliverySequence))
            .Where(delivery => delivery.SourceEvent.OrderId == orderId)
            .OrderBy(delivery => delivery.DeliverySequence)
            .ThenBy(delivery => delivery.SourceEventId, StringComparer.Ordinal)
            .ThenBy(delivery => delivery.DeliveryAttempt))
        {
            capturedAmount += delivery.DeliveredCapturedAmount;
        }

        return new PaymentSnapshot(orderId, capturedAmount, cutoff);
    }
}

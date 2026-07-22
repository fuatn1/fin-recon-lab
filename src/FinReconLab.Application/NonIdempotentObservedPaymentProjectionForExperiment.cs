using FinReconLab.Domain;

namespace FinReconLab.Application;

public sealed class NonIdempotentObservedPaymentProjectionForExperiment
{
    public ObservedPaymentSnapshot Build(
        string orderId,
        string currency,
        ReconciliationCutoff cutoff,
        IEnumerable<DeliveredPaymentCaptured> deliveredEventStream)
    {
        ArgumentNullException.ThrowIfNull(deliveredEventStream);

        var contributions = deliveredEventStream
            .Where(delivery => cutoff.Includes(delivery.DeliverySequence))
            .Where(delivery => delivery.SourceEvent.OrderId == orderId)
            .OrderBy(delivery => delivery.DeliverySequence)
            .ThenBy(delivery => delivery.SourceEventId, StringComparer.Ordinal)
            .ThenBy(delivery => delivery.DeliveryAttempt)
            .Select(delivery => new ObservedPaymentContribution(
                delivery.SourceEventId,
                delivery.SourceEvent.LogicalSequence,
                delivery.DeliverySequence,
                delivery.DeliveryAttempt,
                delivery.DeliveredCapturedAmount))
            .ToArray();

        var capturedAmount = Money.Zero(currency);
        foreach (var contribution in contributions)
        {
            capturedAmount += contribution.AppliedDeliveredCapturedAmount;
        }

        return new ObservedPaymentSnapshot(orderId, capturedAmount, cutoff, contributions);
    }
}

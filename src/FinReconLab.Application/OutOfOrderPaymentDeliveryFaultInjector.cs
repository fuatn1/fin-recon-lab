using FinReconLab.Domain;

namespace FinReconLab.Application;

public sealed class OutOfOrderPaymentDeliveryFaultInjector
{
    public OutOfOrderPaymentFaultInjectionResult Inject(
        IEnumerable<PaymentCaptured> truthEventStream,
        OutOfOrderPaymentFaultRequest request)
    {
        ArgumentNullException.ThrowIfNull(truthEventStream);
        ArgumentNullException.ThrowIfNull(request);

        var truthEvents = truthEventStream
            .OrderBy(payment => payment.LogicalSequence)
            .ThenBy(payment => payment.EventId, StringComparer.Ordinal)
            .ToArray();

        var duplicateSourceEventId = truthEvents
            .GroupBy(payment => payment.EventId, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1)
            ?.Key;

        if (duplicateSourceEventId is not null)
        {
            throw new InvalidOperationException(
                $"Truth event stream contains duplicate source event id '{duplicateSourceEventId}'.");
        }

        var duplicateLogicalSequence = truthEvents
            .GroupBy(payment => payment.LogicalSequence)
            .FirstOrDefault(group => group.Count() > 1)
            ?.Key;

        if (duplicateLogicalSequence is not null)
        {
            throw new InvalidOperationException(
                $"Truth event stream contains duplicate logical sequence '{duplicateLogicalSequence}'.");
        }

        var earlierEvent = truthEvents.FirstOrDefault(
            payment => StringComparer.Ordinal.Equals(payment.EventId, request.EarlierSourceEventId))
            ?? throw new InvalidOperationException(
                $"Earlier source event id '{request.EarlierSourceEventId}' was not found in the truth event stream.");

        var laterEvent = truthEvents.FirstOrDefault(
            payment => StringComparer.Ordinal.Equals(payment.EventId, request.LaterSourceEventId))
            ?? throw new InvalidOperationException(
                $"Later source event id '{request.LaterSourceEventId}' was not found in the truth event stream.");

        if (earlierEvent.LogicalSequence >= laterEvent.LogicalSequence)
        {
            throw new InvalidOperationException(
                "Earlier source event must have a lower logical sequence than the later source event.");
        }

        var deliveredEvents = truthEvents
            .Select(payment =>
            {
                var deliverySequence = payment.LogicalSequence;

                if (StringComparer.Ordinal.Equals(payment.EventId, request.EarlierSourceEventId))
                {
                    deliverySequence = laterEvent.LogicalSequence;
                }
                else if (StringComparer.Ordinal.Equals(payment.EventId, request.LaterSourceEventId))
                {
                    deliverySequence = earlierEvent.LogicalSequence;
                }

                return new DeliveredPaymentCaptured(payment, deliverySequence, deliveryAttempt: 1);
            })
            .OrderBy(delivery => delivery.DeliverySequence)
            .ThenBy(delivery => delivery.SourceEventId, StringComparer.Ordinal)
            .ThenBy(delivery => delivery.DeliveryAttempt)
            .ToArray();

        var manifest = new FaultManifest(
            [
                new OutOfOrderDeliveryFaultManifestEntry(
                    request.FaultId,
                    request.EarlierSourceEventId,
                    request.LaterSourceEventId,
                    earlierEvent.LogicalSequence,
                    laterEvent.LogicalSequence,
                    laterEvent.LogicalSequence,
                    earlierEvent.LogicalSequence)
            ]);

        return new OutOfOrderPaymentFaultInjectionResult(deliveredEvents, manifest);
    }
}

public sealed record OutOfOrderPaymentFaultRequest
{
    public OutOfOrderPaymentFaultRequest(
        string faultId,
        string earlierSourceEventId,
        string laterSourceEventId)
    {
        if (string.IsNullOrWhiteSpace(faultId))
        {
            throw new ArgumentException("Fault id is required.", nameof(faultId));
        }

        if (string.IsNullOrWhiteSpace(earlierSourceEventId))
        {
            throw new ArgumentException("Earlier source event id is required.", nameof(earlierSourceEventId));
        }

        if (string.IsNullOrWhiteSpace(laterSourceEventId))
        {
            throw new ArgumentException("Later source event id is required.", nameof(laterSourceEventId));
        }

        if (StringComparer.Ordinal.Equals(earlierSourceEventId, laterSourceEventId))
        {
            throw new ArgumentException(
                "Earlier and later source event ids must be different.",
                nameof(laterSourceEventId));
        }

        FaultId = faultId;
        EarlierSourceEventId = earlierSourceEventId;
        LaterSourceEventId = laterSourceEventId;
    }

    public string FaultId { get; }

    public string EarlierSourceEventId { get; }

    public string LaterSourceEventId { get; }
}

public sealed record OutOfOrderPaymentFaultInjectionResult
{
    public OutOfOrderPaymentFaultInjectionResult(
        IEnumerable<DeliveredPaymentCaptured> deliveredEventStream,
        FaultManifest faultManifest)
    {
        ArgumentNullException.ThrowIfNull(deliveredEventStream);
        ArgumentNullException.ThrowIfNull(faultManifest);

        DeliveredEventStream = Array.AsReadOnly(deliveredEventStream.ToArray());
        FaultManifest = faultManifest;
    }

    public IReadOnlyList<DeliveredPaymentCaptured> DeliveredEventStream { get; }

    public FaultManifest FaultManifest { get; }
}

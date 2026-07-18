using FinReconLab.Domain;

namespace FinReconLab.Application;

public sealed class DelayedPaymentDeliveryFaultInjector
{
    public DelayedPaymentFaultInjectionResult Inject(
        IEnumerable<PaymentCaptured> truthEventStream,
        DelayedPaymentFaultRequest request)
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

        var selectedEvent = truthEvents.FirstOrDefault(
            payment => StringComparer.Ordinal.Equals(payment.EventId, request.SourceEventId))
            ?? throw new InvalidOperationException(
                $"Source event id '{request.SourceEventId}' was not found in the truth event stream.");

        if (request.DelayedDeliverySequence <= selectedEvent.LogicalSequence)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                "Delayed delivery sequence must be after the selected source event baseline delivery sequence.");
        }

        if (truthEvents
            .Where(payment => !StringComparer.Ordinal.Equals(payment.EventId, request.SourceEventId))
            .Any(payment => payment.LogicalSequence == request.DelayedDeliverySequence))
        {
            throw new InvalidOperationException(
                $"Delayed delivery sequence '{request.DelayedDeliverySequence}' collides with an existing baseline delivery sequence.");
        }

        var deliveredEvents = truthEvents
            .Where(payment => !StringComparer.Ordinal.Equals(payment.EventId, request.SourceEventId))
            .Select(payment => new DeliveredPaymentCaptured(payment, payment.LogicalSequence, deliveryAttempt: 1))
            .Append(new DeliveredPaymentCaptured(selectedEvent, request.DelayedDeliverySequence, deliveryAttempt: 1))
            .OrderBy(delivery => delivery.DeliverySequence)
            .ThenBy(delivery => delivery.SourceEventId, StringComparer.Ordinal)
            .ThenBy(delivery => delivery.DeliveryAttempt)
            .ToArray();

        var manifest = new FaultManifest(
            [
                new DelayedDeliveryFaultManifestEntry(
                    request.FaultId,
                    request.SourceEventId,
                    selectedEvent.LogicalSequence,
                    request.DelayedDeliverySequence)
            ]);

        return new DelayedPaymentFaultInjectionResult(deliveredEvents, manifest);
    }
}

public sealed record DelayedPaymentFaultRequest
{
    public DelayedPaymentFaultRequest(string faultId, string sourceEventId, long delayedDeliverySequence)
    {
        if (string.IsNullOrWhiteSpace(faultId))
        {
            throw new ArgumentException("Fault id is required.", nameof(faultId));
        }

        if (string.IsNullOrWhiteSpace(sourceEventId))
        {
            throw new ArgumentException("Source event id is required.", nameof(sourceEventId));
        }

        if (delayedDeliverySequence < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(delayedDeliverySequence),
                "Delayed delivery sequence cannot be negative.");
        }

        FaultId = faultId;
        SourceEventId = sourceEventId;
        DelayedDeliverySequence = delayedDeliverySequence;
    }

    public string FaultId { get; }

    public string SourceEventId { get; }

    public long DelayedDeliverySequence { get; }
}

public sealed record DelayedPaymentFaultInjectionResult
{
    public DelayedPaymentFaultInjectionResult(
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

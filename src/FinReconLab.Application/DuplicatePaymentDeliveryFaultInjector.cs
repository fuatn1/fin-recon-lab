using FinReconLab.Domain;

namespace FinReconLab.Application;

public sealed class DuplicatePaymentDeliveryFaultInjector
{
    public DuplicatePaymentFaultInjectionResult Inject(
        IEnumerable<PaymentCaptured> truthEventStream,
        DuplicatePaymentFaultRequest request)
    {
        ArgumentNullException.ThrowIfNull(truthEventStream);
        ArgumentNullException.ThrowIfNull(request);

        var truthEvents = truthEventStream
            .OrderBy(payment => payment.LogicalSequence)
            .ThenBy(payment => payment.EventId, StringComparer.Ordinal)
            .ToArray();

        var selectedEvent = truthEvents.Single(payment => payment.EventId == request.SourceEventId);

        var deliveredEvents = truthEvents
            .Select(payment => new DeliveredPaymentCaptured(payment, payment.LogicalSequence, deliveryAttempt: 1))
            .Append(new DeliveredPaymentCaptured(selectedEvent, request.DuplicateDeliverySequence, deliveryAttempt: 2))
            .OrderBy(delivery => delivery.DeliverySequence)
            .ThenBy(delivery => delivery.SourceEventId, StringComparer.Ordinal)
            .ThenBy(delivery => delivery.DeliveryAttempt)
            .ToArray();

        var manifest = new FaultManifest(
            [
                new FaultManifestEntry(
                    request.FaultId,
                    FaultKind.DuplicateDelivery,
                    request.SourceEventId,
                    request.DuplicateDeliverySequence)
            ]);

        return new DuplicatePaymentFaultInjectionResult(deliveredEvents, manifest);
    }
}

public sealed record DuplicatePaymentFaultRequest(
    string FaultId,
    string SourceEventId,
    long DuplicateDeliverySequence);

public sealed record DuplicatePaymentFaultInjectionResult(
    IReadOnlyList<DeliveredPaymentCaptured> DeliveredEventStream,
    FaultManifest FaultManifest);

public sealed record FaultManifest(IReadOnlyList<FaultManifestEntry> Entries);

public sealed record FaultManifestEntry(
    string FaultId,
    FaultKind Kind,
    string SourceEventId,
    long DeliverySequence);

public enum FaultKind
{
    DuplicateDelivery
}

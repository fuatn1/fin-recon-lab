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

        var duplicateSourceEventId = truthEvents
            .GroupBy(payment => payment.EventId, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1)
            ?.Key;

        if (duplicateSourceEventId is not null)
        {
            throw new InvalidOperationException(
                $"Truth event stream contains duplicate source event id '{duplicateSourceEventId}'.");
        }

        var selectedEvent = truthEvents.FirstOrDefault(payment => payment.EventId == request.SourceEventId)
            ?? throw new InvalidOperationException(
                $"Source event id '{request.SourceEventId}' was not found in the truth event stream.");

        if (request.DuplicateDeliverySequence <= selectedEvent.LogicalSequence)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                "Duplicate delivery sequence must be after the selected source event baseline delivery sequence.");
        }

        if (truthEvents.Any(payment => payment.LogicalSequence == request.DuplicateDeliverySequence))
        {
            throw new InvalidOperationException(
                $"Duplicate delivery sequence '{request.DuplicateDeliverySequence}' collides with an existing baseline delivery sequence.");
        }

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

public sealed record DuplicatePaymentFaultRequest
{
    public DuplicatePaymentFaultRequest(string faultId, string sourceEventId, long duplicateDeliverySequence)
    {
        if (string.IsNullOrWhiteSpace(faultId))
        {
            throw new ArgumentException("Fault id is required.", nameof(faultId));
        }

        if (string.IsNullOrWhiteSpace(sourceEventId))
        {
            throw new ArgumentException("Source event id is required.", nameof(sourceEventId));
        }

        if (duplicateDeliverySequence < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(duplicateDeliverySequence),
                "Duplicate delivery sequence cannot be negative.");
        }

        FaultId = faultId;
        SourceEventId = sourceEventId;
        DuplicateDeliverySequence = duplicateDeliverySequence;
    }

    public string FaultId { get; }

    public string SourceEventId { get; }

    public long DuplicateDeliverySequence { get; }
}

public sealed record DuplicatePaymentFaultInjectionResult
{
    public DuplicatePaymentFaultInjectionResult(
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

public sealed record FaultManifest
{
    public FaultManifest(IEnumerable<FaultManifestEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        Entries = Array.AsReadOnly(entries.ToArray());
    }

    public IReadOnlyList<FaultManifestEntry> Entries { get; }
}

public sealed record FaultManifestEntry
{
    public FaultManifestEntry(string faultId, FaultKind kind, string sourceEventId, long deliverySequence)
    {
        if (string.IsNullOrWhiteSpace(faultId))
        {
            throw new ArgumentException("Fault id is required.", nameof(faultId));
        }

        if (string.IsNullOrWhiteSpace(sourceEventId))
        {
            throw new ArgumentException("Source event id is required.", nameof(sourceEventId));
        }

        if (deliverySequence < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(deliverySequence), "Delivery sequence cannot be negative.");
        }

        FaultId = faultId;
        Kind = kind;
        SourceEventId = sourceEventId;
        DeliverySequence = deliverySequence;
    }

    public string FaultId { get; }

    public FaultKind Kind { get; }

    public string SourceEventId { get; }

    public long DeliverySequence { get; }
}

public enum FaultKind
{
    DuplicateDelivery
}

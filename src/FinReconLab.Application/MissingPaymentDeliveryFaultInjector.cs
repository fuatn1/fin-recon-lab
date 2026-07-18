using FinReconLab.Domain;

namespace FinReconLab.Application;

public sealed class MissingPaymentDeliveryFaultInjector
{
    public MissingPaymentFaultInjectionResult Inject(
        IEnumerable<PaymentCaptured> truthEventStream,
        MissingPaymentFaultRequest request)
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

        if (!truthEvents.Any(payment => StringComparer.Ordinal.Equals(payment.EventId, request.SourceEventId)))
        {
            throw new InvalidOperationException(
                $"Source event id '{request.SourceEventId}' was not found in the truth event stream.");
        }

        var deliveredEvents = truthEvents
            .Where(payment => !StringComparer.Ordinal.Equals(payment.EventId, request.SourceEventId))
            .Select(payment => new DeliveredPaymentCaptured(payment, payment.LogicalSequence, deliveryAttempt: 1))
            .OrderBy(delivery => delivery.DeliverySequence)
            .ThenBy(delivery => delivery.SourceEventId, StringComparer.Ordinal)
            .ThenBy(delivery => delivery.DeliveryAttempt)
            .ToArray();

        var manifest = new FaultManifest(
            [
                new MissingDeliveryFaultManifestEntry(
                    request.FaultId,
                    request.SourceEventId)
            ]);

        return new MissingPaymentFaultInjectionResult(deliveredEvents, manifest);
    }
}

public sealed record MissingPaymentFaultRequest
{
    public MissingPaymentFaultRequest(string faultId, string sourceEventId)
    {
        if (string.IsNullOrWhiteSpace(faultId))
        {
            throw new ArgumentException("Fault id is required.", nameof(faultId));
        }

        if (string.IsNullOrWhiteSpace(sourceEventId))
        {
            throw new ArgumentException("Source event id is required.", nameof(sourceEventId));
        }

        FaultId = faultId;
        SourceEventId = sourceEventId;
    }

    public string FaultId { get; }

    public string SourceEventId { get; }
}

public sealed record MissingPaymentFaultInjectionResult
{
    public MissingPaymentFaultInjectionResult(
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

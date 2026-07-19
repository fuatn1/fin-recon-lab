using FinReconLab.Domain;

namespace FinReconLab.Application;

public sealed class InconsistentPaymentAmountDeliveryFaultInjector
{
    public InconsistentPaymentAmountFaultInjectionResult Inject(
        IEnumerable<PaymentCaptured> truthEventStream,
        InconsistentPaymentAmountFaultRequest request)
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

        if (!StringComparer.Ordinal.Equals(
                selectedEvent.CapturedAmount.Currency,
                request.DeliveredCapturedAmount.Currency))
        {
            throw new InvalidOperationException(
                "Delivered captured amount must use the selected source event currency.");
        }

        if (selectedEvent.CapturedAmount == request.DeliveredCapturedAmount)
        {
            throw new ArgumentException(
                "Delivered captured amount must differ from the selected source event amount.",
                nameof(request));
        }

        var deliveredEvents = truthEvents
            .Select(payment => StringComparer.Ordinal.Equals(payment.EventId, request.SourceEventId)
                ? new DeliveredPaymentCaptured(
                    payment,
                    request.DeliveredCapturedAmount,
                    payment.LogicalSequence,
                    deliveryAttempt: 1)
                : new DeliveredPaymentCaptured(payment, payment.LogicalSequence, deliveryAttempt: 1))
            .OrderBy(delivery => delivery.DeliverySequence)
            .ThenBy(delivery => delivery.SourceEventId, StringComparer.Ordinal)
            .ThenBy(delivery => delivery.DeliveryAttempt)
            .ToArray();

        var manifest = new FaultManifest(
            [
                new InconsistentAmountDeliveryFaultManifestEntry(
                    request.FaultId,
                    request.SourceEventId,
                    selectedEvent.CapturedAmount,
                    request.DeliveredCapturedAmount)
            ]);

        return new InconsistentPaymentAmountFaultInjectionResult(deliveredEvents, manifest);
    }
}

public sealed record InconsistentPaymentAmountFaultRequest
{
    public InconsistentPaymentAmountFaultRequest(
        string faultId,
        string sourceEventId,
        Money deliveredCapturedAmount)
    {
        if (string.IsNullOrWhiteSpace(faultId))
        {
            throw new ArgumentException("Fault id is required.", nameof(faultId));
        }

        if (string.IsNullOrWhiteSpace(sourceEventId))
        {
            throw new ArgumentException("Source event id is required.", nameof(sourceEventId));
        }

        ArgumentNullException.ThrowIfNull(deliveredCapturedAmount);

        FaultId = faultId;
        SourceEventId = sourceEventId;
        DeliveredCapturedAmount = deliveredCapturedAmount;
    }

    public string FaultId { get; }

    public string SourceEventId { get; }

    public Money DeliveredCapturedAmount { get; }
}

public sealed record InconsistentPaymentAmountFaultInjectionResult
{
    public InconsistentPaymentAmountFaultInjectionResult(
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

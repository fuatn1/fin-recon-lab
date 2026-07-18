namespace FinReconLab.Domain;

public sealed record DeliveredPaymentCaptured
{
    public DeliveredPaymentCaptured(PaymentCaptured sourceEvent, long deliverySequence, int deliveryAttempt)
    {
        ArgumentNullException.ThrowIfNull(sourceEvent);

        if (deliverySequence < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(deliverySequence), "Delivery sequence cannot be negative.");
        }

        if (deliveryAttempt < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(deliveryAttempt), "Delivery attempt must be positive.");
        }

        SourceEvent = sourceEvent;
        DeliverySequence = deliverySequence;
        DeliveryAttempt = deliveryAttempt;
    }

    public PaymentCaptured SourceEvent { get; }

    public string SourceEventId => SourceEvent.EventId;

    public long DeliverySequence { get; }

    public int DeliveryAttempt { get; }
}

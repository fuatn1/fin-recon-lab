namespace FinReconLab.Domain;

public sealed record DeliveredPaymentCaptured
{
    public DeliveredPaymentCaptured(PaymentCaptured sourceEvent, long deliverySequence, int deliveryAttempt)
        : this(sourceEvent, GetSourceCapturedAmount(sourceEvent), deliverySequence, deliveryAttempt)
    {
    }

    public DeliveredPaymentCaptured(
        PaymentCaptured sourceEvent,
        Money deliveredCapturedAmount,
        long deliverySequence,
        int deliveryAttempt)
    {
        ArgumentNullException.ThrowIfNull(sourceEvent);
        ArgumentNullException.ThrowIfNull(deliveredCapturedAmount);

        if (deliverySequence < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(deliverySequence), "Delivery sequence cannot be negative.");
        }

        if (deliveryAttempt < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(deliveryAttempt), "Delivery attempt must be positive.");
        }

        SourceEvent = sourceEvent;
        DeliveredCapturedAmount = deliveredCapturedAmount;
        DeliverySequence = deliverySequence;
        DeliveryAttempt = deliveryAttempt;
    }

    public PaymentCaptured SourceEvent { get; }

    public string SourceEventId => SourceEvent.EventId;

    public Money DeliveredCapturedAmount { get; }

    public long DeliverySequence { get; }

    public int DeliveryAttempt { get; }

    private static Money GetSourceCapturedAmount(PaymentCaptured? sourceEvent)
    {
        ArgumentNullException.ThrowIfNull(sourceEvent);
        return sourceEvent.CapturedAmount;
    }
}

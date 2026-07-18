namespace FinReconLab.Domain;

public sealed record PaymentCaptured
{
    public PaymentCaptured(
        string eventId,
        string orderId,
        Money capturedAmount,
        long logicalSequence,
        DateTimeOffset occurredAt)
    {
        if (string.IsNullOrWhiteSpace(eventId))
        {
            throw new ArgumentException("Event id is required.", nameof(eventId));
        }

        if (string.IsNullOrWhiteSpace(orderId))
        {
            throw new ArgumentException("Order id is required.", nameof(orderId));
        }

        if (logicalSequence < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(logicalSequence), "Logical sequence cannot be negative.");
        }

        EventId = eventId;
        OrderId = orderId;
        CapturedAmount = capturedAmount;
        LogicalSequence = logicalSequence;
        OccurredAt = occurredAt;
    }

    public string EventId { get; }

    public string OrderId { get; }

    public Money CapturedAmount { get; }

    public long LogicalSequence { get; }

    public DateTimeOffset OccurredAt { get; }
}

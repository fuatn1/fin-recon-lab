namespace FinReconLab.Domain;

public sealed record ExpectedPaymentContribution
{
    public ExpectedPaymentContribution(
        string sourceEventId,
        long sourceLogicalSequence,
        Money appliedCapturedAmount)
    {
        if (string.IsNullOrWhiteSpace(sourceEventId))
        {
            throw new ArgumentException("Source event id is required.", nameof(sourceEventId));
        }

        if (sourceLogicalSequence < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sourceLogicalSequence),
                "Source logical sequence cannot be negative.");
        }

        ArgumentNullException.ThrowIfNull(appliedCapturedAmount);

        SourceEventId = sourceEventId;
        SourceLogicalSequence = sourceLogicalSequence;
        AppliedCapturedAmount = appliedCapturedAmount;
    }

    public string SourceEventId { get; }

    public long SourceLogicalSequence { get; }

    public Money AppliedCapturedAmount { get; }
}

public sealed record ObservedPaymentContribution
{
    public ObservedPaymentContribution(
        string sourceEventId,
        long sourceLogicalSequence,
        long deliverySequence,
        int deliveryAttempt,
        Money appliedDeliveredCapturedAmount)
    {
        if (string.IsNullOrWhiteSpace(sourceEventId))
        {
            throw new ArgumentException("Source event id is required.", nameof(sourceEventId));
        }

        if (sourceLogicalSequence < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sourceLogicalSequence),
                "Source logical sequence cannot be negative.");
        }

        if (deliverySequence < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(deliverySequence),
                "Delivery sequence cannot be negative.");
        }

        if (deliveryAttempt < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(deliveryAttempt),
                "Delivery attempt must be positive.");
        }

        ArgumentNullException.ThrowIfNull(appliedDeliveredCapturedAmount);

        SourceEventId = sourceEventId;
        SourceLogicalSequence = sourceLogicalSequence;
        DeliverySequence = deliverySequence;
        DeliveryAttempt = deliveryAttempt;
        AppliedDeliveredCapturedAmount = appliedDeliveredCapturedAmount;
    }

    public string SourceEventId { get; }

    public long SourceLogicalSequence { get; }

    public long DeliverySequence { get; }

    public int DeliveryAttempt { get; }

    public Money AppliedDeliveredCapturedAmount { get; }
}

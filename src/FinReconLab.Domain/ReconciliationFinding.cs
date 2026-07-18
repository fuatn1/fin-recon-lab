namespace FinReconLab.Domain;

public enum ReconciliationFindingCategory
{
    CapturedAmountMismatch
}

public sealed record ReconciliationFinding
{
    public ReconciliationFinding(
        ReconciliationFindingCategory category,
        string orderId,
        string currency,
        ReconciliationCutoff cutoff,
        decimal expectedAmount,
        decimal observedAmount,
        decimal signedDelta)
    {
        if (string.IsNullOrWhiteSpace(orderId))
        {
            throw new ArgumentException("Order id is required.", nameof(orderId));
        }

        if (string.IsNullOrWhiteSpace(currency))
        {
            throw new ArgumentException("Currency is required.", nameof(currency));
        }

        Category = category;
        OrderId = orderId;
        Currency = currency;
        Cutoff = cutoff;
        ExpectedAmount = expectedAmount;
        ObservedAmount = observedAmount;
        SignedDelta = signedDelta;
    }

    public ReconciliationFindingCategory Category { get; }

    public string OrderId { get; }

    public string Currency { get; }

    public ReconciliationCutoff Cutoff { get; }

    public decimal ExpectedAmount { get; }

    public decimal ObservedAmount { get; }

    public decimal SignedDelta { get; }
}

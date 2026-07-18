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
        ReconciliationCutoff cutoff,
        Money expectedAmount,
        Money observedAmount,
        Money signedDelta)
    {
        ArgumentNullException.ThrowIfNull(expectedAmount);
        ArgumentNullException.ThrowIfNull(observedAmount);
        ArgumentNullException.ThrowIfNull(signedDelta);

        if (string.IsNullOrWhiteSpace(orderId))
        {
            throw new ArgumentException("Order id is required.", nameof(orderId));
        }

        if (!StringComparer.Ordinal.Equals(expectedAmount.Currency, observedAmount.Currency)
            || !StringComparer.Ordinal.Equals(expectedAmount.Currency, signedDelta.Currency))
        {
            throw new InvalidOperationException(
                "Expected amount, observed amount, and signed delta must use the same currency.");
        }

        Category = category;
        OrderId = orderId;
        Cutoff = cutoff;
        ExpectedAmount = expectedAmount;
        ObservedAmount = observedAmount;
        SignedDelta = signedDelta;
    }

    public ReconciliationFindingCategory Category { get; }

    public string OrderId { get; }

    public ReconciliationCutoff Cutoff { get; }

    public Money ExpectedAmount { get; }

    public Money ObservedAmount { get; }

    public Money SignedDelta { get; }
}

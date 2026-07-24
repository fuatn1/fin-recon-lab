namespace FinReconLab.Domain;

public enum ReconciliationFindingCategory
{
    CapturedAmountMismatch
}

public sealed record ReconciliationFinding
{
    public ReconciliationFinding(
        ReconciliationFindingCategory category,
        ExpectedPaymentSnapshot expected,
        ObservedPaymentSnapshot observed,
        Money signedDelta)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(observed);
        ArgumentNullException.ThrowIfNull(signedDelta);

        if (!StringComparer.Ordinal.Equals(expected.OrderId, observed.OrderId))
        {
            throw new InvalidOperationException(
                "Expected and observed payment snapshots must have matching order ids.");
        }

        if (!StringComparer.Ordinal.Equals(expected.CapturedAmount.Currency, observed.CapturedAmount.Currency)
            || !StringComparer.Ordinal.Equals(expected.CapturedAmount.Currency, signedDelta.Currency))
        {
            throw new InvalidOperationException(
                "Expected amount, observed amount, and signed delta must use the same currency.");
        }

        if (expected.Cutoff != observed.Cutoff)
        {
            throw new InvalidOperationException(
                "Expected and observed payment snapshots must have matching cutoffs.");
        }

        if (signedDelta != observed.CapturedAmount - expected.CapturedAmount)
        {
            throw new InvalidOperationException(
                "Signed delta must equal observed amount minus expected amount.");
        }

        Category = category;
        OrderId = expected.OrderId;
        Cutoff = expected.Cutoff;
        ExpectedAmount = expected.CapturedAmount;
        ObservedAmount = observed.CapturedAmount;
        SignedDelta = signedDelta;
        ExpectedContributions = new ValueReadOnlyList<ExpectedPaymentContribution>(expected.Contributions);
        ObservedContributions = new ValueReadOnlyList<ObservedPaymentContribution>(observed.Contributions);
    }

    public ReconciliationFindingCategory Category { get; }

    public string OrderId { get; }

    public ReconciliationCutoff Cutoff { get; }

    public Money ExpectedAmount { get; }

    public Money ObservedAmount { get; }

    public Money SignedDelta { get; }

    public IReadOnlyList<ExpectedPaymentContribution> ExpectedContributions { get; }

    public IReadOnlyList<ObservedPaymentContribution> ObservedContributions { get; }
}

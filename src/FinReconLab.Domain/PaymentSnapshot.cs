namespace FinReconLab.Domain;

public abstract record PaymentSnapshot
{
    protected PaymentSnapshot(string orderId, Money capturedAmount, ReconciliationCutoff cutoff)
    {
        ArgumentNullException.ThrowIfNull(capturedAmount);

        if (string.IsNullOrWhiteSpace(orderId))
        {
            throw new ArgumentException("Order id is required.", nameof(orderId));
        }

        OrderId = orderId;
        CapturedAmount = capturedAmount;
        Cutoff = cutoff;
    }

    public string OrderId { get; }

    public Money CapturedAmount { get; }

    public ReconciliationCutoff Cutoff { get; }
}

public sealed record ExpectedPaymentSnapshot : PaymentSnapshot
{
    public ExpectedPaymentSnapshot(
        string orderId,
        Money capturedAmount,
        ReconciliationCutoff cutoff,
        IEnumerable<ExpectedPaymentContribution> contributions)
        : base(orderId, capturedAmount, cutoff)
    {
        ArgumentNullException.ThrowIfNull(contributions);

        var suppliedContributions = contributions.ToArray();
        if (suppliedContributions.Any(static contribution => contribution is null))
        {
            throw new ArgumentException(
                "Expected contributions cannot contain null entries.",
                nameof(contributions));
        }

        var contributionSnapshot = suppliedContributions
            .OrderBy(contribution => contribution.SourceLogicalSequence)
            .ThenBy(contribution => contribution.SourceEventId, StringComparer.Ordinal)
            .ToArray();

        if (contributionSnapshot.Any(
                contribution => !cutoff.Includes(contribution.SourceLogicalSequence)))
        {
            throw new InvalidOperationException(
                "Expected contributions must be included by the snapshot cutoff.");
        }

        EnsureExpectedTotal(capturedAmount, contributionSnapshot);
        Contributions = new ValueReadOnlyList<ExpectedPaymentContribution>(contributionSnapshot);
    }

    public IReadOnlyList<ExpectedPaymentContribution> Contributions { get; }

    private static void EnsureExpectedTotal(
        Money capturedAmount,
        IReadOnlyList<ExpectedPaymentContribution> contributions)
    {
        var contributionTotal = Money.Zero(capturedAmount.Currency);

        foreach (var contribution in contributions)
        {
            if (!StringComparer.Ordinal.Equals(
                    capturedAmount.Currency,
                    contribution.AppliedCapturedAmount.Currency))
            {
                throw new InvalidOperationException(
                    "Every expected contribution must use the snapshot currency.");
            }

            contributionTotal += contribution.AppliedCapturedAmount;
        }

        if (contributionTotal != capturedAmount)
        {
            throw new InvalidOperationException(
                "Expected contribution total must equal the snapshot captured amount.");
        }
    }
}

public sealed record ObservedPaymentSnapshot : PaymentSnapshot
{
    public ObservedPaymentSnapshot(
        string orderId,
        Money capturedAmount,
        ReconciliationCutoff cutoff,
        IEnumerable<ObservedPaymentContribution> contributions)
        : base(orderId, capturedAmount, cutoff)
    {
        ArgumentNullException.ThrowIfNull(contributions);

        var suppliedContributions = contributions.ToArray();
        if (suppliedContributions.Any(static contribution => contribution is null))
        {
            throw new ArgumentException(
                "Observed contributions cannot contain null entries.",
                nameof(contributions));
        }

        var contributionSnapshot = suppliedContributions
            .OrderBy(contribution => contribution.DeliverySequence)
            .ThenBy(contribution => contribution.SourceEventId, StringComparer.Ordinal)
            .ThenBy(contribution => contribution.DeliveryAttempt)
            .ToArray();

        if (contributionSnapshot.Any(
                contribution => !cutoff.Includes(contribution.DeliverySequence)))
        {
            throw new InvalidOperationException(
                "Observed contributions must be included by the snapshot cutoff.");
        }

        EnsureObservedTotal(capturedAmount, contributionSnapshot);
        Contributions = new ValueReadOnlyList<ObservedPaymentContribution>(contributionSnapshot);
    }

    public IReadOnlyList<ObservedPaymentContribution> Contributions { get; }

    private static void EnsureObservedTotal(
        Money capturedAmount,
        IReadOnlyList<ObservedPaymentContribution> contributions)
    {
        var contributionTotal = Money.Zero(capturedAmount.Currency);

        foreach (var contribution in contributions)
        {
            if (!StringComparer.Ordinal.Equals(
                    capturedAmount.Currency,
                    contribution.AppliedDeliveredCapturedAmount.Currency))
            {
                throw new InvalidOperationException(
                    "Every observed contribution must use the snapshot currency.");
            }

            contributionTotal += contribution.AppliedDeliveredCapturedAmount;
        }

        if (contributionTotal != capturedAmount)
        {
            throw new InvalidOperationException(
                "Observed contribution total must equal the snapshot captured amount.");
        }
    }
}

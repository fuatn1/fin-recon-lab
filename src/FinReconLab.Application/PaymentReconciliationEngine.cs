using FinReconLab.Domain;

namespace FinReconLab.Application;

public sealed class PaymentReconciliationEngine
{
    public IReadOnlyList<ReconciliationFinding> Reconcile(
        ExpectedPaymentSnapshot expected,
        ObservedPaymentSnapshot observed)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(observed);

        if (!StringComparer.Ordinal.Equals(expected.OrderId, observed.OrderId))
        {
            throw new InvalidOperationException("Expected and observed payment snapshots must have matching order ids.");
        }

        if (!StringComparer.Ordinal.Equals(expected.CapturedAmount.Currency, observed.CapturedAmount.Currency))
        {
            throw new InvalidOperationException("Expected and observed payment snapshots must have matching currencies.");
        }

        if (expected.Cutoff != observed.Cutoff)
        {
            throw new InvalidOperationException("Expected and observed payment snapshots must have matching cutoffs.");
        }

        if (expected.CapturedAmount == observed.CapturedAmount)
        {
            return [];
        }

        var signedDelta = observed.CapturedAmount - expected.CapturedAmount;

        return
        [
            new ReconciliationFinding(
                ReconciliationFindingCategory.CapturedAmountMismatch,
                expected,
                observed,
                signedDelta)
        ];
    }
}

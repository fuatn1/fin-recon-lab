namespace FinReconLab.Domain;

public sealed record PaymentSnapshot
{
    public PaymentSnapshot(string orderId, Money capturedAmount, ReconciliationCutoff cutoff)
    {
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

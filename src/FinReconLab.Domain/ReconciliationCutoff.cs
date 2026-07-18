namespace FinReconLab.Domain;

public readonly record struct ReconciliationCutoff
{
    public ReconciliationCutoff(long deliverySequenceInclusive)
    {
        if (deliverySequenceInclusive < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(deliverySequenceInclusive),
                "Cutoff sequence cannot be negative.");
        }

        DeliverySequenceInclusive = deliverySequenceInclusive;
    }

    public long DeliverySequenceInclusive { get; }

    public bool Includes(long deliverySequence) => deliverySequence <= DeliverySequenceInclusive;
}

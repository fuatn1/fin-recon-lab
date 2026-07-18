namespace FinReconLab.Domain;

public readonly record struct ReconciliationCutoff
{
    public ReconciliationCutoff(long sequenceInclusive)
    {
        if (sequenceInclusive < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sequenceInclusive),
                "Cutoff sequence cannot be negative.");
        }

        SequenceInclusive = sequenceInclusive;
    }

    public long SequenceInclusive { get; }

    public bool Includes(long sequence) => sequence <= SequenceInclusive;
}

using FinReconLab.Domain;

namespace FinReconLab.Application;

public sealed record FaultManifest
{
    public FaultManifest(IEnumerable<FaultManifestEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        Entries = Array.AsReadOnly(entries.ToArray());
    }

    public IReadOnlyList<FaultManifestEntry> Entries { get; }
}

public abstract record FaultManifestEntry
{
    protected FaultManifestEntry(string faultId)
    {
        if (string.IsNullOrWhiteSpace(faultId))
        {
            throw new ArgumentException("Fault id is required.", nameof(faultId));
        }

        FaultId = faultId;
    }

    public string FaultId { get; }

    public abstract FaultKind Kind { get; }
}

public abstract record SingleSourceFaultManifestEntry : FaultManifestEntry
{
    protected SingleSourceFaultManifestEntry(string faultId, string sourceEventId)
        : base(faultId)
    {
        if (string.IsNullOrWhiteSpace(sourceEventId))
        {
            throw new ArgumentException("Source event id is required.", nameof(sourceEventId));
        }

        SourceEventId = sourceEventId;
    }

    public string SourceEventId { get; }
}

public sealed record DuplicateDeliveryFaultManifestEntry : SingleSourceFaultManifestEntry
{
    public DuplicateDeliveryFaultManifestEntry(string faultId, string sourceEventId, long deliverySequence)
        : base(faultId, sourceEventId)
    {
        if (deliverySequence < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(deliverySequence), "Delivery sequence cannot be negative.");
        }

        DeliverySequence = deliverySequence;
    }

    public override FaultKind Kind => FaultKind.DuplicateDelivery;

    public long DeliverySequence { get; }
}

public sealed record MissingDeliveryFaultManifestEntry : SingleSourceFaultManifestEntry
{
    public MissingDeliveryFaultManifestEntry(string faultId, string sourceEventId)
        : base(faultId, sourceEventId)
    {
    }

    public override FaultKind Kind => FaultKind.MissingDelivery;
}

public sealed record DelayedDeliveryFaultManifestEntry : SingleSourceFaultManifestEntry
{
    public DelayedDeliveryFaultManifestEntry(
        string faultId,
        string sourceEventId,
        long originalDeliverySequence,
        long delayedDeliverySequence)
        : base(faultId, sourceEventId)
    {
        if (originalDeliverySequence < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(originalDeliverySequence),
                "Original delivery sequence cannot be negative.");
        }

        if (delayedDeliverySequence <= originalDeliverySequence)
        {
            throw new ArgumentOutOfRangeException(
                nameof(delayedDeliverySequence),
                "Delayed delivery sequence must be after the original delivery sequence.");
        }

        OriginalDeliverySequence = originalDeliverySequence;
        DelayedDeliverySequence = delayedDeliverySequence;
    }

    public override FaultKind Kind => FaultKind.DelayedDelivery;

    public long OriginalDeliverySequence { get; }

    public long DelayedDeliverySequence { get; }
}

public sealed record OutOfOrderDeliveryFaultManifestEntry : FaultManifestEntry
{
    public OutOfOrderDeliveryFaultManifestEntry(
        string faultId,
        string earlierSourceEventId,
        string laterSourceEventId,
        long earlierOriginalDeliverySequence,
        long earlierDeliveredSequence,
        long laterOriginalDeliverySequence,
        long laterDeliveredSequence)
        : base(faultId)
    {
        if (string.IsNullOrWhiteSpace(earlierSourceEventId))
        {
            throw new ArgumentException("Earlier source event id is required.", nameof(earlierSourceEventId));
        }

        if (string.IsNullOrWhiteSpace(laterSourceEventId))
        {
            throw new ArgumentException("Later source event id is required.", nameof(laterSourceEventId));
        }

        if (StringComparer.Ordinal.Equals(earlierSourceEventId, laterSourceEventId))
        {
            throw new ArgumentException(
                "Earlier and later source event ids must be different.",
                nameof(laterSourceEventId));
        }

        if (earlierOriginalDeliverySequence < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(earlierOriginalDeliverySequence),
                "Earlier original delivery sequence cannot be negative.");
        }

        if (earlierDeliveredSequence < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(earlierDeliveredSequence),
                "Earlier delivered sequence cannot be negative.");
        }

        if (laterOriginalDeliverySequence < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(laterOriginalDeliverySequence),
                "Later original delivery sequence cannot be negative.");
        }

        if (laterDeliveredSequence < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(laterDeliveredSequence),
                "Later delivered sequence cannot be negative.");
        }

        if (earlierOriginalDeliverySequence >= laterOriginalDeliverySequence)
        {
            throw new ArgumentOutOfRangeException(
                nameof(earlierOriginalDeliverySequence),
                "Earlier original delivery sequence must be before the later original delivery sequence.");
        }

        if (earlierDeliveredSequence != laterOriginalDeliverySequence ||
            laterDeliveredSequence != earlierOriginalDeliverySequence)
        {
            throw new ArgumentException(
                "Delivered sequences must represent an exact pairwise swap of the original delivery sequences.",
                nameof(earlierDeliveredSequence));
        }

        EarlierSourceEventId = earlierSourceEventId;
        LaterSourceEventId = laterSourceEventId;
        EarlierOriginalDeliverySequence = earlierOriginalDeliverySequence;
        EarlierDeliveredSequence = earlierDeliveredSequence;
        LaterOriginalDeliverySequence = laterOriginalDeliverySequence;
        LaterDeliveredSequence = laterDeliveredSequence;
    }

    public override FaultKind Kind => FaultKind.OutOfOrderDelivery;

    public string EarlierSourceEventId { get; }

    public string LaterSourceEventId { get; }

    public long EarlierOriginalDeliverySequence { get; }

    public long EarlierDeliveredSequence { get; }

    public long LaterOriginalDeliverySequence { get; }

    public long LaterDeliveredSequence { get; }
}

public sealed record InconsistentAmountDeliveryFaultManifestEntry : SingleSourceFaultManifestEntry
{
    public InconsistentAmountDeliveryFaultManifestEntry(
        string faultId,
        string sourceEventId,
        Money originalCapturedAmount,
        Money deliveredCapturedAmount)
        : base(faultId, sourceEventId)
    {
        ArgumentNullException.ThrowIfNull(originalCapturedAmount);
        ArgumentNullException.ThrowIfNull(deliveredCapturedAmount);

        if (!StringComparer.Ordinal.Equals(originalCapturedAmount.Currency, deliveredCapturedAmount.Currency))
        {
            throw new InvalidOperationException(
                "Original and delivered captured amounts must use the same currency.");
        }

        if (originalCapturedAmount == deliveredCapturedAmount)
        {
            throw new ArgumentException(
                "Delivered captured amount must differ from the original captured amount.",
                nameof(deliveredCapturedAmount));
        }

        OriginalCapturedAmount = originalCapturedAmount;
        DeliveredCapturedAmount = deliveredCapturedAmount;
    }

    public override FaultKind Kind => FaultKind.InconsistentAmountDelivery;

    public Money OriginalCapturedAmount { get; }

    public Money DeliveredCapturedAmount { get; }
}

public enum FaultKind
{
    DuplicateDelivery,
    MissingDelivery,
    DelayedDelivery,
    OutOfOrderDelivery,
    InconsistentAmountDelivery
}

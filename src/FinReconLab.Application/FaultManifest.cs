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
    protected FaultManifestEntry(string faultId, string sourceEventId)
    {
        if (string.IsNullOrWhiteSpace(faultId))
        {
            throw new ArgumentException("Fault id is required.", nameof(faultId));
        }

        if (string.IsNullOrWhiteSpace(sourceEventId))
        {
            throw new ArgumentException("Source event id is required.", nameof(sourceEventId));
        }

        FaultId = faultId;
        SourceEventId = sourceEventId;
    }

    public string FaultId { get; }

    public string SourceEventId { get; }

    public abstract FaultKind Kind { get; }
}

public sealed record DuplicateDeliveryFaultManifestEntry : FaultManifestEntry
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

public sealed record MissingDeliveryFaultManifestEntry : FaultManifestEntry
{
    public MissingDeliveryFaultManifestEntry(string faultId, string sourceEventId)
        : base(faultId, sourceEventId)
    {
    }

    public override FaultKind Kind => FaultKind.MissingDelivery;
}

public enum FaultKind
{
    DuplicateDelivery,
    MissingDelivery
}

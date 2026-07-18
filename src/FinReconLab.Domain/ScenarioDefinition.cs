namespace FinReconLab.Domain;

public sealed record ScenarioDefinition
{
    public const string SupportedSchemaVersion = "payment-captured.v1";

    public const int MaxPaymentCount = 10_000;

    public ScenarioDefinition(
        string schemaVersion,
        string scenarioId,
        ulong seed,
        int paymentCount,
        Money paymentAmount,
        DateTimeOffset startingOccurredAt,
        TimeSpan eventInterval)
    {
        ArgumentNullException.ThrowIfNull(paymentAmount);

        if (!StringComparer.Ordinal.Equals(schemaVersion, SupportedSchemaVersion))
        {
            throw new ArgumentException(
                $"Unsupported scenario schema version '{schemaVersion}'. Supported version is '{SupportedSchemaVersion}'.",
                nameof(schemaVersion));
        }

        if (string.IsNullOrWhiteSpace(scenarioId))
        {
            throw new ArgumentException("Scenario id is required.", nameof(scenarioId));
        }

        if (paymentCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(paymentCount), "Payment count must be positive.");
        }

        if (paymentCount > MaxPaymentCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(paymentCount),
                $"Payment count cannot exceed {MaxPaymentCount} for scenario schema version '{SupportedSchemaVersion}'.");
        }

        if (eventInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(eventInterval), "Event interval must be positive.");
        }

        ValidateGeneratedTimestampRange(startingOccurredAt, eventInterval, paymentCount);

        SchemaVersion = schemaVersion;
        ScenarioId = scenarioId;
        Seed = seed;
        PaymentCount = paymentCount;
        PaymentAmount = paymentAmount;
        StartingOccurredAt = startingOccurredAt;
        EventInterval = eventInterval;
    }

    public string SchemaVersion { get; }

    public string ScenarioId { get; }

    public ulong Seed { get; }

    public int PaymentCount { get; }

    public Money PaymentAmount { get; }

    public DateTimeOffset StartingOccurredAt { get; }

    public TimeSpan EventInterval { get; }

    private static void ValidateGeneratedTimestampRange(
        DateTimeOffset startingOccurredAt,
        TimeSpan eventInterval,
        int paymentCount)
    {
        try
        {
            var finalOffsetTicks = checked(eventInterval.Ticks * (paymentCount - 1));
            _ = startingOccurredAt + TimeSpan.FromTicks(finalOffsetTicks);
        }
        catch (ArgumentOutOfRangeException)
        {
            throw new ArgumentOutOfRangeException(
                nameof(startingOccurredAt),
                "The final generated event timestamp must be representable by DateTimeOffset.");
        }
        catch (OverflowException)
        {
            throw new ArgumentOutOfRangeException(
                nameof(eventInterval),
                "The generated event timestamp range exceeds supported DateTimeOffset arithmetic.");
        }
    }
}

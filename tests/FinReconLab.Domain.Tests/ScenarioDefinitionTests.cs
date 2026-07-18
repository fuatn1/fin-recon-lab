using FinReconLab.Domain;

namespace FinReconLab.Domain.Tests;

public sealed class ScenarioDefinitionTests
{
    [Fact]
    public void Constructor_rejects_unsupported_schema_version()
    {
        Assert.Throws<ArgumentException>(
            () => CreateDefinition(schemaVersion: "payment-captured.v2"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_rejects_blank_scenario_id(string scenarioId)
    {
        Assert.Throws<ArgumentException>(() => CreateDefinition(scenarioId: scenarioId));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_rejects_non_positive_payment_count(int paymentCount)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateDefinition(paymentCount: paymentCount));
    }

    [Fact]
    public void Constructor_rejects_payment_count_above_documented_upper_bound()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CreateDefinition(paymentCount: ScenarioDefinition.MaxPaymentCount + 1));
    }

    [Fact]
    public void Constructor_rejects_null_payment_amount()
    {
        Assert.Throws<ArgumentNullException>(
            () => new ScenarioDefinition(
                ScenarioDefinition.SupportedSchemaVersion,
                "scenario-alpha",
                42,
                1,
                null!,
                DateTimeOffset.UnixEpoch,
                TimeSpan.FromMinutes(1)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_rejects_non_positive_event_interval(long ticks)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CreateDefinition(eventInterval: TimeSpan.FromTicks(ticks)));
    }

    [Fact]
    public void Constructor_rejects_definition_when_final_timestamp_exceeds_max_value()
    {
        var startingTimestamp = DateTimeOffset.MaxValue.AddTicks(-1);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => CreateDefinition(
                paymentCount: 2,
                startingOccurredAt: startingTimestamp,
                eventInterval: TimeSpan.FromTicks(2)));
    }

    [Fact]
    public void Constructor_rejects_checked_interval_multiplication_overflow()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => CreateDefinition(
                paymentCount: 3,
                eventInterval: TimeSpan.MaxValue));

        Assert.Equal("eventInterval", exception.ParamName);
    }

    [Fact]
    public void Constructor_accepts_definition_when_final_timestamp_is_exactly_max_value()
    {
        var startingTimestamp = DateTimeOffset.MaxValue.AddTicks(-1);

        var definition = CreateDefinition(
            paymentCount: 2,
            startingOccurredAt: startingTimestamp,
            eventInterval: TimeSpan.FromTicks(1));

        Assert.Equal(startingTimestamp, definition.StartingOccurredAt);
        Assert.Equal(TimeSpan.FromTicks(1), definition.EventInterval);
    }

    private static ScenarioDefinition CreateDefinition(
        string schemaVersion = ScenarioDefinition.SupportedSchemaVersion,
        string scenarioId = "scenario-alpha",
        ulong seed = 42,
        int paymentCount = 1,
        Money? paymentAmount = null,
        DateTimeOffset? startingOccurredAt = null,
        TimeSpan? eventInterval = null)
    {
        return new ScenarioDefinition(
            schemaVersion,
            scenarioId,
            seed,
            paymentCount,
            paymentAmount ?? new Money(100m, "USD"),
            startingOccurredAt ?? DateTimeOffset.UnixEpoch,
            eventInterval ?? TimeSpan.FromMinutes(1));
    }
}

using System.Buffers;
using System.Globalization;
using System.Text.Json;
using FinReconLab.Domain;

namespace FinReconLab.Application;

public sealed class ReconciliationReportJsonSerializer
{
    public byte[] Serialize(ReconciliationReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        var buffer = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(buffer);

        writer.WriteStartObject();
        writer.WriteString("schemaVersion", report.SchemaVersion);
        WriteScenario(writer, report.Scenario);
        WriteCutoff(writer, "cutoff", report.Cutoff);

        writer.WriteStartArray("expectedSnapshots");
        foreach (var snapshot in report.ExpectedSnapshots)
        {
            WriteExpectedSnapshot(writer, snapshot);
        }

        writer.WriteEndArray();

        writer.WriteStartArray("observedSnapshots");
        foreach (var snapshot in report.ObservedSnapshots)
        {
            WriteObservedSnapshot(writer, snapshot);
        }

        writer.WriteEndArray();

        writer.WriteStartArray("findings");
        foreach (var finding in report.Findings)
        {
            WriteFinding(writer, finding);
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
        writer.Flush();

        return buffer.WrittenSpan.ToArray();
    }

    private static void WriteScenario(Utf8JsonWriter writer, ScenarioDefinition scenario)
    {
        writer.WriteStartObject("scenario");
        writer.WriteString("schemaVersion", scenario.SchemaVersion);
        writer.WriteString("scenarioId", scenario.ScenarioId);
        writer.WriteNumber("seed", scenario.Seed);
        writer.WriteNumber("paymentCount", scenario.PaymentCount);
        WriteMoney(writer, "paymentAmount", scenario.PaymentAmount);
        writer.WriteString(
            "startingOccurredAt",
            scenario.StartingOccurredAt.ToString("O", CultureInfo.InvariantCulture));
        writer.WriteNumber("eventIntervalTicks", scenario.EventInterval.Ticks);
        writer.WriteEndObject();
    }

    private static void WriteExpectedSnapshot(
        Utf8JsonWriter writer,
        ExpectedPaymentSnapshot snapshot)
    {
        writer.WriteStartObject();
        writer.WriteString("orderId", snapshot.OrderId);
        WriteMoney(writer, "capturedAmount", snapshot.CapturedAmount);
        WriteCutoff(writer, "cutoff", snapshot.Cutoff);
        writer.WriteStartArray("contributions");

        foreach (var contribution in snapshot.Contributions)
        {
            WriteExpectedContribution(writer, contribution);
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void WriteObservedSnapshot(
        Utf8JsonWriter writer,
        ObservedPaymentSnapshot snapshot)
    {
        writer.WriteStartObject();
        writer.WriteString("orderId", snapshot.OrderId);
        WriteMoney(writer, "capturedAmount", snapshot.CapturedAmount);
        WriteCutoff(writer, "cutoff", snapshot.Cutoff);
        writer.WriteStartArray("contributions");

        foreach (var contribution in snapshot.Contributions)
        {
            WriteObservedContribution(writer, contribution);
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void WriteFinding(Utf8JsonWriter writer, ReconciliationFinding finding)
    {
        writer.WriteStartObject();
        writer.WriteString("category", FormatCategory(finding.Category));
        writer.WriteString("orderId", finding.OrderId);
        WriteCutoff(writer, "cutoff", finding.Cutoff);
        WriteMoney(writer, "expectedAmount", finding.ExpectedAmount);
        WriteMoney(writer, "observedAmount", finding.ObservedAmount);
        WriteMoney(writer, "signedDelta", finding.SignedDelta);

        writer.WriteStartArray("expectedContributions");
        foreach (var contribution in finding.ExpectedContributions)
        {
            WriteExpectedContribution(writer, contribution);
        }

        writer.WriteEndArray();

        writer.WriteStartArray("observedContributions");
        foreach (var contribution in finding.ObservedContributions)
        {
            WriteObservedContribution(writer, contribution);
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void WriteExpectedContribution(
        Utf8JsonWriter writer,
        ExpectedPaymentContribution contribution)
    {
        writer.WriteStartObject();
        writer.WriteString("sourceEventId", contribution.SourceEventId);
        writer.WriteNumber("sourceLogicalSequence", contribution.SourceLogicalSequence);
        WriteMoney(writer, "appliedCapturedAmount", contribution.AppliedCapturedAmount);
        writer.WriteEndObject();
    }

    private static void WriteObservedContribution(
        Utf8JsonWriter writer,
        ObservedPaymentContribution contribution)
    {
        writer.WriteStartObject();
        writer.WriteString("sourceEventId", contribution.SourceEventId);
        writer.WriteNumber("sourceLogicalSequence", contribution.SourceLogicalSequence);
        writer.WriteNumber("deliverySequence", contribution.DeliverySequence);
        writer.WriteNumber("deliveryAttempt", contribution.DeliveryAttempt);
        WriteMoney(
            writer,
            "appliedDeliveredCapturedAmount",
            contribution.AppliedDeliveredCapturedAmount);
        writer.WriteEndObject();
    }

    private static void WriteMoney(Utf8JsonWriter writer, string propertyName, Money money)
    {
        writer.WriteStartObject(propertyName);
        writer.WriteNumber("amount", money.Amount);
        writer.WriteString("currency", money.Currency);
        writer.WriteEndObject();
    }

    private static void WriteCutoff(
        Utf8JsonWriter writer,
        string propertyName,
        ReconciliationCutoff cutoff)
    {
        writer.WriteStartObject(propertyName);
        writer.WriteNumber("sequenceInclusive", cutoff.SequenceInclusive);
        writer.WriteEndObject();
    }

    private static string FormatCategory(ReconciliationFindingCategory category) =>
        category switch
        {
            ReconciliationFindingCategory.CapturedAmountMismatch => "captured-amount-mismatch",
            _ => throw new InvalidOperationException(
                $"Unsupported reconciliation finding category '{category}'."),
        };
}

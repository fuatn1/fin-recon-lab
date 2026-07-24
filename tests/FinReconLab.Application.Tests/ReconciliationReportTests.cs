using System.Reflection;
using System.Text;
using System.Text.Json;
using FinReconLab.Domain;

namespace FinReconLab.Application.Tests;

public sealed class ReconciliationReportTests
{
    private static readonly ReconciliationCutoff Cutoff = new(2);

    [Fact]
    public void Serializer_produces_exact_versioned_report_structure()
    {
        var report = CreateMismatchReport();

        var json = Encoding.UTF8.GetString(new ReconciliationReportJsonSerializer().Serialize(report));

        const string expected =
            """{"schemaVersion":"reconciliation-report.v1","scenario":{"schemaVersion":"payment-captured.v1","scenarioId":"scenario-report","seed":42,"paymentCount":1,"paymentAmount":{"amount":100,"currency":"USD"},"startingOccurredAt":"1970-01-01T00:00:00.0000000\u002B00:00","eventIntervalTicks":600000000},"cutoff":{"sequenceInclusive":2},"expectedSnapshots":[{"orderId":"order-001","capturedAmount":{"amount":100,"currency":"USD"},"cutoff":{"sequenceInclusive":2},"contributions":[{"sourceEventId":"payment-001","sourceLogicalSequence":1,"appliedCapturedAmount":{"amount":100,"currency":"USD"}}]}],"observedSnapshots":[{"orderId":"order-001","capturedAmount":{"amount":75,"currency":"USD"},"cutoff":{"sequenceInclusive":2},"contributions":[{"sourceEventId":"payment-001","sourceLogicalSequence":1,"deliverySequence":1,"deliveryAttempt":1,"appliedDeliveredCapturedAmount":{"amount":75,"currency":"USD"}}]}],"findings":[{"category":"captured-amount-mismatch","orderId":"order-001","cutoff":{"sequenceInclusive":2},"expectedAmount":{"amount":100,"currency":"USD"},"observedAmount":{"amount":75,"currency":"USD"},"signedDelta":{"amount":-25,"currency":"USD"},"expectedContributions":[{"sourceEventId":"payment-001","sourceLogicalSequence":1,"appliedCapturedAmount":{"amount":100,"currency":"USD"}}],"observedContributions":[{"sourceEventId":"payment-001","sourceLogicalSequence":1,"deliverySequence":1,"deliveryAttempt":1,"appliedDeliveredCapturedAmount":{"amount":75,"currency":"USD"}}]}]}""";

        Assert.Equal(expected, json);
    }

    [Fact]
    public void Independently_created_equal_inputs_produce_byte_for_byte_identical_json()
    {
        var first = CreateMismatchReport();
        var second = CreateMismatchReport();
        var serializer = new ReconciliationReportJsonSerializer();

        var firstJson = serializer.Serialize(first);
        var secondJson = serializer.Serialize(second);

        Assert.NotSame(firstJson, secondJson);
        Assert.Equal(firstJson, secondJson);
    }

    [Fact]
    public void Builder_stably_orders_snapshots_findings_and_contribution_traces()
    {
        var orderB = CreatePair("order-b", "payment-b", 2, 20m, 15m);
        var orderAExpected = new ExpectedPaymentSnapshot(
            "order-a",
            new Money(10m, "USD"),
            Cutoff,
            [
                new ExpectedPaymentContribution("payment-a-2", 2, new Money(6m, "USD")),
                new ExpectedPaymentContribution("payment-a-1", 1, new Money(4m, "USD"))
            ]);
        var orderAObserved = new ObservedPaymentSnapshot(
            "order-a",
            new Money(8m, "USD"),
            Cutoff,
            [
                new ObservedPaymentContribution("payment-a-2", 2, 2, 1, new Money(3m, "USD")),
                new ObservedPaymentContribution("payment-a-1", 1, 1, 1, new Money(5m, "USD"))
            ]);
        var orderAFinding = new ReconciliationFinding(
            ReconciliationFindingCategory.CapturedAmountMismatch,
            orderAExpected,
            orderAObserved,
            new Money(-2m, "USD"));

        var report = new ReconciliationReportBuilder().Build(
            CreateScenario(paymentCount: 2),
            Cutoff,
            [orderB.Expected, orderAExpected],
            [orderB.Observed, orderAObserved],
            [orderB.Finding, orderAFinding]);

        Assert.Equal(["order-a", "order-b"], report.ExpectedSnapshots.Select(value => value.OrderId));
        Assert.Equal(["order-a", "order-b"], report.ObservedSnapshots.Select(value => value.OrderId));
        Assert.Equal(["order-a", "order-b"], report.Findings.Select(value => value.OrderId));
        Assert.Equal(
            ["payment-a-1", "payment-a-2"],
            report.Findings[0].ExpectedContributions.Select(value => value.SourceEventId));
        Assert.Equal(
            ["payment-a-1", "payment-a-2"],
            report.Findings[0].ObservedContributions.Select(value => value.SourceEventId));

        var json = Encoding.UTF8.GetString(new ReconciliationReportJsonSerializer().Serialize(report));
        Assert.True(
            json.IndexOf("\"orderId\":\"order-a\"", StringComparison.Ordinal) <
            json.IndexOf("\"orderId\":\"order-b\"", StringComparison.Ordinal));
    }

    [Fact]
    public void Zero_finding_reconciliation_serializes_an_empty_finding_array()
    {
        var pair = CreatePair("order-001", "payment-001", 1, 100m, 100m);
        var report = new ReconciliationReportBuilder().Build(
            CreateScenario(),
            Cutoff,
            [pair.Expected],
            [pair.Observed],
            []);

        var json = new ReconciliationReportJsonSerializer().Serialize(report);
        using var document = JsonDocument.Parse(json);

        Assert.Empty(report.Findings);
        Assert.Equal(0, document.RootElement.GetProperty("findings").GetArrayLength());
    }

    [Fact]
    public void Negative_signed_delta_is_preserved_as_a_json_number()
    {
        var json = new ReconciliationReportJsonSerializer().Serialize(CreateMismatchReport());
        using var document = JsonDocument.Parse(json);

        var signedDelta = document.RootElement
            .GetProperty("findings")[0]
            .GetProperty("signedDelta");

        Assert.Equal(-25m, signedDelta.GetProperty("amount").GetDecimal());
        Assert.Equal("USD", signedDelta.GetProperty("currency").GetString());
    }

    [Fact]
    public void Builder_rejects_null_inputs()
    {
        var pair = CreatePair("order-001", "payment-001", 1, 100m, 75m);
        var builder = new ReconciliationReportBuilder();

        Assert.Throws<ArgumentNullException>(
            () => builder.Build(null!, Cutoff, [pair.Expected], [pair.Observed], [pair.Finding]));
        Assert.Throws<ArgumentNullException>(
            () => builder.Build(CreateScenario(), Cutoff, null!, [pair.Observed], [pair.Finding]));
        Assert.Throws<ArgumentNullException>(
            () => builder.Build(CreateScenario(), Cutoff, [pair.Expected], null!, [pair.Finding]));
        Assert.Throws<ArgumentNullException>(
            () => builder.Build(CreateScenario(), Cutoff, [pair.Expected], [pair.Observed], null!));
    }

    [Fact]
    public void Serializer_rejects_null_report()
    {
        Assert.Throws<ArgumentNullException>(
            () => new ReconciliationReportJsonSerializer().Serialize(null!));
    }

    [Fact]
    public void Report_builder_public_surface_cannot_access_fault_manifest_or_injection_results()
    {
        var exposedTypes = typeof(ReconciliationReportBuilder)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .SelectMany(method => method.GetParameters()
                .Select(parameter => parameter.ParameterType)
                .Append(method.ReturnType))
            .SelectMany(FlattenTypeGraph)
            .ToArray();

        Assert.DoesNotContain(typeof(FaultManifest), exposedTypes);
        Assert.DoesNotContain(
            exposedTypes,
            type => type.Name.Contains("FaultInjectionResult", StringComparison.Ordinal));
        Assert.DoesNotContain(
            exposedTypes,
            type => typeof(FaultManifestEntry).IsAssignableFrom(type));
    }

    private static IEnumerable<Type> FlattenTypeGraph(Type type)
    {
        yield return type;

        foreach (var argument in type.GetGenericArguments())
        {
            foreach (var nestedType in FlattenTypeGraph(argument))
            {
                yield return nestedType;
            }
        }
    }

    private static ReconciliationReport CreateMismatchReport()
    {
        var pair = CreatePair("order-001", "payment-001", 1, 100m, 75m);

        return new ReconciliationReportBuilder().Build(
            CreateScenario(),
            Cutoff,
            [pair.Expected],
            [pair.Observed],
            [pair.Finding]);
    }

    private static ScenarioDefinition CreateScenario(int paymentCount = 1)
    {
        return new ScenarioDefinition(
            ScenarioDefinition.SupportedSchemaVersion,
            "scenario-report",
            42,
            paymentCount,
            new Money(100m, "USD"),
            DateTimeOffset.UnixEpoch,
            TimeSpan.FromMinutes(1));
    }

    private static (
        ExpectedPaymentSnapshot Expected,
        ObservedPaymentSnapshot Observed,
        ReconciliationFinding Finding)
        CreatePair(
            string orderId,
            string eventId,
            long sequence,
            decimal expectedAmount,
            decimal observedAmount)
    {
        var expected = new ExpectedPaymentSnapshot(
            orderId,
            new Money(expectedAmount, "USD"),
            Cutoff,
            [new ExpectedPaymentContribution(eventId, sequence, new Money(expectedAmount, "USD"))]);
        var observed = new ObservedPaymentSnapshot(
            orderId,
            new Money(observedAmount, "USD"),
            Cutoff,
            [new ObservedPaymentContribution(eventId, sequence, sequence, 1, new Money(observedAmount, "USD"))]);

        return (
            expected,
            observed,
            new ReconciliationFinding(
                ReconciliationFindingCategory.CapturedAmountMismatch,
                expected,
                observed,
                observed.CapturedAmount - expected.CapturedAmount));
    }
}

using System.Collections.ObjectModel;
using System.Globalization;
using FinReconLab.Domain;

namespace FinReconLab.Application;

public sealed class PaymentCapturedTruthStreamGenerator
{
    public IReadOnlyList<PaymentCaptured> Generate(ScenarioDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var events = new List<PaymentCaptured>(definition.PaymentCount);
        var eventIds = new HashSet<string>(StringComparer.Ordinal);
        var orderIds = new HashSet<string>(StringComparer.Ordinal);

        for (var ordinal = 1; ordinal <= definition.PaymentCount; ordinal++)
        {
            var eventId = FormatIdentity("payment-captured", definition, ordinal);
            var orderId = FormatIdentity("order", definition, ordinal);

            if (!eventIds.Add(eventId))
            {
                throw new InvalidOperationException($"Generated duplicate event id '{eventId}'.");
            }

            if (!orderIds.Add(orderId))
            {
                throw new InvalidOperationException($"Generated duplicate order id '{orderId}'.");
            }

            events.Add(
                new PaymentCaptured(
                    eventId,
                    orderId,
                    definition.PaymentAmount,
                    logicalSequence: ordinal,
                    CalculateOccurredAt(definition, ordinal)));
        }

        return new ReadOnlyCollection<PaymentCaptured>(events);
    }

    private static DateTimeOffset CalculateOccurredAt(ScenarioDefinition definition, int ordinal)
    {
        try
        {
            var offsetTicks = checked(definition.EventInterval.Ticks * (ordinal - 1));
            return definition.StartingOccurredAt + TimeSpan.FromTicks(offsetTicks);
        }
        catch (ArgumentOutOfRangeException)
        {
            throw new ArgumentOutOfRangeException(
                nameof(definition),
                "Generated event timestamp must be representable by DateTimeOffset.");
        }
        catch (OverflowException)
        {
            throw new ArgumentOutOfRangeException(
                nameof(definition),
                "Generated event timestamp arithmetic exceeded supported range.");
        }
    }

    private static string FormatIdentity(string prefix, ScenarioDefinition definition, int ordinal)
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            "{0}:{1}:seed-{2}:ordinal-{3:D6}",
            prefix,
            definition.ScenarioId,
            definition.Seed,
            ordinal);
    }
}

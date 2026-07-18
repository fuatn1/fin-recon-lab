namespace FinReconLab.Domain;

public readonly record struct Money
{
    public Money(decimal amount, string currency)
    {
        if (!IsValidCurrency(currency))
        {
            throw new ArgumentException("Currency must be a three-letter uppercase ISO 4217 code.", nameof(currency));
        }

        Amount = amount;
        Currency = currency;
    }

    public decimal Amount { get; }

    public string Currency { get; }

    public static Money Zero(string currency) => new(0m, currency);

    public static Money operator +(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return new Money(left.Amount + right.Amount, left.Currency);
    }

    public static Money operator -(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return new Money(left.Amount - right.Amount, left.Currency);
    }

    private static void EnsureSameCurrency(Money left, Money right)
    {
        if (!StringComparer.Ordinal.Equals(left.Currency, right.Currency))
        {
            throw new InvalidOperationException("Money values with different currencies cannot be combined.");
        }
    }

    private static bool IsValidCurrency(string? currency)
    {
        return currency is { Length: 3 } && currency.All(static character => character is >= 'A' and <= 'Z');
    }
}

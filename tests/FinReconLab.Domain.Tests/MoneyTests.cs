using FinReconLab.Domain;

namespace FinReconLab.Domain.Tests;

public sealed class MoneyTests
{
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("US")]
    [InlineData("USDD")]
    [InlineData("usd")]
    [InlineData("US1")]
    [InlineData("U-D")]
    public void Constructor_rejects_malformed_currency_codes(string currency)
    {
        Assert.Throws<ArgumentException>(() => new Money(100m, currency));
    }

    [Fact]
    public void Constructor_rejects_missing_currency()
    {
        Assert.Throws<ArgumentException>(() => new Money(100m, null!));
    }

    [Fact]
    public void Arithmetic_rejects_mixed_currencies()
    {
        var usd = new Money(100m, "USD");
        var eur = new Money(100m, "EUR");

        Assert.Throws<InvalidOperationException>(() => usd + eur);
        Assert.Throws<InvalidOperationException>(() => usd - eur);
    }

    [Fact]
    public void Arithmetic_rejects_null_operands()
    {
        Money? missing = null;
        var usd = new Money(100m, "USD");

        Assert.Throws<ArgumentNullException>(() => usd + missing);
        Assert.Throws<ArgumentNullException>(() => missing - usd);
    }

    [Fact]
    public void Constructor_does_not_silently_round_amounts()
    {
        var money = new Money(100.123456789m, "USD");

        Assert.Equal(100.123456789m, money.Amount);
    }

    [Fact]
    public void PaymentCaptured_rejects_null_money()
    {
        Assert.Throws<ArgumentNullException>(
            () => new PaymentCaptured("payment-captured-001", "order-001", null!, 1, DateTimeOffset.UnixEpoch));
    }

    [Fact]
    public void PaymentSnapshot_rejects_null_money()
    {
        Assert.Throws<ArgumentNullException>(
            () => new PaymentSnapshot("order-001", null!, new ReconciliationCutoff(1)));
    }

    [Fact]
    public void ReconciliationFinding_rejects_mismatched_money_currencies()
    {
        Assert.Throws<InvalidOperationException>(
            () => new ReconciliationFinding(
                ReconciliationFindingCategory.CapturedAmountMismatch,
                "order-001",
                new ReconciliationCutoff(1),
                new Money(100m, "USD"),
                new Money(200m, "EUR"),
                new Money(100m, "USD")));
    }
}

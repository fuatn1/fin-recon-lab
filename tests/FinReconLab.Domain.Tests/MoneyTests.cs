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
    public void Arithmetic_rejects_mixed_currencies()
    {
        var usd = new Money(100m, "USD");
        var eur = new Money(100m, "EUR");

        Assert.Throws<InvalidOperationException>(() => usd + eur);
        Assert.Throws<InvalidOperationException>(() => usd - eur);
    }

    [Fact]
    public void Constructor_does_not_silently_round_amounts()
    {
        var money = new Money(100.123456789m, "USD");

        Assert.Equal(100.123456789m, money.Amount);
    }
}

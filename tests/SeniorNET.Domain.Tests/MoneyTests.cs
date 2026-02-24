using FluentAssertions;
using SeniorNET.Domain.ValueObjects;

namespace SeniorNET.Domain.Tests;

public class MoneyTests
{
    [Fact]
    public void Create_WithValidData_ShouldSucceed()
    {
        var money = Money.Create(100.50m, "USD");

        money.Amount.Should().Be(100.50m);
        money.Currency.Should().Be("USD");
    }

    [Fact]
    public void Create_WithNegativeAmount_ShouldThrow()
    {
        var act = () => Money.Create(-10, "USD");
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("US")]
    [InlineData("USDD")]
    public void Create_WithInvalidCurrency_ShouldThrow(string currency)
    {
        var act = () => Money.Create(100, currency);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Add_SameCurrency_ShouldReturnSum()
    {
        var a = Money.Create(100, "USD");
        var b = Money.Create(50, "USD");

        var result = a.Add(b);

        result.Amount.Should().Be(150);
        result.Currency.Should().Be("USD");
    }

    [Fact]
    public void Add_DifferentCurrency_ShouldThrow()
    {
        var usd = Money.Create(100, "USD");
        var eur = Money.Create(50, "EUR");

        var act = () => usd.Add(eur);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Subtract_ValidAmount_ShouldReturnDifference()
    {
        var a = Money.Create(100, "USD");
        var b = Money.Create(30, "USD");

        var result = a.Subtract(b);

        result.Amount.Should().Be(70);
    }

    [Fact]
    public void Subtract_MoreThanAvailable_ShouldThrow()
    {
        var a = Money.Create(10, "USD");
        var b = Money.Create(50, "USD");

        var act = () => a.Subtract(b);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Multiply_ByQuantity_ShouldReturnProduct()
    {
        var price = Money.Create(25.50m, "USD");

        var result = price.Multiply(3);

        result.Amount.Should().Be(76.50m);
    }

    [Fact]
    public void Zero_ShouldReturnZeroAmount()
    {
        var zero = Money.Zero("EUR");

        zero.Amount.Should().Be(0);
        zero.Currency.Should().Be("EUR");
    }
}

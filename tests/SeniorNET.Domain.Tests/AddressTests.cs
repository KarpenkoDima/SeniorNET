using FluentAssertions;
using SeniorNET.Domain.ValueObjects;

namespace SeniorNET.Domain.Tests;

public class AddressTests
{
    [Fact]
    public void Create_WithValidData_ShouldSucceed()
    {
        var address = Address.Create("123 Main St", "Kyiv", "01001", "Ukraine");

        address.Street.Should().Be("123 Main St");
        address.City.Should().Be("Kyiv");
        address.ZipCode.Should().Be("01001");
        address.Country.Should().Be("Ukraine");
    }

    [Theory]
    [InlineData("", "City", "12345", "Country")]
    [InlineData("Street", "", "12345", "Country")]
    [InlineData("Street", "City", "", "Country")]
    [InlineData("Street", "City", "12345", "")]
    public void Create_WithMissingField_ShouldThrow(string street, string city, string zip, string country)
    {
        var act = () => Address.Create(street, city, zip, country);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ToString_ShouldReturnFormattedAddress()
    {
        var address = Address.Create("123 Main St", "Kyiv", "01001", "Ukraine");

        address.ToString().Should().Be("123 Main St, Kyiv, 01001, Ukraine");
    }

    [Fact]
    public void TwoAddresses_WithSameValues_ShouldBeEqual()
    {
        var a = Address.Create("Street", "City", "12345", "Country");
        var b = Address.Create("Street", "City", "12345", "Country");

        a.Should().Be(b);
    }
}

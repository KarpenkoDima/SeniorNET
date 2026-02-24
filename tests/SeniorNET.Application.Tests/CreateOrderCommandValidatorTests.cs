using FluentAssertions;
using SeniorNET.Application.Commands.CreateOrder;

namespace SeniorNET.Application.Tests;

public class CreateOrderCommandValidatorTests
{
    private readonly CreateOrderCommandValidator _validator = new();

    private static CreateOrderCommand CreateValidCommand() => new(
        CustomerId: "customer-123",
        Street: "123 Main St",
        City: "Kyiv",
        ZipCode: "01001",
        Country: "Ukraine",
        Items: [new CreateOrderItemCommand(Guid.NewGuid(), "Widget", 29.99m, "USD", 2)]);

    [Fact]
    public void Validate_ValidCommand_ShouldPass()
    {
        var result = _validator.Validate(CreateValidCommand());
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyCustomerId_ShouldFail()
    {
        var command = CreateValidCommand() with { CustomerId = "" };
        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "CustomerId");
    }

    [Fact]
    public void Validate_NoItems_ShouldFail()
    {
        var command = CreateValidCommand() with { Items = [] };
        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_NegativePrice_ShouldFail()
    {
        var command = CreateValidCommand() with
        {
            Items = [new CreateOrderItemCommand(Guid.NewGuid(), "Widget", -10, "USD", 1)]
        };
        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_ZeroQuantity_ShouldFail()
    {
        var command = CreateValidCommand() with
        {
            Items = [new CreateOrderItemCommand(Guid.NewGuid(), "Widget", 10, "USD", 0)]
        };
        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_EmptyStreet_ShouldFail()
    {
        var command = CreateValidCommand() with { Street = "" };
        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
    }
}

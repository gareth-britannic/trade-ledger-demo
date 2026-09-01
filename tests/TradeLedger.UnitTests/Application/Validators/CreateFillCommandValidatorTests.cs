using Shouldly;
using TradeLedger.Application;
using TradeLedger.Application.Records;
using TradeLedger.Application.Validators;
using Xunit;

namespace TradeLedger.UnitTests.Application.Validators;

public sealed class CreateFillCommandValidatorTests
{
    private readonly CreateFillCommandValidator _validator = new();

    [Fact]
    public async Task Validate_ValidCommand_Passes()
    {
        var result = await _validator.ValidateAsync(ValidCommand() with { Symbol = " acme.l " });

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Validate_MissingSymbol_Fails(string? symbol)
    {
        var result = await _validator.ValidateAsync(ValidCommand() with { Symbol = symbol! });

        result.Errors.ShouldContain(error => error.PropertyName == nameof(CreateFillCommand.Symbol));
    }

    [Theory]
    [InlineData("$ACME")]
    [InlineData("ACME CO")]
    [InlineData(".ACME")]
    [InlineData("ACME@L")]
    public async Task Validate_InvalidSymbol_Fails(string symbol)
    {
        var result = await _validator.ValidateAsync(ValidCommand() with { Symbol = symbol });

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public async Task Validate_UndefinedSide_Fails()
    {
        var result = await _validator.ValidateAsync(ValidCommand() with { Side = (Side)999 });

        result.Errors.ShouldContain(error => error.PropertyName == nameof(CreateFillCommand.Side));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Validate_NonPositiveQuantity_Fails(decimal quantity)
    {
        var result = await _validator.ValidateAsync(ValidCommand() with { Quantity = quantity });

        result.Errors.ShouldContain(error => error.PropertyName == nameof(CreateFillCommand.Quantity));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Validate_NonPositivePrice_Fails(decimal price)
    {
        var result = await _validator.ValidateAsync(ValidCommand() with { Price = price });

        result.Errors.ShouldContain(error => error.PropertyName == nameof(CreateFillCommand.Price));
    }

    [Fact]
    public async Task Validate_DefaultExecutionTimestamp_Fails()
    {
        var result = await _validator.ValidateAsync(ValidCommand() with { ExecutedAt = default });

        result.Errors.ShouldContain(error => error.PropertyName == nameof(CreateFillCommand.ExecutedAt));
    }

    private static CreateFillCommand ValidCommand() => new(
        Guid.NewGuid(),
        "ACME",
        Side.Buy,
        10m,
        12.34m,
        new DateTimeOffset(2026, 9, 1, 10, 0, 0, TimeSpan.Zero));
}

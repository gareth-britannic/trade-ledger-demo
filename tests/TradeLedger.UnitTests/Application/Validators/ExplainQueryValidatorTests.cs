using Shouldly;
using TradeLedger.Application.Records;
using TradeLedger.Application.Validators;
using Xunit;

namespace TradeLedger.UnitTests.Application.Validators;

public sealed class ExplainQueryValidatorTests
{
    private const string ValidQuestion = "What is my AAPL P&L?";
    private readonly ExplainQueryValidator _validator = new();

    [Fact]
    public async Task Validate_Question_Passes()
    {
        // Arrange
        var query = new ExplainQuery(ValidQuestion);

        // Act
        var result = await _validator.ValidateAsync(query);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Validate_MissingQuestion_Fails(string question)
    {
        // Arrange
        var query = new ExplainQuery(question);

        // Act
        var result = await _validator.ValidateAsync(query);

        // Assert
        result.Errors.ShouldContain(error => error.PropertyName == nameof(ExplainQuery.Question));
    }

    [Fact]
    public async Task Validate_QuestionOverMaximumLength_Fails()
    {
        // Arrange
        var query = new ExplainQuery(new string('x', ExplainQueryValidator.MaximumQuestionLength + 1));

        // Act
        var result = await _validator.ValidateAsync(query);

        // Assert
        result.IsValid.ShouldBeFalse();
    }
}

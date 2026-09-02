using Moq;
using FluentValidation;
using TradeLedger.Application.Interfaces;
using TradeLedger.Application.Records;
using TradeLedger.Application.Services;
using TradeLedger.Application.Validators;
using Xunit;

namespace TradeLedger.UnitTests.Application.Services;

public sealed class ExplainServiceTests
{
    [Fact]
    public async Task Explain_ValidQuestion_DelegatesToLlmClient()
    {
        // Arrange
        using var source = new CancellationTokenSource();
        const string question = "What's my realised P&L on AAPL this month?";
        var expected = new ExplainResult(["get_positions()"], "An answer");
        var llmClient = new Mock<ILlmClient>();
        llmClient.Setup(instance => instance.ExplainAsync(question, source.Token))
            .ReturnsAsync(expected);
        var service = new ExplainService(llmClient.Object, new ExplainQueryValidator());

        // Act
        var result = await service.ExplainAsync(new ExplainQuery(question), source.Token);

        // Assert
        Assert.Same(expected, result);
        llmClient.VerifyAll();
    }

    [Fact]
    public async Task Explain_InvalidQuestion_DoesNotCallLlmClient()
    {
        // Arrange
        var llmClient = new Mock<ILlmClient>();
        var service = new ExplainService(llmClient.Object, new ExplainQueryValidator());

        // Act / Assert
        await Assert.ThrowsAsync<ValidationException>(() => service.ExplainAsync(
            new ExplainQuery(string.Empty),
            CancellationToken.None));

        llmClient.VerifyNoOtherCalls();
    }
}

using Microsoft.AspNetCore.Mvc;
using Moq;
using Shouldly;
using TradeLedger.Api.Contracts.Fills;
using TradeLedger.Api.Contracts.Explain;
using TradeLedger.Api.Contracts.Positions;
using TradeLedger.Api.Controllers;
using TradeLedger.Application;
using TradeLedger.Application.Interfaces;
using TradeLedger.Application.Records;
using TradeLedger.Domain;
using Xunit;

namespace TradeLedger.UnitTests.Api.Controllers;

public sealed class ControllerTests
{
    [Fact]
    public async Task Explain_ReturnsToolCallsAndAnswer_AndCallsOneServiceMethod()
    {
        // Arrange
        const string question = "What happened?";
        const string toolCall = "get_positions()";
        const string answer = "The answer.";
        var service = new Mock<IExplainService>();
        service.Setup(instance => instance.ExplainAsync(It.IsAny<ExplainQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExplainResult([toolCall], answer));
        var controller = new ExplainController(service.Object);

        // Act
        var action = await controller.Explain(new ExplainRequest(question), CancellationToken.None);

        // Assert
        var ok = action.Result.ShouldBeOfType<OkObjectResult>();
        var response = ok.Value.ShouldBeOfType<ExplainResponse>();
        response.ToolCalls.ShouldBe([toolCall]);
        response.Answer.ShouldBe(answer);
        service.Verify(
            instance => instance.ExplainAsync(
                It.Is<ExplainQuery>(query => query.Question == question),
                It.IsAny<CancellationToken>()),
            Times.Once);
        service.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task CreateFill_ReturnsAcceptedWithFillId_AndCallsOneServiceMethod()
    {
        var id = Guid.NewGuid();
        var service = new Mock<IFillRequestService>();
        service.Setup(instance => instance.CreateAsync(It.IsAny<CreateFillCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CreateFillResult(id));
        var controller = new FillsController(service.Object);
        var request = new CreateFillRequest(id, "ACME", Side.Buy, 10m, 12m, DateTimeOffset.UtcNow);

        var action = await controller.Create(request, CancellationToken.None);

        var accepted = action.Result.ShouldBeOfType<AcceptedResult>();
        accepted.Value.ShouldBe(new CreateFillResponse(id));
        service.Verify(
            instance => instance.CreateAsync(It.IsAny<CreateFillCommand>(), It.IsAny<CancellationToken>()),
            Times.Once);
        service.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetPositions_ReturnsExplicitResponseShape()
    {
        var service = new Mock<IPositionQueryService>();
        service.Setup(instance => instance.GetPositionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new PositionResult("ACME", 10m, 12m, 5m)]);
        var controller = new PositionsController(service.Object);

        var action = await controller.Get(CancellationToken.None);

        var ok = action.Result.ShouldBeOfType<OkObjectResult>();
        ok.Value.ShouldBeOfType<List<PositionResponse>>()
            .ShouldBe([new PositionResponse("ACME", 10m, 12m, 5m)]);
        service.VerifyAll();
    }

    [Fact]
    public async Task GetLots_ReturnsExplicitResponseShape()
    {
        var id = Guid.NewGuid();
        var openedAt = DateTimeOffset.UtcNow;
        var service = new Mock<IPositionQueryService>();
        service.Setup(instance => instance.GetOpenLotsAsync("acme", It.IsAny<CancellationToken>()))
            .ReturnsAsync([new LotResult(id, "ACME", 10m, 12m, openedAt)]);
        var controller = new PositionsController(service.Object);

        var action = await controller.GetLots("acme", CancellationToken.None);

        var ok = action.Result.ShouldBeOfType<OkObjectResult>();
        ok.Value.ShouldBeOfType<List<LotResponse>>()
            .ShouldBe([new LotResponse(id, "ACME", 10m, 12m, openedAt)]);
        service.VerifyAll();
    }
}

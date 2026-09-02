using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Shouldly;
using TradeLedger.Api.Contracts.Positions;
using TradeLedger.Api.Controllers;
using TradeLedger.Application.Interfaces;
using TradeLedger.Application.Records;
using Xunit;

namespace TradeLedger.UnitTests.Api.Controllers;

public sealed class PositionsControllerTests
{
    [Fact]
    public async Task Get_MapsEveryPositionAndCancellationToken()
    {
        var expected = new PositionResult("ACME", 10m, 12m, 5m);
        using var cancellation = new CancellationTokenSource();
        var service = new Mock<IPositionQueryService>();
        service.Setup(instance => instance.GetPositionsAsync(cancellation.Token))
            .ReturnsAsync([expected]);
        var controller = new PositionsController(service.Object);

        var action = await controller.Get(cancellation.Token);

        var ok = action.Result.ShouldBeOfType<OkObjectResult>();
        ok.StatusCode.ShouldBe(StatusCodes.Status200OK);
        ok.Value.ShouldBeOfType<List<PositionResponse>>()
            .ShouldBe([new PositionResponse("ACME", 10m, 12m, 5m)]);
        service.VerifyAll();
        service.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetLots_PassesSymbolAndCancellationToken_AndMapsEveryLot()
    {
        var id = Guid.NewGuid();
        var openedAt = new DateTimeOffset(2026, 9, 2, 12, 30, 0, TimeSpan.Zero);
        using var cancellation = new CancellationTokenSource();
        var service = new Mock<IPositionQueryService>();
        service.Setup(instance => instance.GetOpenLotsAsync("acme", cancellation.Token))
            .ReturnsAsync([new LotResult(id, "ACME", 10m, 12m, openedAt)]);
        var controller = new PositionsController(service.Object);

        var action = await controller.GetLots("acme", cancellation.Token);

        var ok = action.Result.ShouldBeOfType<OkObjectResult>();
        ok.StatusCode.ShouldBe(StatusCodes.Status200OK);
        ok.Value.ShouldBeOfType<List<LotResponse>>()
            .ShouldBe([new LotResponse(id, "ACME", 10m, 12m, openedAt)]);
        service.VerifyAll();
        service.VerifyNoOtherCalls();
    }
}

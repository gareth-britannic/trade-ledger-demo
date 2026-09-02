using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Shouldly;
using TradeLedger.Api.Contracts.Fills;
using TradeLedger.Api.Controllers;
using TradeLedger.Application.Interfaces;
using TradeLedger.Application.Records;
using TradeLedger.Domain;
using Xunit;

namespace TradeLedger.UnitTests.Api.Controllers;

public sealed class FillsControllerTests
{
    [Fact]
    public async Task Create_MapsEveryRequestFieldAndCancellationToken_AndReturnsAcceptedId()
    {
        var fillId = Guid.NewGuid();
        var executedAt = new DateTimeOffset(2026, 9, 2, 12, 30, 0, TimeSpan.Zero);
        var request = new CreateFillRequest(fillId, "ACME", Side.Sell, 10m, 12.5m, executedAt);
        var expectedCommand = new CreateFillCommand(
            fillId,
            request.Symbol!,
            request.Side,
            request.Quantity,
            request.Price,
            request.ExecutedAt);
        using var cancellation = new CancellationTokenSource();
        var service = new Mock<IFillRequestService>();
        service.Setup(instance => instance.CreateAsync(expectedCommand, cancellation.Token))
            .ReturnsAsync(new CreateFillResult(fillId));
        var controller = new FillsController(service.Object);

        var action = await controller.Create(request, cancellation.Token);

        var accepted = action.Result.ShouldBeOfType<AcceptedResult>();
        accepted.StatusCode.ShouldBe(StatusCodes.Status202Accepted);
        accepted.Value.ShouldBe(new CreateFillResponse(fillId));
        service.VerifyAll();
        service.VerifyNoOtherCalls();
    }
}

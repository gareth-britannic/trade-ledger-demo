using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Shouldly;
using TradeLedger.Api.Contracts.Explain;
using TradeLedger.Api.Controllers;
using TradeLedger.Application.Interfaces;
using TradeLedger.Application.Records;
using Xunit;

namespace TradeLedger.UnitTests.Api.Controllers;

public sealed class ExplainControllerTests
{
    [Fact]
    public async Task Explain_MapsRequestAndCancellationToken_AndReturnsResponse()
    {
        const string question = "What happened?";
        var expected = new ExplainResult(["get_positions()"], "The answer.");
        using var cancellation = new CancellationTokenSource();
        var service = new Mock<IExplainService>();
        service.Setup(instance => instance.ExplainAsync(
                It.Is<ExplainQuery>(query => query.Question == question),
                cancellation.Token))
            .ReturnsAsync(expected);
        var controller = new ExplainController(service.Object);

        var action = await controller.Explain(new ExplainRequest(question), cancellation.Token);

        var ok = action.Result.ShouldBeOfType<OkObjectResult>();
        ok.StatusCode.ShouldBe(StatusCodes.Status200OK);
        ok.Value.ShouldBe(new ExplainResponse(expected.ToolCalls, expected.Answer));
        service.VerifyAll();
        service.VerifyNoOtherCalls();
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TradeLedger.Api.Constants;
using TradeLedger.Api.Contracts.Fills;
using TradeLedger.Application.Interfaces;
using TradeLedger.Application.Records;

namespace TradeLedger.Api.Controllers;

/// <summary>Accepts executed trade fills for asynchronous FIFO processing.</summary>
[ApiController]
[Route(ApiRoutes.Fills)]
[Authorize]
[Produces(ApiMediaTypes.Json, ApiMediaTypes.ProblemJson)]
public sealed class FillsController(IFillRequestService fillRequestService) : ControllerBase
{
    /// <summary>Accepts and queues a fill.</summary>
    /// <remarks>
    /// The fill is persisted before it is published for asynchronous processing.
    /// </remarks>
    /// <param name="request">The executed fill to accept.</param>
    /// <param name="cancellationToken">Cancels work when the request is aborted.</param>
    /// <returns>The stable ID of the accepted fill.</returns>
    [HttpPost(Name = ApiOperationIds.CreateFill)]
    [ProducesResponseType(typeof(CreateFillResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<CreateFillResponse>> Create(
        CreateFillRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateFillCommand(request.FillId, request.Symbol!, request.Side,
            request.Quantity, request.Price, request.ExecutedAt);
        var result = await fillRequestService.CreateAsync(command, cancellationToken);
        return Accepted(new CreateFillResponse(result.FillId));
    }
}

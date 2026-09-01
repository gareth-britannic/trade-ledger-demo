using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TradeLedger.Api.Constants;
using TradeLedger.Api.Contracts.Positions;
using TradeLedger.Application.Interfaces;

namespace TradeLedger.Api.Controllers;

/// <summary>Reads positions derived from asynchronously maintained FIFO lots.</summary>
[ApiController]
[Route(ApiRoutes.Positions)]
[Authorize]
[Produces(ApiMediaTypes.Json, ApiMediaTypes.ProblemJson)]
public sealed class PositionsController(IPositionQueryService positionQueryService) : ControllerBase
{
    /// <summary>Gets all current positions.</summary>
    /// <remarks>
    /// Open quantity and average unit cost are derived from open lots; realised P&amp;L is read from
    /// values persisted by the FIFO processor.
    /// </remarks>
    /// <param name="cancellationToken">Cancels work when the request is aborted.</param>
    /// <returns>The current position summaries.</returns>
    [HttpGet(Name = ApiOperationIds.GetPositions)]
    [ProducesResponseType(typeof(IReadOnlyList<PositionResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IReadOnlyList<PositionResponse>>> Get(CancellationToken cancellationToken)
    {
        var results = await positionQueryService.GetPositionsAsync(cancellationToken);
        return Ok(results.Select(PositionResponse.FromApplication).ToArray());
    }

    /// <summary>Gets the FIFO-ordered open lots for a position.</summary>
    /// <remarks>
    /// The symbol is trimmed and normalized before lookup. A well-formed symbol with no persisted
    /// position returns 404.
    /// </remarks>
    /// <param name="symbol">The case-insensitive market symbol.</param>
    /// <param name="cancellationToken">Cancels work when the request is aborted.</param>
    /// <returns>The position's open lots in FIFO order.</returns>
    [HttpGet(ApiRoutes.PositionLots, Name = ApiOperationIds.GetPositionLots)]
    [ProducesResponseType(typeof(IReadOnlyList<LotResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IReadOnlyList<LotResponse>>> GetLots(
        string symbol,
        CancellationToken cancellationToken)
    {
        var results = await positionQueryService.GetOpenLotsAsync(symbol, cancellationToken);
        return Ok(results.Select(LotResponse.FromApplication).ToArray());
    }
}

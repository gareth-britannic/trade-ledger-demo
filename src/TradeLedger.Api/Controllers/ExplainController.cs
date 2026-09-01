using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TradeLedger.Api.Constants;
using TradeLedger.Api.Contracts.Explain;
using TradeLedger.Application.Interfaces;
using TradeLedger.Application.Records;

namespace TradeLedger.Api.Controllers;

/// <summary>Explains ledger data and exposes the tools used to produce the answer.</summary>
[ApiController]
[Route(ApiRoutes.Explain)]
[Authorize]
[Produces(ApiMediaTypes.Json, ApiMediaTypes.ProblemJson)]
public sealed class ExplainController(IExplainService explainService) : ControllerBase
{
    /// <summary>Answers a question about the current ledger.</summary>
    /// <param name="request">The ledger question to answer.</param>
    /// <param name="cancellationToken">Cancels work when the request is aborted.</param>
    /// <returns>The tool calls and resulting answer.</returns>
    [HttpPost(Name = ApiOperationIds.Explain)]
    [ProducesResponseType(typeof(ExplainResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ExplainResponse>> Explain(
        ExplainRequest request,
        CancellationToken cancellationToken)
    {
        var result = await explainService.ExplainAsync(
            new ExplainQuery(request.Question!),
            cancellationToken);
        return Ok(ExplainResponse.FromApplication(result));
    }
}

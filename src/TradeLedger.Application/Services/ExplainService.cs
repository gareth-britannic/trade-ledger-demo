using FluentValidation;
using TradeLedger.Application.Interfaces;
using TradeLedger.Application.Records;

namespace TradeLedger.Application.Services;

public sealed class ExplainService(
    ILlmClient llmClient,
    IValidator<ExplainQuery> validator) : IExplainService
{
    public async Task<ExplainResult> ExplainAsync(
        ExplainQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        await validator.ValidateAndThrowAsync(query, cancellationToken);

        return await llmClient.ExplainAsync(query.Question, cancellationToken);
    }
}

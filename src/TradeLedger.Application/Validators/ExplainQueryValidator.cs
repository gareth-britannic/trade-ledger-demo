using FluentValidation;
using TradeLedger.Application.Records;

namespace TradeLedger.Application.Validators;

public sealed class ExplainQueryValidator : AbstractValidator<ExplainQuery>
{
    public const int MaximumQuestionLength = 500;

    public ExplainQueryValidator()
    {
        RuleFor(query => query.Question)
            .NotEmpty()
            .MaximumLength(MaximumQuestionLength);
    }
}

using FluentValidation;
using TradeLedger.Application.Messaging;
using TradeLedger.Domain;

namespace TradeLedger.Processor.Validation;

public sealed class FillMessageValidator : AbstractValidator<FillRequestMessage>
{
    private const string MessageGroupIdContextKey = nameof(MessageGroupIdContextKey);

    public FillMessageValidator()
    {
        RuleFor(message => message.FillId).NotEmpty();
        RuleFor(message => message.Symbol)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .Must(SymbolNormalizer.IsValid)
            .WithMessage("Symbol is invalid.");
        RuleFor(message => message.Side)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .Must(IsValidSide)
            .WithMessage("Side must be Buy or Sell.");
        RuleFor(message => message.Quantity).GreaterThan(0);
        RuleFor(message => message.Price).GreaterThan(0);
        RuleFor(message => message.ExecutedAt).NotEqual(default(DateTimeOffset));
        RuleFor(message => message).Custom((message, context) =>
        {
            var messageGroupId = context.RootContextData[MessageGroupIdContextKey] as string;
            if (!SymbolNormalizer.IsValid(message.Symbol) ||
                string.IsNullOrWhiteSpace(messageGroupId) ||
                !string.Equals(
                    SymbolNormalizer.Normalize(message.Symbol),
                    messageGroupId,
                    StringComparison.Ordinal))
            {
                context.AddFailure(
                    "MessageGroupId",
                    "The FIFO message group must equal the normalized fill symbol.");
            }
        });
    }

    public async Task ValidateAndThrowAsync(
        FillRequestMessage message,
        string? messageGroupId,
        CancellationToken cancellationToken)
    {
        var context = new ValidationContext<FillRequestMessage>(message);
        context.RootContextData[MessageGroupIdContextKey] = messageGroupId ?? string.Empty;
        var result = await ValidateAsync(context, cancellationToken);
        if (!result.IsValid)
        {
            throw new ValidationException(result.Errors);
        }
    }

    private static bool IsValidSide(string? value) =>
        Enum.TryParse<Side>(value, true, out var side) && Enum.IsDefined(side);
}

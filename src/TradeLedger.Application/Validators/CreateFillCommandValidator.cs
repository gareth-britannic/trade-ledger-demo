using FluentValidation;
using TradeLedger.Application.Records;
using TradeLedger.Domain;

namespace TradeLedger.Application.Validators;

public sealed class CreateFillCommandValidator : AbstractValidator<CreateFillCommand>
{
    public CreateFillCommandValidator()
    {
        RuleFor(command => command.FillId)
            .NotEqual(Guid.Empty)
            .When(command => command.FillId.HasValue);
        RuleFor(command => command.Symbol)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .Must(symbol => SymbolNormalizer.Normalize(symbol).Length <= SymbolNormalizer.MaximumLength)
            .WithMessage($"Symbol must be no longer than {SymbolNormalizer.MaximumLength} characters after trimming.")
            .Must(SymbolNormalizer.IsValid)
            .WithMessage("Symbol may contain letters, digits, '.', '-', '_', and '/', and must begin with a letter or digit.");
        RuleFor(command => command.Side).IsInEnum();
        RuleFor(command => command.Quantity).GreaterThan(0);
        RuleFor(command => command.Price).GreaterThan(0);
        RuleFor(command => command.ExecutedAt)
            .NotEqual(default(DateTimeOffset))
            .WithMessage("ExecutedAt must be a valid timestamp.");
    }
}

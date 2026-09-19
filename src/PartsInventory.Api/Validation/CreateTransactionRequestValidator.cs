using FluentValidation;
using PartsInventory.Api.Dtos;

namespace PartsInventory.Api.Validation;

/// <summary>
/// Field-shape rules for a stock transaction. The negative-quantity business rule is NOT here:
/// it depends on the part's current on-hand quantity and must be evaluated atomically with the
/// write to stay race-safe, so it lives in PartService.AddTransactionAsync (see DECISIONS.md Q4).
/// </summary>
public class CreateTransactionRequestValidator : AbstractValidator<CreateTransactionRequest>
{
    public CreateTransactionRequestValidator()
    {
        RuleFor(x => x.QuantityChange)
            .NotEqual(0)
            .WithMessage("QuantityChange must be non-zero.");

        RuleFor(x => x.Reason)
            .MaximumLength(200);
    }
}

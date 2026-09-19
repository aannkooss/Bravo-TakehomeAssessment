using FluentValidation;
using PartsInventory.Api.Dtos;
using PartsInventory.Api.Services;

namespace PartsInventory.Api.Validation;

/// <summary>
/// Field-shape rules for creating a part, plus the async SKU-uniqueness business rule.
/// Runs in the pipeline via the ValidationFilter (IEndpointFilter), not hand-invoked.
/// </summary>
public class CreatePartRequestValidator : AbstractValidator<CreatePartRequest>
{
    public CreatePartRequestValidator(IPartService parts)
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.Sku)
            .NotEmpty()
            .MaximumLength(64)
            .MustAsync(async (sku, ct) => !await parts.SkuExistsAsync(sku, excludePartId: null, ct))
            .WithMessage("A part with this SKU already exists.");

        RuleFor(x => x.Quantity)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Opening quantity cannot be negative.");

        RuleFor(x => x.Location)
            .MaximumLength(100);
    }
}

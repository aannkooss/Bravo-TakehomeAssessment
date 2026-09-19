using FluentValidation;
using PartsInventory.Api.Dtos;
using PartsInventory.Api.Services;

namespace PartsInventory.Api.Validation;

/// <summary>
/// Field-shape rules for updating a part, plus async SKU-uniqueness that excludes the part
/// being updated. The route id is read from ValidationContext.RootContextData, which the
/// ValidationFilter populates from HttpContext route values.
/// </summary>
public class UpdatePartRequestValidator : AbstractValidator<UpdatePartRequest>
{
    public const string RoutePartIdKey = "RoutePartId";

    public UpdatePartRequestValidator(IPartService parts)
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.Sku)
            .NotEmpty()
            .MaximumLength(64)
            .MustAsync(async (_, sku, context, ct) =>
            {
                int? excludeId = context.RootContextData.TryGetValue(RoutePartIdKey, out var raw) && raw is int id
                    ? id
                    : null;
                return !await parts.SkuExistsAsync(sku, excludeId, ct);
            })
            .WithMessage("A part with this SKU already exists.");

        RuleFor(x => x.Location)
            .MaximumLength(100);
    }
}

using FluentValidation;

namespace PartsInventory.Api.Validation;

/// <summary>
/// Generic endpoint filter that validates the first argument of type <typeparamref name="T"/>
/// using the registered <see cref="IValidator{T}"/>. Validation runs in the pipeline (not
/// hand-invoked inside each handler); failures short-circuit with a single
/// <c>ValidationProblemDetails</c> (application/problem+json) via <c>TypedResults.ValidationProblem</c>.
/// The route id (if present) is surfaced to validators through RootContextData so async rules
/// like SKU-uniqueness-excluding-self can see it.
/// </summary>
public class ValidationFilter<T> : IEndpointFilter where T : class
{
    private readonly IValidator<T> _validator;

    public ValidationFilter(IValidator<T> validator) => _validator = validator;

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var model = context.Arguments.OfType<T>().FirstOrDefault();
        if (model is null)
            return await next(context);

        var validationContext = new ValidationContext<T>(model);
        if (context.HttpContext.Request.RouteValues.TryGetValue("id", out var raw)
            && int.TryParse(raw?.ToString(), out var id))
        {
            validationContext.RootContextData[UpdatePartRequestValidator.RoutePartIdKey] = id;
        }

        var result = await _validator.ValidateAsync(validationContext, context.HttpContext.RequestAborted);
        if (result.IsValid)
            return await next(context);

        var errors = result.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

        return TypedResults.ValidationProblem(errors);
    }
}

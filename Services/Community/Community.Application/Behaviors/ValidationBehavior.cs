using Community.Application.Common;
using FluentValidation;
using ValidationException = Community.Application.Common.ValidationException;
using MediatR;

namespace Community.Application.Behaviors;

// Запускає всі валідатори команди/запиту, збирає ВСІ помилки й кидає ValidationException до виклику обробника.
public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (!validators.Any())
            return await next();

        var context = new ValidationContext<TRequest>(request);
        var results = await Task.WhenAll(validators.Select(v => v.ValidateAsync(context, cancellationToken)));

        var errors = results
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .GroupBy(f => f.PropertyName, f => f.ErrorMessage)
            .ToDictionary(g => g.Key, g => g.Distinct().ToArray());

        if (errors.Count > 0)
            throw new ValidationException(errors);

        return await next();
    }
}

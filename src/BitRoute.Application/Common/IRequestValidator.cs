using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace BitRoute.Application.Common;

/// <summary>
/// Single entry point for request validation: resolves the registered
/// <see cref="IValidator{T}"/> for the request type and throws
/// <see cref="ValidationException"/> on failure. Services depend on this one
/// abstraction instead of one validator per DTO.
/// </summary>
public interface IRequestValidator
{
    Task ValidateAndThrowAsync<TRequest>(TRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Resolves validators from DI at call time. A request type with no registered
/// validator is treated as a programming error, not silently waved through:
/// every incoming DTO must have a validator.
/// </summary>
public sealed class RequestValidator : IRequestValidator
{
    private readonly IServiceProvider _services;

    public RequestValidator(IServiceProvider services)
    {
        _services = services;
    }

    public async Task ValidateAndThrowAsync<TRequest>(
        TRequest request, CancellationToken cancellationToken = default)
    {
        var validator = _services.GetService<IValidator<TRequest>>()
            ?? throw new InvalidOperationException(
                $"No validator is registered for {typeof(TRequest).Name}. Every request DTO must have one.");

        await validator.ValidateAndThrowAsync(request, cancellationToken);
    }
}

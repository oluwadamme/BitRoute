using BitRoute.Application;
using BitRoute.Domain.Exceptions;
using FluentValidation;

namespace BitRoute.Api.Middleware;

/// <summary>
/// The single place domain and validation failures become HTTP responses, wearing the
/// same { status, message, data } envelope as success responses. Services throw typed
/// exceptions; this table gives them status codes. Every new DomainException subtype
/// must be mapped here.
/// </summary>
public sealed class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception exception) when (!context.Response.HasStarted)
        {
            await WriteErrorAsync(context, exception);
        }
    }

    private async Task WriteErrorAsync(HttpContext context, Exception exception)
    {
        var (statusCode, body) = exception switch
        {
            ValidationException validation => (
                StatusCodes.Status400BadRequest,
                ApiResponse.Error(
                    "One or more validation errors occurred.",
                    validation.Errors
                        .GroupBy(e => e.PropertyName)
                        .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()))),
            EmailAlreadyRegisteredException e => (StatusCodes.Status409Conflict, ApiResponse.Error(e.Message)),
            InvalidCredentialsException e => (StatusCodes.Status401Unauthorized, ApiResponse.Error(e.Message)),
            InvalidRefreshTokenException e => (StatusCodes.Status401Unauthorized, ApiResponse.Error(e.Message)),
            RefreshTokenReuseException e => (StatusCodes.Status401Unauthorized, ApiResponse.Error(e.Message)),
            AccountCreationException e => (StatusCodes.Status400BadRequest, ApiResponse.Error(e.Message)),
            SeatUnavailableException e => (StatusCodes.Status409Conflict, ApiResponse.Error(e.Message)),
            HoldExpiredException e => (StatusCodes.Status410Gone, ApiResponse.Error(e.Message)),
            ScheduleNotFoundException e => (StatusCodes.Status404NotFound, ApiResponse.Error(e.Message)),
            InvalidBookingTransitionException e => (StatusCodes.Status400BadRequest, ApiResponse.Error(e.Message)),
            BookingDomainException e => (StatusCodes.Status400BadRequest, ApiResponse.Error(e.Message)),
            // Safety net: a DomainException without a specific mapping is a client-visible
            // rule violation, not a server fault. Add a specific arm when one appears.
            DomainException e => (StatusCodes.Status400BadRequest, ApiResponse.Error(e.Message)),
            _ => (StatusCodes.Status500InternalServerError, ApiResponse.Error("An unexpected error occurred.")),
        };

        if (statusCode == StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(exception, "Unhandled exception for {Method} {Path}",
                context.Request.Method, context.Request.Path);
        }

        context.Response.StatusCode = statusCode;
        await context.Response.WriteAsJsonAsync(body);
    }
}

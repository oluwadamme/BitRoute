namespace BitRoute.Application;

/// <summary>
/// The uniform response envelope every endpoint returns, success or failure:
/// { "status": true | false, "message": ..., "data": ... }.
/// Inner layers define this so both Application services and Api layer share the contract.
/// </summary>
/// <example>
/// {
///   "status": true,
///   "message": "Request successful.",
///   "data": null
/// }
/// </example>
public sealed record ApiResponse<T>(bool Status, string Message, T? Data);

public static class ApiResponse
{
    public static ApiResponse<T> Success<T>(T data, string message = "Request successful.")
        => new(true, message, data);

    public static ApiResponse<object> SuccessMessage(string message)
        => new(true, message, null);

    public static ApiResponse<object> Error(string message, object? data = null)
        => new(false, message, data);
}

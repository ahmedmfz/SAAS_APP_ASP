namespace SaaSPlatform.Application.Common;

public class ApiResponse<T>
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public T? Data { get; init; }
    public Dictionary<string, string[]>? Errors { get; init; }

    public static ApiResponse<T> Ok(T data, string message = "Success")
        => new() { Success = true, Message = message, Data = data };

    public static ApiResponse<object> Fail(string message)
        => new ApiResponse<object> { Success = false, Message = message, Data = null };

    public static ApiResponse<object> ValidationFail(Dictionary<string, string[]> errors)
        => new ApiResponse<object>
        {
            Success = false,
            Message = "Validation failed.",
            Data    = null,
            Errors  = errors
        };
}

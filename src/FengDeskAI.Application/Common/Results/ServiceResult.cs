using FengDeskAI.Application.Common.Constants;

namespace FengDeskAI.Application.Common.Results;

public class ServiceResult : IServiceResult
{
    public bool IsSuccess { get; protected set; }
    public int StatusCode { get; protected set; }
    public string? Message { get; protected set; }
    public IEnumerable<string>? Errors { get; protected set; }

    protected ServiceResult(bool isSuccess, int statusCode, string? message = null, IEnumerable<string>? errors = null)
    {
        IsSuccess = isSuccess;
        StatusCode = statusCode;
        Message = message;
        Errors = errors;
    }

    public static ServiceResult Success(string? message = null, int statusCode = ApiStatusCodes.Ok)
        => new(true, statusCode, message);

    public static ServiceResult Failure(int statusCode, string message)
        => new(false, statusCode, message);

    public static ServiceResult Failure(int statusCode, IEnumerable<string> errors)
        => new(false, statusCode, errors: errors);
}

public class ServiceResult<T> : ServiceResult, IServiceResult<T>
{
    public T? Data { get; private set; }

    protected ServiceResult(bool isSuccess, int statusCode, T? data = default, string? message = null, IEnumerable<string>? errors = null)
        : base(isSuccess, statusCode, message, errors)
    {
        Data = data;
    }

    public static ServiceResult<T> Success(T data, string? message = null, int statusCode = ApiStatusCodes.Ok)
        => new(true, statusCode, data, message);

    public static new ServiceResult<T> Failure(int statusCode, string message)
        => new(false, statusCode, default, message);

    public static new ServiceResult<T> Failure(int statusCode, IEnumerable<string> errors)
        => new(false, statusCode, default, errors: errors);
}

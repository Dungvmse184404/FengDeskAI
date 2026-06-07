namespace FengDeskAI.Application.Common.Results;

public interface IServiceResult
{
    bool IsSuccess { get; }
    int StatusCode { get; }
    string? Message { get; }
    IEnumerable<string>? Errors { get; }
}

public interface IServiceResult<out T> : IServiceResult
{
    T? Data { get; }
}

using FengDeskAI.Application.Common.Constants;
using FengDeskAI.Application.Common.Results;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace FengDeskAI.WebAPI.Common.Filters;

/// <summary>
/// Catch <see cref="UnauthorizedAccessException"/> (thường do <c>ApiControllerBase.CurrentUserId</c>
/// throw khi thiếu user claim) và trả ServiceResult 401 chuẩn — đồng dạng với các endpoint khác.
/// </summary>
public class UnauthorizedExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is not UnauthorizedAccessException ex) return;

        var payload = ServiceResult.Failure(
            ApiStatusCodes.Unauthorized,
            string.IsNullOrWhiteSpace(ex.Message) ? "Unauthorized." : ex.Message);

        context.Result = new ObjectResult(payload) { StatusCode = ApiStatusCodes.Unauthorized };
        context.ExceptionHandled = true;
    }
}

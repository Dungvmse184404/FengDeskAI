using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace FengDeskAI.WebAPI.Authorization;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class ResourceAuthorizeAttribute : TypeFilterAttribute
{
    public ResourceAuthorizeAttribute(ResourceOperation operation, string routeKey)
        : base(typeof(ResourceAuthorizationFilter))
    {
        Arguments = new object[] { operation, routeKey };
    }
}

public sealed class ResourceAuthorizationFilter : IAsyncAuthorizationFilter
{
    private readonly ResourceOperation _operation;
    private readonly string _routeKey;
    private readonly IAuthorizationService _authorization;

    public ResourceAuthorizationFilter(
        ResourceOperation operation, string routeKey, IAuthorizationService authorization)
    {
        _operation = operation;
        _routeKey = routeKey;
        _authorization = authorization;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (!context.RouteData.Values.TryGetValue(_routeKey, out var value)
            || !Guid.TryParse(value?.ToString(), out var id))
        {
            context.Result = new BadRequestObjectResult(new { message = $"Route resource '{_routeKey}' không hợp lệ." });
            return;
        }

        var result = await _authorization.AuthorizeAsync(
            context.HttpContext.User, new ResourceReference(id), new ResourceAccessRequirement(_operation));
        if (!result.Succeeded) context.Result = new ForbidResult();
    }
}

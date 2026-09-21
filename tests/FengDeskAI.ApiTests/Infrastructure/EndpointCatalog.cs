using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ActionConstraints;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using FengDeskAI.WebAPI.Authorization;

namespace FengDeskAI.ApiTests.Infrastructure;

/// <summary>Một action đã đăng ký, kèm yêu cầu phân quyền đọc từ metadata.</summary>
public sealed record EndpointInfo(
    string HttpMethod,
    string RouteTemplate,
    string Controller,
    string Action,
    bool AllowsAnonymous,
    IReadOnlyList<string> Policies,
    IReadOnlyList<string> ConsumesContentTypes)
{
    public bool RequiresAuth => !AllowsAnonymous;

    /// <summary>
    /// Action khai <c>[Consumes("multipart/form-data")]</c>. Gửi sai content-type thì khâu CHỌN
    /// ACTION trả 415 ngay trong UseRouting — trước cả khi AuthorizationMiddleware kịp chạy, nên
    /// không bao giờ thấy 401/403.
    /// </summary>
    public bool ConsumesMultipart =>
        ConsumesContentTypes.Any(c => c.Contains("multipart/form-data", StringComparison.OrdinalIgnoreCase));

    /// <summary>Có tham số route ({id}, {storeId}…) → cần thay giá trị giả trước khi gọi.</summary>
    public bool HasRouteParameters => RouteTemplate.Contains('{');

    public override string ToString() => $"{HttpMethod} /{RouteTemplate} ({Controller}.{Action})";
}

/// <summary>
/// Liệt kê toàn bộ endpoint từ <see cref="IActionDescriptorCollectionProvider"/> — nguồn thật của
/// routing, chính xác hơn parse swagger.json và đọc được cả policy phân quyền.
/// Nhờ vậy ma trận quyền ở tầng 1 tự phủ endpoint mới mà không cần ai nhớ cập nhật danh sách.
/// </summary>
public static class EndpointCatalog
{
    /// <summary>Policy chấp nhận những role NÀO — phải khớp phần AddAuthorization trong Program.cs.</summary>
    private static readonly Dictionary<string, string[]> PolicyRoles = new()
    {
        [AuthorizationPolicies.AdminOnly] = [Roles.Admin],
        [AuthorizationPolicies.StaffOrAbove] = [Roles.Staff, Roles.Manager, Roles.Admin],
        [AuthorizationPolicies.ManagerOrAbove] = [Roles.Manager, Roles.Admin],
        [AuthorizationPolicies.CustomerOnly] = [Roles.Customer],
        [AuthorizationPolicies.GardenOwnerOrAbove] = [Roles.GardenOwner, Roles.Admin],
    };

    /// <summary>
    /// Claim role THẬT mà mỗi user test mang — phải khớp <c>ApiTestFixture.RoleMap()</c>.
    /// Quan trọng: user GardenOwner mang cả <c>Customer</c> (GardenOwner là flag cộng thêm), nên nó
    /// vẫn qua được policy CustomerOnly. Tính nhầm chỗ này là test khẳng định 403 nhưng thực tế 200.
    /// </summary>
    private static readonly Dictionary<TestRole, string[]> RoleClaims = new()
    {
        [TestRole.Customer] = [Roles.Customer],
        [TestRole.Staff] = [Roles.Staff],
        [TestRole.Manager] = [Roles.Manager],
        [TestRole.Admin] = [Roles.Admin],
        [TestRole.GardenOwner] = [Roles.Customer, Roles.GardenOwner],
    };

    public static IReadOnlyList<EndpointInfo> Discover(IServiceProvider services)
    {
        var provider = services.GetRequiredService<IActionDescriptorCollectionProvider>();
        var result = new List<EndpointInfo>();

        foreach (var descriptor in provider.ActionDescriptors.Items.OfType<ControllerActionDescriptor>())
        {
            var template = descriptor.AttributeRouteInfo?.Template;
            if (string.IsNullOrWhiteSpace(template)) continue;

            var allowsAnonymous = descriptor.EndpointMetadata.OfType<IAllowAnonymous>().Any();

            var policies = descriptor.EndpointMetadata
                .OfType<IAuthorizeData>()
                .Select(a => a.Policy)
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p => p!)
                .Distinct()
                .ToList();

            var requiresAuth = descriptor.EndpointMetadata.OfType<IAuthorizeData>().Any();

            var consumes = descriptor.EndpointMetadata
                .OfType<ConsumesAttribute>()
                .SelectMany(a => a.ContentTypes)
                .Distinct()
                .ToList();

            // [ApiController] tự thêm ràng buộc multipart/form-data cho action có tham số IFormFile
            // mà KHÔNG sinh ra ConsumesAttribute trong EndpointMetadata — nên phải dò theo kiểu tham số.
            if (consumes.Count == 0 && HasFormFileParameter(descriptor))
                consumes.Add("multipart/form-data");

            foreach (var method in HttpMethodsOf(descriptor))
            {
                result.Add(new EndpointInfo(
                    method,
                    template,
                    descriptor.ControllerName,
                    descriptor.ActionName,
                    AllowsAnonymous: allowsAnonymous || !requiresAuth,
                    policies,
                    consumes));
            }
        }

        return result.OrderBy(e => e.RouteTemplate).ThenBy(e => e.HttpMethod).ToList();
    }

    private static bool HasFormFileParameter(ControllerActionDescriptor descriptor)
        => descriptor.Parameters.Any(p =>
            typeof(IFormFile).IsAssignableFrom(p.ParameterType)
            || typeof(IFormFileCollection).IsAssignableFrom(p.ParameterType)
            || typeof(IFormCollection).IsAssignableFrom(p.ParameterType)
            || typeof(IEnumerable<IFormFile>).IsAssignableFrom(p.ParameterType));

    /// <summary>
    /// HTTP method của action, đọc từ <see cref="HttpMethodActionConstraint"/>.
    /// Action không ràng buộc method (hiếm) coi như GET để vẫn đưa được vào ma trận.
    /// </summary>
    private static IEnumerable<string> HttpMethodsOf(ControllerActionDescriptor descriptor)
    {
        var methods = descriptor.ActionConstraints?
            .OfType<HttpMethodActionConstraint>()
            .SelectMany(c => c.HttpMethods)
            .Distinct()
            .ToList();

        return methods is { Count: > 0 } ? methods : ["GET"];
    }

    /// <summary>
    /// Một role chắc chắn KHÔNG qua được endpoint — dùng để khẳng định trả 403.
    /// Null nếu endpoint không gắn policy nào đã biết (khi đó không có gì để khẳng định).
    /// </summary>
    public static TestRole? PickDisallowedRole(EndpointInfo endpoint)
    {
        // Controller gắn policy ở cấp class nhưng action đè [AllowAnonymous] → ai gọi cũng qua,
        // không có role nào bị từ chối để mà khẳng định 403.
        if (endpoint.AllowsAnonymous) return null;

        var known = endpoint.Policies.Where(PolicyRoles.ContainsKey).ToList();
        if (known.Count == 0) return null;

        foreach (var (role, claims) in RoleClaims)
        {
            // Bị TỪ CHỐI nếu có ít nhất một policy mà role không có claim nào khớp.
            var rejected = known.Any(policy => !PolicyRoles[policy].Intersect(claims).Any());
            if (rejected) return role;
        }

        return null;
    }

    /// <summary>
    /// Thay tham số route bằng giá trị lấy từ <paramref name="data"/> để dựng URL gọi được.
    /// Giá trị không cần trỏ tới bản ghi có thật: phân quyền chạy trước model binding.
    /// </summary>
    public static string BuildUrl(EndpointInfo endpoint, TestDataSet data)
    {
        var segments = endpoint.RouteTemplate.Split('/');

        for (var i = 0; i < segments.Length; i++)
        {
            var segment = segments[i];
            if (!segment.StartsWith('{')) continue;

            var (name, constraint) = SplitParameter(segment);
            segments[i] = Uri.EscapeDataString(data.RouteValue(name, constraint));
        }

        return "/" + string.Join('/', segments);
    }

    /// <summary>Tách <c>{id:guid}</c> → ("id", "guid"); <c>{code?}</c> → ("code", null).</summary>
    private static (string Name, string? Constraint) SplitParameter(string segment)
    {
        var inner = segment.Trim('{', '}', '?');
        var colon = inner.IndexOf(':');
        return colon >= 0 ? (inner[..colon], inner[(colon + 1)..]) : (inner, null);
    }
}

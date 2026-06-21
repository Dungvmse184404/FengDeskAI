using System.Text.Json;
using FengDeskAI.Application.Interfaces.External;
using FengDeskAI.WebAPI.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FengDeskAI.WebAPI.Controllers;

/// <summary>
/// [CHỈ MÔI TRƯỜNG DEVELOPMENT] Test tay các AI tool — gọi thẳng <see cref="IAiTool.ExecuteAsync"/>
/// đúng path mà AI dùng, với ngữ cảnh scope theo user đang đăng nhập. Khác path AI ở chỗ
/// endpoint này KHÔNG nuốt exception (path AI bắt lỗi và trả "Tool execution failed" chung chung)
/// → giúp lộ nguyên nhân thật khiến tool báo lỗi (vd search_products / recommend_products).
/// Ngoài Production trả 404.
/// </summary>
[Route("api/dev/tools")]
[Authorize]
public sealed class DevToolsController : ApiControllerBase
{
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    private readonly IReadOnlyList<IAiTool> _tools;
    private readonly IWebHostEnvironment _env;

    public DevToolsController(IEnumerable<IAiTool> tools, IWebHostEnvironment env)
    {
        _tools = tools.ToList();
        _env = env;
    }

    /// <summary>Liệt kê tool + schema tham số (để biết cần truyền gì khi test).</summary>
    [HttpGet]
    public IActionResult List()
    {
        if (!_env.IsDevelopment()) return NotFound();

        var tools = _tools.Select(t => new
        {
            t.Name,
            t.Description,
            parameters = t.Parameters.ToDictionary(
                p => p.Key,
                p => new { p.Value.Type, p.Value.Description, p.Value.Required, p.Value.Enum }),
        });
        return Ok(tools);
    }

    /// <summary>
    /// Chạy 1 tool theo tên. Body = JSON đối số (giống hệt thứ LLM sinh ra), vd:
    /// <c>{ "query": "Hỏa" }</c> cho search_products, <c>{ "workspaceProfileId": "..." }</c> cho recommend_products.
    /// Body rỗng = không tham số. <paramref name="chatboxId"/> chỉ cần cho tool đọc theo phòng (get_chat_partner_info).
    /// Trả về CHÍNH chuỗi output mà AI nhận; nếu tool ném exception → trả 500 kèm chi tiết lỗi thật.
    /// </summary>
    [HttpPost("{name}")]
    public async Task<IActionResult> Run(
        string name, [FromBody] JsonElement? body, [FromQuery] Guid? chatboxId, CancellationToken ct)
    {
        if (!_env.IsDevelopment()) return NotFound();

        var tool = _tools.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));
        if (tool is null)
            return NotFound(new { error = $"Tool '{name}' not found.", available = _tools.Select(t => t.Name) });

        // Đối số: body JSON, mặc định object rỗng nếu không truyền.
        var args = body is { ValueKind: not JsonValueKind.Null and not JsonValueKind.Undefined }
            ? body.Value
            : JsonSerializer.SerializeToElement(new { }, WebJson);

        var ctx = new AiToolContext(CurrentUserId, CurrentUser.Role, CurrentUser.Email, chatboxId);

        try
        {
            var output = await tool.ExecuteAsync(ctx, args, ct);
            // Trả raw JSON string mà AI sẽ nhận (kể cả khi tool tự trả {"error": "..."}).
            return Content(output, "application/json");
        }
        catch (Exception ex)
        {
            // CHÍNH chỗ này là giá trị của endpoint: path AI nuốt exception, ở đây phơi bày đầy đủ.
            return StatusCode(500, new
            {
                tool = tool.Name,
                error = "exception",
                message = ex.Message,
                type = ex.GetType().FullName,
                inner = ex.InnerException?.Message,
                stackTrace = ex.StackTrace,
            });
        }
    }
}

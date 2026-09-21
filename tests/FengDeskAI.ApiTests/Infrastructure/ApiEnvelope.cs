using System.Text.Json;

namespace FengDeskAI.ApiTests.Infrastructure;

/// <summary>
/// Đọc phong bì phản hồi chuẩn của API: <c>{ isSuccess, statusCode, message, errors, data }</c>
/// (xem <c>ServiceResult</c> và <c>ApiControllerBase.ToActionResult</c>).
///
/// Lưu ý: KHÔNG phải phản hồi nào cũng có phong bì này —
/// <list type="bullet">
/// <item>401/403 do middleware phân quyền: thân rỗng</item>
/// <item>400 do model binding của <c>[ApiController]</c>: <c>ValidationProblemDetails</c></item>
/// </list>
/// Vì vậy <see cref="MessageAsync"/> trả chuỗi rỗng thay vì ném, để assert vẫn đọc được.
/// </summary>
public static class ApiEnvelope
{
    /// <summary>Nhánh <c>data</c> đã clone (an toàn sau khi JsonDocument bị dispose).</summary>
    public static async Task<JsonElement> DataAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);

        if (!document.RootElement.TryGetProperty("data", out var data))
            throw new InvalidOperationException($"Phản hồi không có nhánh 'data': {(int)response.StatusCode} {body}");

        return data.Clone();
    }

    /// <summary><c>message</c> của phong bì, hoặc chuỗi rỗng nếu phản hồi không theo phong bì.</summary>
    public static async Task<string> MessageAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(body)) return string.Empty;

        try
        {
            using var document = JsonDocument.Parse(body);
            return document.RootElement.TryGetProperty("message", out var message)
                ? message.GetString() ?? string.Empty
                : string.Empty;
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }

    /// <summary>Mô tả gọn để đưa vào thông điệp assert khi một bước dựng dữ liệu thất bại.</summary>
    public static async Task<string> DescribeAsync(HttpResponseMessage response, string step)
        => $"Bước '{step}' thất bại: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}";
}

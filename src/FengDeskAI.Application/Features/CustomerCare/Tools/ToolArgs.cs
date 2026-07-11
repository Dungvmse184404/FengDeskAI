using System.Text.Json;

namespace FengDeskAI.Application.Features.CustomerCare.Tools;

/// <summary>Đọc tham số tool từ JsonElement (đối số LLM gửi) một cách an toàn.</summary>
internal static class ToolArgs
{
    public static string? GetString(JsonElement e, string name)
        => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() : null;

    public static Guid? GetGuid(JsonElement e, string name)
        => Guid.TryParse(GetString(e, name), out var g) ? g : null;

    public static int? GetInt(JsonElement e, string name)
    {
        if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty(name, out var v)) return null;
        if (v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var n)) return n;
        return int.TryParse(GetString(e, name), out var s) ? s : null;
    }

    /// <summary>
    /// Encoder mặc định escape mọi ký tự non-ASCII → tiếng Việt trong tool result biến thành
    /// chuỗi escape dạng u+0169/u+1EA1... Model nhỏ giải mã escape unicode rất kém
    /// (từng "dịch" sai tên user thành họ khác) → giữ UTF-8 thô để model đọc thẳng.
    /// </summary>
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string Json(object? value) => JsonSerializer.Serialize(value, JsonOptions);

    public static string Error(string message) => Json(new { error = message });
}

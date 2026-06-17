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

    public static string Json(object? value)
        => JsonSerializer.Serialize(value, new JsonSerializerOptions(JsonSerializerDefaults.Web));

    public static string Error(string message) => Json(new { error = message });
}

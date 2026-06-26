using System.Text.Json.Serialization;

namespace FengDeskAI.Infrastructure.Persistence.Seeding;

// Khớp response master-data GHN: { code, message, data: [...] }. Chỉ map field cần để khớp + lấy mã.
internal sealed record GhnEnvelope<T>(
    [property: JsonPropertyName("code")] int Code,
    [property: JsonPropertyName("message")] string? Message,
    [property: JsonPropertyName("data")] List<T>? Data);

internal sealed record GhnProvince(
    [property: JsonPropertyName("ProvinceID")] int ProvinceId,
    [property: JsonPropertyName("ProvinceName")] string ProvinceName,
    [property: JsonPropertyName("Code")] string? Code,
    [property: JsonPropertyName("NameExtension")] List<string>? NameExtension);

internal sealed record GhnDistrict(
    [property: JsonPropertyName("DistrictID")] int DistrictId,
    [property: JsonPropertyName("ProvinceID")] int ProvinceId,
    [property: JsonPropertyName("DistrictName")] string DistrictName,
    // GHN trả 2 mã: "Code" là mã nội bộ GHN, "GovernmentCode" mới là mã GSO (khớp District.Code của ta).
    [property: JsonPropertyName("GovernmentCode")] string? GovernmentCode,
    [property: JsonPropertyName("NameExtension")] List<string>? NameExtension);

internal sealed record GhnWard(
    [property: JsonPropertyName("WardCode")] string WardCode,
    [property: JsonPropertyName("DistrictID")] int DistrictId,
    [property: JsonPropertyName("WardName")] string WardName,
    // GovernmentCode (mã GSO) đôi khi null ở staging → ưu tiên khớp mã, fallback theo tên.
    [property: JsonPropertyName("GovernmentCode")] string? GovernmentCode,
    [property: JsonPropertyName("NameExtension")] List<string>? NameExtension);

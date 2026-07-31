using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FengDeskAI.Infrastructure.ExternalServices.Shipping;

/// <summary>
/// Đọc trường số tiền của nhà vận chuyển dù JSON trả về <c>Number</c> hay <c>String</c>.
/// GHN không nhất quán giữa các endpoint (vd <c>/shipping-order/create</c> trả
/// <c>"total_fee": 20900</c> nhưng tài liệu lại ghi kiểu chuỗi), nên khai cứng một kiểu
/// sẽ ném <see cref="JsonException"/> khi GHN đổi. Giá trị lạ/null → null để caller tự xử lý.
/// </summary>
public sealed class FlexibleDecimalConverter : JsonConverter<decimal?>
{
    public override decimal? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.TokenType switch
        {
            JsonTokenType.Null => null,
            JsonTokenType.Number => reader.TryGetDecimal(out var number) ? number : null,
            JsonTokenType.String => decimal.TryParse(reader.GetString(), NumberStyles.Any,
                CultureInfo.InvariantCulture, out var parsed) ? parsed : null,
            _ => null,
        };

    public override void Write(Utf8JsonWriter writer, decimal? value, JsonSerializerOptions options)
    {
        if (value is { } v) writer.WriteNumberValue(v);
        else writer.WriteNullValue();
    }
}

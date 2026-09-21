using System.Text.Json;
using System.Text.Json.Serialization;

namespace FengDeskAI.WebAPI.Common;

/// <summary>
/// Bọc <see cref="JsonStringEnumConverter"/> để <b>từ chối giá trị enum ngoài miền</b> (DEF-12).
///
/// <para>
/// <c>JsonStringEnumConverter</c> nhận cả tên lẫn số; số thì nó ép thẳng sang enum mà không hỏi
/// <see cref="Enum.IsDefined(Type, object)"/>, nên <c>"locationType": 999</c> đi tới tận DB (cột lưu
/// dạng chuỗi ⇒ "999" nằm lại và mọi phép so khớp phía sau trượt). Ở đây đọc xong thì kiểm định
/// nghĩa; sai thì ném <see cref="JsonException"/> ⇒ model binding trả 400 như mọi JSON hỏng khác.
/// Enum <c>[Flags]</c> (vd role) bỏ qua kiểm — tổ hợp bit hợp lệ không nhất thiết là một tên.
/// </para>
/// </summary>
public sealed class StrictEnumConverterFactory : JsonConverterFactory
{
    private readonly JsonStringEnumConverter _inner = new();

    public override bool CanConvert(Type typeToConvert)
        => _inner.CanConvert(typeToConvert);

    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var inner = _inner.CreateConverter(typeToConvert, options);
        if (inner is null) return null;

        var enumType = Nullable.GetUnderlyingType(typeToConvert) ?? typeToConvert;
        if (enumType.IsDefined(typeof(FlagsAttribute), inherit: false))
            return inner;

        var wrapper = typeof(StrictEnumConverter<>).MakeGenericType(typeToConvert);
        return (JsonConverter)Activator.CreateInstance(wrapper, inner)!;
    }

    private sealed class StrictEnumConverter<T> : JsonConverter<T>
    {
        private readonly JsonConverter<T> _inner;

        public StrictEnumConverter(JsonConverter inner) => _inner = (JsonConverter<T>)inner;

        // Không override HandleNull: base gọi property này NGAY trong constructor, trước khi _inner được gán.
        // Mặc định của JsonConverter<T> (false cho enum/enum?) cũng chính là hành vi của converter gốc.

        public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var value = _inner.Read(ref reader, typeToConvert, options);
            if (value is null) return value;

            var enumType = Nullable.GetUnderlyingType(typeToConvert) ?? typeToConvert;
            if (!Enum.IsDefined(enumType, value))
                throw new JsonException($"Giá trị '{Convert.ToInt64(value)}' không hợp lệ cho {enumType.Name}.");
            return value;
        }

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
            => _inner.Write(writer, value, options);
    }
}

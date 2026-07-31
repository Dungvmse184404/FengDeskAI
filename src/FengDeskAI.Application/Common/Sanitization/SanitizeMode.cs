namespace FengDeskAI.Application.Common.Sanitization;

/// <summary>
/// Mức lọc áp cho text do model sinh ra trước khi tới user. Cùng một text nhưng 2 kênh hiển thị có
/// ràng buộc khác nhau, nên KHÔNG dùng chung một bộ luật.
/// </summary>
public enum SanitizeMode
{
    /// <summary>
    /// Đáp án cuối lưu DB + render Markdown. Phải GIỮ NGUYÊN GUID nằm trong URL (<c>/products/{id}</c>)
    /// để link sản phẩm còn bấm được; chỉ rút gọn GUID "trần" model lỡ phun ra giữa câu.
    /// </summary>
    UserMessage,

    /// <summary>
    /// Text ephemeral đẩy realtime (thinking tail, narration, nhãn tool…). Không có nhu cầu link,
    /// không lưu DB → lọc mạnh tay: mọi GUID (kể cả trong URL), mảnh hex bị cắt dở, email, SĐT,
    /// URL tuyệt đối và tên tool nội bộ.
    /// </summary>
    LiveStream,
}

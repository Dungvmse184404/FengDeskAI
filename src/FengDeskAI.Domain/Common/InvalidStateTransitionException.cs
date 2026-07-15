namespace FengDeskAI.Domain.Common;

/// <summary>
/// Ném khi cố chuyển một aggregate (ticket RMA / refund / công nợ) sang trạng thái không hợp lệ
/// theo máy trạng thái. Tầng Application bắt/hoặc kiểm trước để trả HTTP 409 Conflict.
/// </summary>
public class InvalidStateTransitionException : Exception
{
    public string Aggregate { get; }
    public string From { get; }
    public string To { get; }

    public InvalidStateTransitionException(string aggregate, string from, string to)
        : base($"Không thể chuyển {aggregate} từ '{from}' sang '{to}'.")
    {
        Aggregate = aggregate;
        From = from;
        To = to;
    }
}

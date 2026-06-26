namespace FengDeskAI.Infrastructure.ExternalServices.Shipping;

/// <summary>
/// Cấp & cache token AhaMove (per account). Token hết hạn → gọi <see cref="RefreshAsync"/>
/// để lấy mới (mỗi lần phát token mới làm token cũ vô hiệu — chỉ giữ 1 token).
/// </summary>
public interface IAhamoveTokenProvider
{
    /// <summary>Token đang cache; tự lấy mới nếu chưa có.</summary>
    Task<string> GetAsync(CancellationToken ct = default);

    /// <summary>Buộc lấy token mới (gọi khi nhận 401 NOT_AUTHORIZED).</summary>
    Task<string> RefreshAsync(CancellationToken ct = default);
}

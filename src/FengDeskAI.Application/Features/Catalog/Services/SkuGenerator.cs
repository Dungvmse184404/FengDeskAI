using System.Security.Cryptography;
using System.Text;
using FengDeskAI.Application.Interfaces.Repositories;

namespace FengDeskAI.Application.Features.Catalog.Services;

/// <summary>
/// Mã dạng <c>FD-XXXXXXXX</c> (11 ký tự, dưới giới hạn 20 của cột <c>sku</c>).
///
/// KHÔNG mã hóa thuộc tính nghiệp vụ (tên, hành, placement) vì SKU phải BẤT BIẾN: vendor đổi tên
/// hay đổi hình thức sử dụng là chuyện thường, mà đổi mã thì hỏng đơn hàng/hóa đơn/phiếu kho đã in.
/// KHÔNG tuần tự để không lộ quy mô catalog cho đối thủ.
/// </summary>
public sealed class SkuGenerator : ISkuGenerator
{
    /// <summary>Base32 Crockford — bỏ I, L, O, U để không nhầm với 1/0 khi đọc mã qua điện thoại.</summary>
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    private const string Prefix = "FD-";
    private const int RandomLength = 8;
    private const int MaxAttempts = 5;

    private readonly IUnitOfWork _uow;

    public SkuGenerator(IUnitOfWork uow) => _uow = uow;

    public async Task<string> GenerateAsync(CancellationToken ct = default)
    {
        // 32^8 ≈ 1,1 nghìn tỷ tổ hợp nên gần như không đụng; vẫn thử lại vài lần cho chắc.
        for (int attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var candidate = Prefix + RandomCode(RandomLength);
            if (!await _uow.Products.SkuExistsAsync(candidate, null, ct))
                return candidate;
        }

        throw new InvalidOperationException(
            $"Không sinh được mã SKU duy nhất sau {MaxAttempts} lần thử.");
    }

    private static string RandomCode(int length)
    {
        var sb = new StringBuilder(length);
        for (int i = 0; i < length; i++)
            sb.Append(Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)]);
        return sb.ToString();
    }
}

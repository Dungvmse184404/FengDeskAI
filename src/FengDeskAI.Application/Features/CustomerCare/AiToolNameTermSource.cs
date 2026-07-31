using FengDeskAI.Application.Common.Sanitization;
using FengDeskAI.Application.Interfaces.External;
using Microsoft.Extensions.DependencyInjection;

namespace FengDeskAI.Application.Features.CustomerCare;

/// <summary>
/// Nạp tên các tool đang đăng ký làm từ vựng nhạy cảm — cùng triết lý với
/// <c>AiChatService.LooksLikeToolLeak</c>: KHÔNG hardcode để thêm/xóa tool là tự cập nhật.
/// Tool đăng ký Scoped nên phải mở scope tạm để đọc <see cref="IAiTool.Name"/>; kết quả cache vĩnh viễn
/// (danh sách tool cố định theo vòng đời process).
/// </summary>
public sealed class AiToolNameTermSource : ISensitiveTermSource
{
    private readonly Lazy<IReadOnlyCollection<string>> _terms;

    public AiToolNameTermSource(IServiceScopeFactory scopeFactory)
    {
        _terms = new Lazy<IReadOnlyCollection<string>>(() =>
        {
            using var scope = scopeFactory.CreateScope();
            return scope.ServiceProvider.GetServices<IAiTool>()
                .Select(t => t.Name)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .ToList();
        }, isThreadSafe: true);
    }

    public IReadOnlyCollection<string> Terms => _terms.Value;
}

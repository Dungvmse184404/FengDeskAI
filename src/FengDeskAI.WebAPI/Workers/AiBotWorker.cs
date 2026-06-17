using FengDeskAI.Application.Features.CustomerCare.Services;

namespace FengDeskAI.WebAPI.Workers;

/// <summary>
/// Worker nền xử lý job bot AI: với mỗi chatboxId trong hàng đợi, tạo scope mới rồi gọi
/// <see cref="IAiChatService.RespondInRoomAsync"/>. Tách khỏi request vì LLM chậm.
/// </summary>
public sealed class AiBotWorker : BackgroundService
{
    private readonly AiBotQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AiBotWorker> _logger;

    public AiBotWorker(AiBotQueue queue, IServiceScopeFactory scopeFactory, ILogger<AiBotWorker> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var chatboxId in _queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var ai = scope.ServiceProvider.GetRequiredService<IAiChatService>();
                await ai.RespondInRoomAsync(chatboxId, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[AiBot] Xử lý phòng {ChatboxId} lỗi.", chatboxId);
            }
        }
    }
}

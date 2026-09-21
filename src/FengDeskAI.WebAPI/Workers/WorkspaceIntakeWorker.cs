using FengDeskAI.Application.Features.Workspace.Services;

namespace FengDeskAI.WebAPI.Workers;

/// <summary>
/// Worker nền xử lý job AI intake workspace: với mỗi job trong hàng đợi, tạo scope mới rồi gọi
/// <see cref="IWorkspaceIntakeService.RunJobAsync"/>. Tách khỏi request vì LLM chậm (có ảnh có thể ~80s)
/// nên chạy đồng bộ trong request sẽ làm FE timeout.
/// <para>
/// Chạy <see cref="Concurrency"/> luồng tiêu thụ song song. Trước đây chỉ 1 luồng: 3 user bấm phân
/// tích cùng lúc thì người thứ 3 phải chờ hết 2 job trước — thời gian chờ cộng dồn tuyến tính dù
/// bản thân model không hề chậm hơn.
/// </para>
/// <para>
/// Lưu ý: song song ở đây chỉ giúp khi backend LLM phục vụ được nhiều request cùng lúc. Ollama
/// self-host mặc định xử lý tuần tự (OLLAMA_NUM_PARALLEL) — nếu vậy cần chỉnh cả phía Ollama,
/// nếu không job vẫn xếp hàng ở đó thay vì ở đây.
/// </para>
/// </summary>
public sealed class WorkspaceIntakeWorker : BackgroundService
{
    private readonly WorkspaceIntakeQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WorkspaceIntakeWorker> _logger;

    public WorkspaceIntakeWorker(
        WorkspaceIntakeQueue queue, IServiceScopeFactory scopeFactory, ILogger<WorkspaceIntakeWorker> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>Số job intake chạy đồng thời.</summary>
    private const int Concurrency = 3;

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
        => Task.WhenAll(Enumerable.Range(0, Concurrency).Select(i => ConsumeAsync(i, stoppingToken)));

    private async Task ConsumeAsync(int workerIndex, CancellationToken stoppingToken)
    {
        await foreach (var job in _queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var intake = scope.ServiceProvider.GetRequiredService<IWorkspaceIntakeService>();
                await intake.RunJobAsync(job, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "[WorkspaceIntake] Luồng {Worker} xử lý job {OperationId} lỗi.", workerIndex, job.OperationId);
            }
        }
    }
}

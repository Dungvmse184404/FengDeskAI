using FengDeskAI.Application.Features.Workspace.Services;

namespace FengDeskAI.WebAPI.Workers;

/// <summary>
/// Worker nền xử lý job AI intake workspace: với mỗi job trong hàng đợi, tạo scope mới rồi gọi
/// <see cref="IWorkspaceIntakeService.RunJobAsync"/>. Tách khỏi request vì LLM chậm (có ảnh có thể ~80s)
/// nên chạy đồng bộ trong request sẽ làm FE timeout.
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

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
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
                _logger.LogError(ex, "[WorkspaceIntake] Xử lý job {OperationId} lỗi.", job.OperationId);
            }
        }
    }
}

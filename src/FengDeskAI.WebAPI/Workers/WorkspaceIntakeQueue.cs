using System.Threading.Channels;
using FengDeskAI.Application.Interfaces.External;

namespace FengDeskAI.WebAPI.Workers;

/// <summary>Hàng đợi in-memory (Channel) cho job AI intake workspace. Singleton — writer (request) ↔ reader (worker nền).</summary>
public sealed class WorkspaceIntakeQueue : IWorkspaceIntakeQueue
{
    private readonly Channel<WorkspaceIntakeJob> _channel = Channel.CreateUnbounded<WorkspaceIntakeJob>(
        new UnboundedChannelOptions { SingleReader = true });

    public void Enqueue(WorkspaceIntakeJob job) => _channel.Writer.TryWrite(job);

    public IAsyncEnumerable<WorkspaceIntakeJob> ReadAllAsync(CancellationToken ct) => _channel.Reader.ReadAllAsync(ct);
}

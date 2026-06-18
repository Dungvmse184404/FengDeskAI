using System.Threading.Channels;
using FengDeskAI.Application.Interfaces.External;

namespace FengDeskAI.WebAPI.Workers;

/// <summary>Hàng đợi in-memory (Channel) cho job bot AI. Singleton — writer (request) ↔ reader (worker nền).</summary>
public sealed class AiBotQueue : IAiBotQueue
{
    private readonly Channel<AiBotJob> _channel = Channel.CreateUnbounded<AiBotJob>(
        new UnboundedChannelOptions { SingleReader = true });

    public void Enqueue(AiBotJob job) => _channel.Writer.TryWrite(job);

    public IAsyncEnumerable<AiBotJob> ReadAllAsync(CancellationToken ct) => _channel.Reader.ReadAllAsync(ct);
}

using System.Threading.Channels;
using FengDeskAI.Application.Interfaces.External;

namespace FengDeskAI.WebAPI.Workers;

/// <summary>Hàng đợi in-memory (Channel) cho job bot AI. Singleton — writer (request) ↔ reader (worker nền).</summary>
public sealed class AiBotQueue : IAiBotQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>(
        new UnboundedChannelOptions { SingleReader = true });

    public void Enqueue(Guid chatboxId) => _channel.Writer.TryWrite(chatboxId);

    public IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken ct) => _channel.Reader.ReadAllAsync(ct);
}

using System.Threading.Channels;

namespace PlanIt.Api.Application.Similarity.Embeddings;

// Unbounded Channel<Guid> -- the event-driven trigger path. MediatR notification handlers
// enqueue and return immediately, keeping embedding computation off the request's critical path.
// Singleton: the channel needs to outlive any single request/scope
public class EmbeddingWorkQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>();

    public void Enqueue(Guid workItemId) => _channel.Writer.TryWrite(workItemId);

    public ChannelReader<Guid> Reader => _channel.Reader;
}
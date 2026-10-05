using Pgvector;
using PlanIt.Api.Domain.Entities;
using PlanIt.Api.Domain.Repositories;

namespace PlanIt.Api.Tests.Application.Similarity;

// Hand-written in-memory fake, not Moq -- matches the "no Moq yet" constraint (root CLAUDE.md /
// PlanIt.Api/CLAUDE.md's Testing section) while still letting EmbeddingSimilaritySignal.Prepare
// run against something instead of a real database.
internal class FakeWorkItemEmbeddingRepository : IWorkItemEmbeddingRepository
{
    private readonly Dictionary<Guid, Vector> _onnxVectors;

    public FakeWorkItemEmbeddingRepository(Dictionary<Guid, Vector> onnxVectors) => _onnxVectors = onnxVectors;

    public Task<IReadOnlyDictionary<Guid, Vector>> GetVectorsAsync(EmbeddingSource source, IReadOnlyList<Guid> workItemIds)
    {
        var vectors = source == EmbeddingSource.Onnx ? _onnxVectors : new Dictionary<Guid, Vector>();
        var result = workItemIds
            .Where(vectors.ContainsKey)
            .ToDictionary(id => id, id => vectors[id]);

        return Task.FromResult<IReadOnlyDictionary<Guid, Vector>>(result);
    }

    public Task UpsertAsync(EmbeddingSource source, Guid workItemId, Vector vector, string sourceText, DateTimeOffset computedAt) =>
        throw new NotSupportedException("Not needed for EmbeddingSimilaritySignal tests.");

    public Task<IReadOnlyList<Guid>> GetStaleOrMissingWorkItemIdsAsync() =>
        throw new NotSupportedException("Not needed for EmbeddingSimilaritySignal tests.");
}

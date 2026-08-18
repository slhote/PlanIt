using Microsoft.Extensions.Options;
using Pgvector;
using PlanIt.Api.Application.Similarity;
using PlanIt.Api.Startup.Options;

namespace PlanIt.Api.Tests.Application.Similarity;

public class EmbeddingSimilaritySignalTests
{
    private static EmbeddingSimilaritySignal CreateSignal(Dictionary<Guid, Vector> onnxVectors, string embeddingSource = "Onnx")
    {
        var repository = new FakeWorkItemEmbeddingRepository(onnxVectors);
        var options = Options.Create(new SimilarWorkItemsOptions { EmbeddingSource = embeddingSource });
        return new EmbeddingSimilaritySignal(repository, options);
    }

    [Fact]
    public void IdenticalVectors_ScoreOne()
    {
        var reference = WorkItemTestFactory.Create();
        var candidate = WorkItemTestFactory.Create();
        var vector = new Vector(new float[] { 1f, 0f, 0f });

        var signal = CreateSignal(new Dictionary<Guid, Vector> { [reference.Id] = vector, [candidate.Id] = vector });
        signal.Prepare(reference, [candidate]);

        Assert.Equal(1.0, signal.Score(candidate, reference), precision: 10);
    }

    [Fact]
    public void OrthogonalVectors_ScoreZero()
    {
        var reference = WorkItemTestFactory.Create();
        var candidate = WorkItemTestFactory.Create();

        var signal = CreateSignal(new Dictionary<Guid, Vector>
        {
            [reference.Id] = new Vector(new float[] { 1f, 0f }),
            [candidate.Id] = new Vector(new float[] { 0f, 1f }),
        });
        signal.Prepare(reference, [candidate]);

        Assert.Equal(0.0, signal.Score(candidate, reference), precision: 10);
    }

    // Raw cosine similarity ranges [-1.0, 1.0], but ISimilaritySignal.Score's documented contract
    // is [0.0, 1.0] -- opposite vectors must clamp to 0.0, not leak a negative score into the
    // weighted sum (planit-similar-tasks-semantic-embeddings.md).
    [Fact]
    public void OppositeVectors_ClampToZero_NotNegative()
    {
        var reference = WorkItemTestFactory.Create();
        var candidate = WorkItemTestFactory.Create();

        var signal = CreateSignal(new Dictionary<Guid, Vector>
        {
            [reference.Id] = new Vector(new float[] { 1f, 0f }),
            [candidate.Id] = new Vector(new float[] { -1f, 0f }),
        });
        signal.Prepare(reference, [candidate]);

        Assert.Equal(0.0, signal.Score(candidate, reference), precision: 10);
    }

    [Fact]
    public void NonUnitVectors_StillNormalizedByMagnitude()
    {
        var reference = WorkItemTestFactory.Create();
        var candidate = WorkItemTestFactory.Create();

        // Same direction, different magnitude -- cosine similarity ignores magnitude entirely.
        var signal = CreateSignal(new Dictionary<Guid, Vector>
        {
            [reference.Id] = new Vector(new float[] { 3f, 4f }),
            [candidate.Id] = new Vector(new float[] { 6f, 8f }),
        });
        signal.Prepare(reference, [candidate]);

        Assert.Equal(1.0, signal.Score(candidate, reference), precision: 10);
    }

    [Fact]
    public void MissingVector_ScoresZero_CandidateNotExcluded()
    {
        var reference = WorkItemTestFactory.Create();
        var candidate = WorkItemTestFactory.Create();

        // Only the reference has a computed embedding -- the candidate's is missing (not yet
        // computed, or the sweep hasn't caught up).
        var signal = CreateSignal(new Dictionary<Guid, Vector> { [reference.Id] = new Vector(new float[] { 1f, 0f }) });
        signal.Prepare(reference, [candidate]);

        Assert.Equal(0.0, signal.Score(candidate, reference));
    }

    [Fact]
    public void ReadsFromTheConfiguredSource()
    {
        var reference = WorkItemTestFactory.Create();
        var candidate = WorkItemTestFactory.Create();
        var vector = new Vector(new float[] { 1f, 0f });

        // Vectors exist under Onnx, but EmbeddingSource is configured as Python -- the fake
        // repository's GetVectorsAsync returns nothing for Python, so this should score 0.0.
        var signal = CreateSignal(
            new Dictionary<Guid, Vector> { [reference.Id] = vector, [candidate.Id] = vector },
            embeddingSource: "Python");
        signal.Prepare(reference, [candidate]);

        Assert.Equal(0.0, signal.Score(candidate, reference));
    }
}

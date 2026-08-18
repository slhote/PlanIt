namespace PlanIt.Api.Domain.Entities;

// Which generator produced/reads a WorkItem embedding. Matches SimilarWorkItemsOptions
// .EmbeddingSource ("Onnx" | "Python") — see planit-similar-tasks-semantic-embeddings.md.
public enum EmbeddingSource
{
    Onnx,
    Python,
}

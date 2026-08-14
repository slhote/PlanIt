namespace PlanIt.Api.Application.Similarity;

// Pluggable text-similarity algorithm behind LexicalTextSignal, selected at startup via
// SimilarTasksOptions.LexicalStrategy (planit-similar-tasks-lexical-metadata.md).
public interface ILexicalSimilarityStrategy
{
    // Called once per request, before any Score calls, with every candidate's tokens keyed
    // by WorkItem.Id. Corpus-wide strategies (TF-IDF) build their document-frequency stats
    // here; purely pairwise strategies (Jaccard) can no-op it.
    void Prepare(IReadOnlyList<string> referenceTokens, IReadOnlyList<IReadOnlyList<string>> candidateTokenSets) { }

    double Score(IReadOnlyList<string> candidateTokens, IReadOnlyList<string> referenceTokens);
}

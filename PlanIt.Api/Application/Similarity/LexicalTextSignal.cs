using PlanIt.Api.Domain.Entities;

namespace PlanIt.Api.Application.Similarity;

// Tokenizes Title + Description and delegates scoring to the configured
// ILexicalSimilarityStrategy (Jaccard or TF-IDF, selected via SimilarTasks:LexicalStrategy).
public class LexicalTextSignal : ISimilaritySignal
{
    private readonly ILexicalSimilarityStrategy _strategy;
    private readonly Dictionary<Guid, IReadOnlyList<string>> _tokensById = new();
    private IReadOnlyList<string> _referenceTokens = [];

    public LexicalTextSignal(ILexicalSimilarityStrategy strategy) => _strategy = strategy;

    public void Prepare(WorkItem reference, IReadOnlyList<WorkItem> candidates)
    {
        _referenceTokens = Tokenizer.Tokenize(reference.Title, reference.Description);

        _tokensById.Clear();
        foreach (var candidate in candidates)
            _tokensById[candidate.Id] = Tokenizer.Tokenize(candidate.Title, candidate.Description);

        _strategy.Prepare(_referenceTokens, _tokensById.Values.ToList());
    }

    public double Score(WorkItem candidate, WorkItem reference)
    {
        var candidateTokens = _tokensById.TryGetValue(candidate.Id, out var tokens)
            ? tokens
            : Tokenizer.Tokenize(candidate.Title, candidate.Description);

        return _strategy.Score(candidateTokens, _referenceTokens);
    }
}

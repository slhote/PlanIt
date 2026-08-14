namespace PlanIt.Api.Application.Similarity;

// TF-IDF cosine similarity. Needs corpus-wide document frequencies, computed once in
// Prepare across the reference + full candidate pool for this request, then reused by
// every Score call.
public class TfIdfLexicalStrategy : ILexicalSimilarityStrategy
{
    private Dictionary<string, double> _idf = new();

    public void Prepare(IReadOnlyList<string> referenceTokens, IReadOnlyList<IReadOnlyList<string>> candidateTokenSets)
    {
        var documents = new List<IReadOnlyList<string>>(candidateTokenSets.Count + 1) { referenceTokens };
        documents.AddRange(candidateTokenSets);

        var documentCount = documents.Count;
        var documentFrequency = new Dictionary<string, int>();

        foreach (var document in documents)
        {
            foreach (var token in new HashSet<string>(document))
                documentFrequency[token] = documentFrequency.GetValueOrDefault(token) + 1;
        }

        // +1 (smoothed IDF) so a term appearing in every document still carries some weight
        // rather than dropping to zero.
        _idf = documentFrequency.ToDictionary(
            kvp => kvp.Key,
            kvp => Math.Log((double)documentCount / kvp.Value) + 1.0);
    }

    public double Score(IReadOnlyList<string> candidateTokens, IReadOnlyList<string> referenceTokens)
    {
        if (referenceTokens.Count == 0 || candidateTokens.Count == 0)
            return 0.0;

        var referenceVector = BuildTfIdfVector(referenceTokens);
        var candidateVector = BuildTfIdfVector(candidateTokens);

        return CosineSimilarity(referenceVector, candidateVector);
    }

    private Dictionary<string, double> BuildTfIdfVector(IReadOnlyList<string> tokens)
    {
        var termFrequency = new Dictionary<string, int>();
        foreach (var token in tokens)
            termFrequency[token] = termFrequency.GetValueOrDefault(token) + 1;

        var totalTerms = tokens.Count;
        return termFrequency.ToDictionary(
            kvp => kvp.Key,
            kvp => (double)kvp.Value / totalTerms * _idf.GetValueOrDefault(kvp.Key, 0.0));
    }

    private static double CosineSimilarity(Dictionary<string, double> a, Dictionary<string, double> b)
    {
        var dot = 0.0;
        var normA = 0.0;
        var normB = 0.0;

        foreach (var (term, weight) in a)
        {
            normA += weight * weight;
            if (b.TryGetValue(term, out var otherWeight))
                dot += weight * otherWeight;
        }

        foreach (var weight in b.Values)
            normB += weight * weight;

        return normA == 0.0 || normB == 0.0 ? 0.0 : dot / (Math.Sqrt(normA) * Math.Sqrt(normB));
    }
}

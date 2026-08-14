namespace PlanIt.Api.Application.Similarity;

// Token-set Jaccard overlap. Purely pairwise — Prepare is unnecessary (default no-op).
public class JaccardLexicalStrategy : ILexicalSimilarityStrategy
{
    public double Score(IReadOnlyList<string> candidateTokens, IReadOnlyList<string> referenceTokens)
    {
        if (referenceTokens.Count == 0 || candidateTokens.Count == 0)
            return 0.0;

        var referenceSet = new HashSet<string>(referenceTokens);
        var candidateSet = new HashSet<string>(candidateTokens);

        var intersectionCount = referenceSet.Intersect(candidateSet).Count();
        var unionCount = referenceSet.Union(candidateSet).Count();

        return unionCount == 0 ? 0.0 : (double)intersectionCount / unionCount;
    }
}

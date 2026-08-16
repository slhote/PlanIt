namespace PlanIt.Api.Startup.Options;

public class SimilarTasksOptions
{
    public const string SectionName = "SimilarTasks";

    // "Jaccard" or "TfIdf" — selects the ILexicalSimilarityStrategy implementation at startup.
    public string LexicalStrategy { get; set; } = "Jaccard";

    public int MaxResults { get; set; } = 5;

    public double MinScoreThreshold { get; set; } = 0.0;

    // Signal name -> weight, combined by WeightedSimilarityScorer.
    public Dictionary<string, double> Weights { get; set; } = new();
}

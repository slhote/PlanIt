namespace PlanIt.Api.Startup.Options;

public class SimilarWorkItemsOptions
{
    public const string SectionName = "SimilarWorkItems";

    // "Jaccard" or "TfIdf" — selects the ILexicalSimilarityStrategy implementation at startup.
    public string LexicalStrategy { get; set; } = "Jaccard";

    // How many work items are shown from similar results set
    public int MaxResults { get; set; } = 5;

    // Scale 0.0-1.0, not a percentage — weighted scores are computed from signals that each score 0.0-1.0 and weights that sum to 1.0.
    public double MinScoreThreshold { get; set; } = 0.0;

    // Signal name -> weight, combined by WeightedSimilarityScorer.
    public Dictionary<string, double> Weights { get; set; } = new();
}

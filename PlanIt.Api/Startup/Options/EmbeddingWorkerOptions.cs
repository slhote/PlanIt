namespace PlanIt.Api.Startup.Options;

public class EmbeddingWorkerOptions
{
    public const string SectionName = "EmbeddingWorker";

    // The periodic sweep's catch-up interval -- also doubles as the initial backfill delay on a
    // fresh deploy (planit-similar-tasks-semantic-embeddings.md).
    public int SweepIntervalMinutes { get; set; } = 5;
}

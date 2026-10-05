namespace PlanIt.Api.Startup.Options;

public class PythonEmbeddingOptions
{
    public const string SectionName = "PythonEmbedding";

    // docker-compose service name -- matches the "embedding-service" entry added to docker-compose.yml 
    public string BaseUrl { get; set; } = "http://localhost:8000";

    public int RetryAttempts { get; set; } = 3;
    public int RetryBaseDelayMilliseconds { get; set; } = 250;
}

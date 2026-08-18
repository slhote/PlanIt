namespace PlanIt.Api.Startup.Options;

public class OnnxEmbeddingOptions
{
    public const string SectionName = "OnnxEmbedding";

    // sentence-transformers/all-MiniLM-L6-v2 exported to ONNX (one-time offline step; the .onnx
    // file itself is not generated at runtime or by this repo -- see
    // planit-similar-tasks-semantic-embeddings.md).
    public string ModelPath { get; set; } = "Models/all-MiniLM-L6-v2/model.onnx";
    public string VocabPath { get; set; } = "Models/all-MiniLM-L6-v2/vocab.txt";
    public int MaxTokens { get; set; } = 256;
}

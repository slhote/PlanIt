namespace PlanIt.Api.Startup.Options;

public class OnnxEmbeddingOptions
{
    public const string SectionName = "OnnxEmbedding";

    // sentence-transformers/all-MiniLM-L6-v2 exported to ONNX; the .onnx file itself is not generated at runtime or by this repo
    public string ModelPath { get; set; } = "Models/all-MiniLM-L6-v2/model.onnx";
    public string VocabPath { get; set; } = "Models/all-MiniLM-L6-v2/vocab.txt";
    // The maximum sequence length ONNX model all-MiniLM-L6-v2 was trained with.
    // Supports roughly 1000-1500 words, suitable for most title + descriptions for this project right now.
    // Edge case: If the size allowed for title + description continues to grow then we might need to either
    // (a) use a larger model (slower, more memory), (b) chunk long descriptions and embed them separately,
    // or (c) add a warning when truncation happens.
    public int MaxTokens { get; set; } = 256;
}
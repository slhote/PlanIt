using Microsoft.Extensions.Options;
using PlanIt.Api.Startup.Options;

namespace PlanIt.Api.Startup.Validation;

// Fail-fast at startup, not on first request -- the model/vocab files are local assets, not a
// network dependency, so a missing file is a deployment mistake, not a runtime condition to retry 
public class OnnxEmbeddingOptionsValidator : IValidateOptions<OnnxEmbeddingOptions>
{
    public ValidateOptionsResult Validate(string? name, OnnxEmbeddingOptions options)
    {
        if (!File.Exists(options.ModelPath))
        {
            return ValidateOptionsResult.Fail(
                $"{OnnxEmbeddingOptions.SectionName}:{nameof(OnnxEmbeddingOptions.ModelPath)} " +
                $"'{options.ModelPath}' does not exist. See planit-similar-tasks-semantic-embeddings.md " +
                "for how to obtain the ONNX-exported model file.");
        }

        if (!File.Exists(options.VocabPath))
        {
            return ValidateOptionsResult.Fail(
                $"{OnnxEmbeddingOptions.SectionName}:{nameof(OnnxEmbeddingOptions.VocabPath)} " +
                $"'{options.VocabPath}' does not exist.");
        }

        return ValidateOptionsResult.Success;
    }
}
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using PlanIt.Api.Startup.Options;

namespace PlanIt.Api.Application.Similarity.Embeddings;

// Calls the separate Python microservice -- PlanIt.EmbeddingService, FastAPI + sentence-transformers/all-mpnet-base-v2, 768-dim.
// Unlike the ONNX path, this is a network call across processes even locally, so it's treated as flaky: bounded retry with
// exponential backoff, then the caller decides what to do with the exception (the background worker's catch-log-skip policy)
public class PythonEmbeddingGenerator : IEmbeddingGenerator
{
    private readonly HttpClient _httpClient;
    private readonly PythonEmbeddingOptions _options;

    public PythonEmbeddingGenerator(HttpClient httpClient, IOptions<PythonEmbeddingOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default)
    {
        Exception? lastException = null;

        for (var attempt = 1; attempt <= _options.RetryAttempts; attempt++)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("/embed", new { text }, cancellationToken);
                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadFromJsonAsync<EmbedResponse>(cancellationToken)
                    ?? throw new InvalidOperationException("Embedding service returned an empty response.");

                return result.Vector;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                lastException = ex;
                if (attempt < _options.RetryAttempts)
                {
                    var delay = TimeSpan.FromMilliseconds(_options.RetryBaseDelayMilliseconds * Math.Pow(2, attempt - 1));
                    await Task.Delay(delay, cancellationToken);
                }
            }
        }

        throw new InvalidOperationException(
            $"Embedding service call failed after {_options.RetryAttempts} attempts.", lastException);
    }

    private record EmbedResponse(float[] Vector, int Dimensions);
}

using Microsoft.Extensions.Options;
using PlanIt.Api.Domain.Entities;
using PlanIt.Api.Startup.Options;

namespace PlanIt.Api.Application.Similarity;

public class WeightedSimilarityScorer(IEnumerable<ISimilaritySignal> signals, IOptions<SimilarTasksOptions> options)
{
    public IReadOnlyList<(WorkItem WorkItem, double Score)> Rank(WorkItem reference, IReadOnlyList<WorkItem> candidates)
    {
        var signalList = signals.ToList();
        foreach (var signal in signalList)
            signal.Prepare(reference, candidates);

        var settings = options.Value;

        return candidates
            .Select(candidate => (WorkItem: candidate, Score: ComputeWeightedScore(candidate, reference, signalList, settings.Weights)))
            .Where(r => r.Score >= settings.MinScoreThreshold)
            .OrderByDescending(r => r.Score)
            .Take(settings.MaxResults)
            .ToList();
    }

    private static double ComputeWeightedScore(
        WorkItem candidate,
        WorkItem reference,
        IReadOnlyList<ISimilaritySignal> signals,
        IReadOnlyDictionary<string, double> weights)
    {
        var total = 0.0;
        foreach (var signal in signals)
        {
            var weight = weights.GetValueOrDefault(signal.Name, 0.0);
            if (weight == 0.0)
                continue;

            total += weight * signal.Score(candidate, reference);
        }

        return total;
    }
}

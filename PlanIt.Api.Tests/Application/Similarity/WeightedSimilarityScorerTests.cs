using Microsoft.Extensions.Options;
using PlanIt.Api.Application.Similarity;
using PlanIt.Api.Domain.Entities;
using PlanIt.Api.Startup.Options;

namespace PlanIt.Api.Tests.Application.Similarity;

public class WeightedSimilarityScorerTests
{
    [Fact]
    public void RanksDescendingByWeightedScore_AndAppliesMinScoreThreshold()
    {
        var reference = WorkItemTestFactory.Create();
        var high = WorkItemTestFactory.Create();
        var mid = WorkItemTestFactory.Create();
        var belowThreshold = WorkItemTestFactory.Create();
        var candidates = new[] { belowThreshold, high, mid };

        var signal = new StubSignal("A", new Dictionary<Guid, double>
        {
            [high.Id] = 1.0,
            [mid.Id] = 0.5,
            [belowThreshold.Id] = 0.05,
        });

        var scorer = CreateScorer([signal], new SimilarWorkItemsOptions
        {
            MaxResults = 5,
            MinScoreThreshold = 0.1,
            Weights = new Dictionary<string, double> { ["A"] = 1.0 },
        });

        var ranked = scorer.Rank(reference, candidates);

        Assert.Equal(2, ranked.Count);
        Assert.Equal(high.Id, ranked[0].WorkItem.Id);
        Assert.Equal(mid.Id, ranked[1].WorkItem.Id);
    }

    [Fact]
    public void CapsResultsAtMaxResults()
    {
        var reference = WorkItemTestFactory.Create();
        var candidates = Enumerable.Range(0, 10).Select(_ => WorkItemTestFactory.Create()).ToList();
        var signal = new StubSignal("A", candidates.ToDictionary(c => c.Id, _ => 1.0));

        var scorer = CreateScorer([signal], new SimilarWorkItemsOptions
        {
            MaxResults = 3,
            MinScoreThreshold = 0.0,
            Weights = new Dictionary<string, double> { ["A"] = 1.0 },
        });

        var ranked = scorer.Rank(reference, candidates);

        Assert.Equal(3, ranked.Count);
    }

    [Fact]
    public void SignalWithNoConfiguredWeight_DoesNotContributeToScore()
    {
        var reference = WorkItemTestFactory.Create();
        var candidate = WorkItemTestFactory.Create();
        var candidates = new[] { candidate };
        var signal = new StubSignal("Unweighted", new Dictionary<Guid, double> { [candidate.Id] = 1.0 });

        var scorer = CreateScorer([signal], new SimilarWorkItemsOptions
        {
            MaxResults = 5,
            MinScoreThreshold = 0.0,
            Weights = new Dictionary<string, double>(),
        });

        var ranked = scorer.Rank(reference, candidates);

        Assert.Single(ranked);
        Assert.Equal(0.0, ranked[0].Score);
    }

    [Fact]
    public void CombinesMultipleSignalsByWeightedSum()
    {
        var reference = WorkItemTestFactory.Create();
        var candidate = WorkItemTestFactory.Create();
        var candidates = new[] { candidate };

        var signalA = new StubSignal("A", new Dictionary<Guid, double> { [candidate.Id] = 1.0 });
        var signalB = new StubSignal("B", new Dictionary<Guid, double> { [candidate.Id] = 0.5 });

        var scorer = CreateScorer([signalA, signalB], new SimilarWorkItemsOptions
        {
            MaxResults = 5,
            MinScoreThreshold = 0.0,
            Weights = new Dictionary<string, double> { ["A"] = 0.6, ["B"] = 0.4 },
        });

        var ranked = scorer.Rank(reference, candidates);

        Assert.Equal(0.6 * 1.0 + 0.4 * 0.5, ranked[0].Score, precision: 10);
    }

    private static WeightedSimilarityScorer CreateScorer(IEnumerable<ISimilaritySignal> signals, SimilarWorkItemsOptions options) =>
        new(signals, Options.Create(options));

    private class StubSignal(string name, Dictionary<Guid, double> scoresByCandidateId) : ISimilaritySignal
    {
        public string Name => name;

        public double Score(WorkItem candidate, WorkItem reference) =>
            scoresByCandidateId.GetValueOrDefault(candidate.Id, 0.0);
    }
}

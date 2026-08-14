using PlanIt.Api.Application.Similarity;

namespace PlanIt.Api.Tests.Application.Similarity;

public class TagOverlapSignalTests
{
    private readonly TagOverlapSignal _signal = new();

    [Fact]
    public void FullOverlap_ScoresOne()
    {
        var reference = WorkItemTestFactory.Create(tags: ["bug", "urgent"]);
        var candidate = WorkItemTestFactory.Create(tags: ["bug", "urgent"]);

        Assert.Equal(1.0, _signal.Score(candidate, reference));
    }

    [Fact]
    public void PartialOverlap_ScoresJaccardRatio()
    {
        var reference = WorkItemTestFactory.Create(tags: ["bug", "urgent"]);
        var candidate = WorkItemTestFactory.Create(tags: ["bug", "backend"]);

        // intersection {bug} = 1, union {bug, urgent, backend} = 3
        Assert.Equal(1.0 / 3.0, _signal.Score(candidate, reference), precision: 5);
    }

    [Fact]
    public void NoOverlap_ScoresZero()
    {
        var reference = WorkItemTestFactory.Create(tags: ["bug"]);
        var candidate = WorkItemTestFactory.Create(tags: ["feature"]);

        Assert.Equal(0.0, _signal.Score(candidate, reference));
    }

    [Fact]
    public void EitherSideHasNoTags_ScoresZero()
    {
        var reference = WorkItemTestFactory.Create(tags: []);
        var candidate = WorkItemTestFactory.Create(tags: ["bug"]);

        Assert.Equal(0.0, _signal.Score(candidate, reference));
    }
}

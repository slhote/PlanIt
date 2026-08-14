using PlanIt.Api.Application.Similarity;

namespace PlanIt.Api.Tests.Application.Similarity;

public class AssigneeMatchSignalTests
{
    private readonly AssigneeMatchSignal _signal = new();

    [Fact]
    public void SameNonNullAssignee_ScoresOne()
    {
        var userId = Guid.NewGuid();
        var reference = WorkItemTestFactory.Create(assigneeId: userId);
        var candidate = WorkItemTestFactory.Create(assigneeId: userId);

        Assert.Equal(1.0, _signal.Score(candidate, reference));
    }

    [Fact]
    public void DifferentAssignee_ScoresZero()
    {
        var reference = WorkItemTestFactory.Create(assigneeId: Guid.NewGuid());
        var candidate = WorkItemTestFactory.Create(assigneeId: Guid.NewGuid());

        Assert.Equal(0.0, _signal.Score(candidate, reference));
    }

    [Fact]
    public void BothUnassigned_ScoresZero()
    {
        var reference = WorkItemTestFactory.Create();
        var candidate = WorkItemTestFactory.Create();

        Assert.Equal(0.0, _signal.Score(candidate, reference));
    }
}

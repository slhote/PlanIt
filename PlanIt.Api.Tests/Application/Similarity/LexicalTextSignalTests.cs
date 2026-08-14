using PlanIt.Api.Application.Similarity;

namespace PlanIt.Api.Tests.Application.Similarity;

// Hand-written near-miss/match pairs, per the planning doc's Step 1 recommendation for an
// informal similarity test set.
public class LexicalTextSignalTests
{
    [Theory]
    [InlineData(typeof(JaccardLexicalStrategy))]
    [InlineData(typeof(TfIdfLexicalStrategy))]
    public void IdenticalText_ScoresHigh(Type strategyType)
    {
        var signal = new LexicalTextSignal(CreateStrategy(strategyType));

        var reference = WorkItemTestFactory.Create(title: "Fix login bug", description: "Users cannot sign in");
        var candidate = WorkItemTestFactory.Create(title: "Fix login bug", description: "Users cannot sign in");
        var candidates = new[] { candidate };

        signal.Prepare(reference, candidates);
        var score = signal.Score(candidate, reference);

        Assert.True(score > 0.9, $"Expected a near-1.0 score for identical text, got {score}.");
    }

    [Theory]
    [InlineData(typeof(JaccardLexicalStrategy))]
    [InlineData(typeof(TfIdfLexicalStrategy))]
    public void UnrelatedText_ScoresZero(Type strategyType)
    {
        var signal = new LexicalTextSignal(CreateStrategy(strategyType));

        var reference = WorkItemTestFactory.Create(title: "Fix login bug", description: "Users cannot sign in");
        var candidate = WorkItemTestFactory.Create(
            title: "Update payment invoice template",
            description: "Redesign the PDF layout");
        var candidates = new[] { candidate };

        signal.Prepare(reference, candidates);
        var score = signal.Score(candidate, reference);

        Assert.Equal(0.0, score);
    }

    [Fact]
    public void Jaccard_IsWordOrderIndependent()
    {
        var signal = new LexicalTextSignal(new JaccardLexicalStrategy());

        var reference = WorkItemTestFactory.Create(title: "Fix login bug");
        var candidate = WorkItemTestFactory.Create(title: "Login bug fix");
        var candidates = new[] { candidate };

        signal.Prepare(reference, candidates);
        var score = signal.Score(candidate, reference);

        Assert.Equal(1.0, score);
    }

    [Fact]
    public void TfIdf_RanksDistinctiveSharedTermsAboveCorpusCommonOnes()
    {
        var signal = new LexicalTextSignal(new TfIdfLexicalStrategy());

        var reference = WorkItemTestFactory.Create(title: "Fix login authentication bug");
        // "fix"/"bug" appear across the whole corpus (low IDF weight); "login"/"authentication"
        // are shared only between the reference and distinctiveCandidate (high IDF weight).
        var commonCandidate = WorkItemTestFactory.Create(title: "Fix bug in export");
        var distinctiveCandidate = WorkItemTestFactory.Create(title: "Fix login authentication timeout");
        var otherCandidate = WorkItemTestFactory.Create(title: "Fix bug in reporting");
        var candidates = new[] { commonCandidate, distinctiveCandidate, otherCandidate };

        signal.Prepare(reference, candidates);

        var commonScore = signal.Score(commonCandidate, reference);
        var distinctiveScore = signal.Score(distinctiveCandidate, reference);

        Assert.True(
            distinctiveScore > commonScore,
            $"Expected the candidate sharing distinctive terms ({distinctiveScore}) to outrank the one " +
            $"sharing only corpus-common terms ({commonScore}).");
    }

    private static ILexicalSimilarityStrategy CreateStrategy(Type strategyType) =>
        (ILexicalSimilarityStrategy)Activator.CreateInstance(strategyType)!;
}

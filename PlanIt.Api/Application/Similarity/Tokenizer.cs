using System.Text.RegularExpressions;

namespace PlanIt.Api.Application.Similarity;

// Lowercase, strip punctuation, split on whitespace, drop stopwords and empty tokens.
// Deliberately simple — no stemming/lemmatization, per the planning doc's "start at the
// cheapest rung" recommendation for lexical similarity.
public static partial class Tokenizer
{
    private static readonly HashSet<string> Stopwords = new(StringComparer.Ordinal)
    {
        "a", "an", "the", "and", "or", "but", "if", "then", "so", "of", "to", "in", "on",
        "for", "with", "at", "by", "from", "up", "about", "into", "over", "after", "is",
        "are", "was", "were", "be", "been", "being", "it", "its", "this", "that", "these",
        "those", "as", "not", "no", "do", "does", "did", "will", "would", "can", "could",
        "should", "we", "you", "your", "i", "our",
    };

    public static IReadOnlyList<string> Tokenize(string? title, string? description)
    {
        var text = $"{title} {description}";
        var tokens = NonWordCharacters().Split(text.ToLowerInvariant());

        return tokens
            .Where(t => t.Length > 0 && !Stopwords.Contains(t))
            .ToList();
    }

    [GeneratedRegex(@"[^\p{L}\p{Nd}]+")]
    private static partial Regex NonWordCharacters();
}

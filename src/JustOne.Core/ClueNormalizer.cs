using System.Globalization;
using System.Text;

namespace JustOne.Core;

/// <summary>
/// Canonicalizes clues for duplicate detection and guess matching:
/// trim, collapse internal whitespace, strip diacritics, lowercase, strip
/// leading/trailing punctuation.
/// Multi-word clues (e.g. proper nouns like "New York") are allowed; the group
/// polices borderline clues via manual cancellation during review.
/// Internal hyphens/apostrophes are kept ("ice-cream" stays distinct from "icecream").
/// </summary>
public static class ClueNormalizer
{
    public const int MaxClueLength = 30;
    public const int MaxClueWords = 5;

    /// <summary>Trims and collapses runs of internal whitespace to single spaces.</summary>
    public static string Clean(string input) =>
        string.Join(' ', input.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    public static string Normalize(string input)
    {
        var formD = Clean(input).Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(formD.Length);
        foreach (var c in formD)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }

        var s = sb.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();

        var start = 0;
        var end = s.Length;
        while (start < end && char.IsPunctuation(s[start]))
        {
            start++;
        }

        while (end > start && char.IsPunctuation(s[end - 1]))
        {
            end--;
        }

        return s[start..end];
    }

    /// <summary>Returns a player-facing error message, or null if the clue is acceptable.</summary>
    public static string? ValidateWord(string word)
    {
        if (string.IsNullOrWhiteSpace(word))
        {
            return "Enter a clue first.";
        }

        var cleaned = Clean(word);
        if (cleaned.Length > MaxClueLength)
        {
            return $"That's too long ({MaxClueLength} characters max).";
        }

        var wordCount = cleaned.Count(c => c == ' ') + 1;
        if (wordCount > MaxClueWords)
        {
            return $"That's too many words ({MaxClueWords} max).";
        }

        return Normalize(cleaned).Any(char.IsLetterOrDigit) ? null : "That clue doesn't contain any letters.";
    }
}

using System.Globalization;
using System.Text;

namespace JustOne.Core;

/// <summary>
/// Canonicalizes words for duplicate detection and guess matching:
/// trim, strip diacritics, lowercase, strip leading/trailing punctuation.
/// Internal hyphens/apostrophes are kept ("ice-cream" stays distinct from "icecream";
/// near-duplicates are handled by the players' manual cancellation).
/// </summary>
public static class ClueNormalizer
{
    public const int MaxWordLength = 30;

    public static string Normalize(string input)
    {
        var formD = input.Trim().Normalize(NormalizationForm.FormD);
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

    /// <summary>Returns a player-facing error message, or null if the word is acceptable.</summary>
    public static string? ValidateWord(string word)
    {
        if (string.IsNullOrWhiteSpace(word))
        {
            return "Enter a word first.";
        }

        var trimmed = word.Trim();
        if (trimmed.Any(char.IsWhiteSpace))
        {
            return "It has to be a single word.";
        }

        if (trimmed.Length > MaxWordLength)
        {
            return $"That's too long ({MaxWordLength} characters max).";
        }

        return Normalize(trimmed).Length == 0 ? "That word doesn't contain any letters." : null;
    }
}

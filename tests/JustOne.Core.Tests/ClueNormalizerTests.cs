using JustOne.Core;

namespace JustOne.Core.Tests;

public class ClueNormalizerTests
{
    [Test]
    [Arguments("  Apple  ", "apple")]
    [Arguments("BANANA", "banana")]
    [Arguments("Café", "cafe")]
    [Arguments("naïve", "naive")]
    [Arguments("\"quoted\"", "quoted")]
    [Arguments("wow!", "wow")]
    [Arguments("...dots...", "dots")]
    public async Task Normalize_canonicalizes(string input, string expected)
    {
        await Assert.That(ClueNormalizer.Normalize(input)).IsEqualTo(expected);
    }

    [Test]
    public async Task Normalize_keeps_internal_hyphens()
    {
        await Assert.That(ClueNormalizer.Normalize("Ice-Cream")).IsEqualTo("ice-cream");
        await Assert.That(ClueNormalizer.Normalize("ice-cream")).IsNotEqualTo(ClueNormalizer.Normalize("icecream"));
    }

    [Test]
    [Arguments("apple")]
    [Arguments("ice-cream")]
    [Arguments("O'Brien")]
    [Arguments("New York")]           // multi-word proper noun
    [Arguments("  Statue  of Liberty ")] // messy internal/edge whitespace, still 3 words
    [Arguments("tab\there")]          // any whitespace counts as a word separator
    public async Task ValidateWord_accepts_words_and_short_phrases(string word)
    {
        await Assert.That(ClueNormalizer.ValidateWord(word)).IsNull();
    }

    [Test]
    [Arguments("")]
    [Arguments("   ")]
    [Arguments("!!!")]
    [Arguments("+++")]      // symbol-only, no letters/digits
    [Arguments("- -")]      // punctuation-only across multiple tokens
    [Arguments("!! ??")]
    public async Task ValidateWord_rejects_input_without_letters_or_digits(string word)
    {
        await Assert.That(ClueNormalizer.ValidateWord(word)).IsNotNull();
    }

    [Test]
    public async Task ValidateWord_rejects_too_many_words()
    {
        await Assert.That(ClueNormalizer.ValidateWord("a b c d e")).IsNull();      // 5 words: the cap
        var error = ClueNormalizer.ValidateWord("a b c d e f");                     // 6 words
        await Assert.That(error).IsNotNull();
        await Assert.That(error).Contains("words");
    }

    [Test]
    public async Task ValidateWord_rejects_overlong_clues()
    {
        await Assert.That(ClueNormalizer.ValidateWord(new string('a', 31))).IsNotNull();
        await Assert.That(ClueNormalizer.ValidateWord(new string('a', 30))).IsNull();
    }

    [Test]
    public async Task Clean_collapses_internal_and_edge_whitespace()
    {
        await Assert.That(ClueNormalizer.Clean("  New   York ")).IsEqualTo("New York");
        await Assert.That(ClueNormalizer.Clean("plain")).IsEqualTo("plain");
    }

    [Test]
    public async Task Normalize_is_whitespace_insensitive_for_dedup()
    {
        await Assert.That(ClueNormalizer.Normalize("New   York")).IsEqualTo("new york");
        await Assert.That(ClueNormalizer.Normalize("new york")).IsEqualTo(ClueNormalizer.Normalize("New  York"));
    }
}

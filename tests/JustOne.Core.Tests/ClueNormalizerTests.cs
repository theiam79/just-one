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
    public async Task ValidateWord_accepts_single_words(string word)
    {
        await Assert.That(ClueNormalizer.ValidateWord(word)).IsNull();
    }

    [Test]
    [Arguments("two words")]
    [Arguments("tab\there")]
    [Arguments("")]
    [Arguments("   ")]
    [Arguments("!!!")]
    public async Task ValidateWord_rejects_invalid_input(string word)
    {
        await Assert.That(ClueNormalizer.ValidateWord(word)).IsNotNull();
    }

    [Test]
    public async Task ValidateWord_rejects_overlong_words()
    {
        await Assert.That(ClueNormalizer.ValidateWord(new string('a', 31))).IsNotNull();
        await Assert.That(ClueNormalizer.ValidateWord(new string('a', 30))).IsNull();
    }
}

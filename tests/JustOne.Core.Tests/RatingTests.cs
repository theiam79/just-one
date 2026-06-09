using JustOne.Core;

namespace JustOne.Core.Tests;

public class RatingTests
{
    [Test]
    [Arguments(13, "Perfect score!")]
    [Arguments(12, "Incredible!")]
    [Arguments(11, "Awesome!")]
    [Arguments(10, "Wow!")]
    [Arguments(9, "Wow!")]
    [Arguments(8, "Great job!")]
    [Arguments(7, "Great job!")]
    [Arguments(6, "Not bad!")]
    [Arguments(4, "Not bad!")]
    [Arguments(3, "Try again!")]
    [Arguments(0, "Try again!")]
    public async Task Score_maps_to_official_rating(int score, string expected)
    {
        await Assert.That(Rating.For(score)).IsEqualTo(expected);
    }
}

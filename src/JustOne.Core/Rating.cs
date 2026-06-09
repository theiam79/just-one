namespace JustOne.Core;

/// <summary>The official Just One end-of-game ratings.</summary>
public static class Rating
{
    public static string For(int score) => score switch
    {
        13 => "Perfect score!",
        12 => "Incredible!",
        11 => "Awesome!",
        >= 9 => "Wow!",
        >= 7 => "Great job!",
        >= 4 => "Not bad!",
        _ => "Try again!",
    };
}

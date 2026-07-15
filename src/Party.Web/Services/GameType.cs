namespace Party.Web.Services;

/// <summary>The games this app can host.</summary>
public enum GameType
{
    JustOne,
    Flip7,
}

/// <summary>How each game is described on the picker.</summary>
public sealed record GameInfo(GameType Type, string Name, string Tagline, string Blurb)
{
    public static readonly IReadOnlyList<GameInfo> All =
    [
        new(GameType.JustOne, "Just One", "The cooperative word game",
            "One player guesses a mystery word; everyone else writes a clue — but identical clues cancel out."),
        new(GameType.Flip7, "Flip 7", "The push-your-luck card game",
            "Hit for one more card, or stay and bank your points. Two of the same number and you bust."),
    ];

    public static GameInfo For(GameType type) => All.Single(g => g.Type == type);
}

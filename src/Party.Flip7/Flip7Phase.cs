namespace Party.Flip7;

public enum Flip7Phase
{
    Lobby,
    /// <summary>Handing every player their first card. Pauses to resolve any action card dealt.</summary>
    Dealing,
    /// <summary>Offering each player Hit or Stay in turn.</summary>
    Turns,
    /// <summary>The round is scored; the host moves things on.</summary>
    RoundResult,
    GameOver,
}

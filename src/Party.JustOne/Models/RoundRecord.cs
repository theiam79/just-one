namespace Party.JustOne;

public sealed record RoundRecord(int RoundNumber, string? MysteryWord, string? Guess, RoundOutcome Outcome, string GuesserName);

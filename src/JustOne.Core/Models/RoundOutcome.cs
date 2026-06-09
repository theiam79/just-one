namespace JustOne.Core;

public enum RoundOutcome
{
    /// <summary>The guess was right: +1 point.</summary>
    Correct,

    /// <summary>The guesser passed (or the round was skipped): the card is discarded with no penalty.</summary>
    Passed,

    /// <summary>The guess was wrong: this card is lost and the next card in the deck is discarded too.</summary>
    Wrong,
}

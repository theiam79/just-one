using Party.Core;
using Party.JustOne;

namespace Party.JustOne.Tests;

/// <summary>Builders for rooms in known states. Round 1 guesser is always Alice (seat 0).</summary>
internal static class TestGame
{
    public static readonly Guid Alice = Guid.Parse("00000000-0000-0000-0000-000000000001");
    public static readonly Guid Bob = Guid.Parse("00000000-0000-0000-0000-000000000002");
    public static readonly Guid Carol = Guid.Parse("00000000-0000-0000-0000-000000000003");
    public static readonly Guid Dave = Guid.Parse("00000000-0000-0000-0000-000000000004");

    public static string[] Words(int count = 80) =>
        [.. Enumerable.Range(1, count).Select(i => $"word{i:D3}")];

    public static GameRoom NewRoom(int seed = 42) => new("TEST", Words(), new Random(seed));

    /// <summary>
    /// Alice (host), Bob, Carol in the lobby, all connected.
    /// Pinned to one clue per writer: three players would otherwise trip the small-group
    /// variant, and these fixtures exist to exercise the ordinary one-clue round.
    /// The variant has its own fixtures in <see cref="TwoCluesTests"/>.
    /// </summary>
    public static GameRoom Lobby3(int seed = 42)
    {
        var room = NewRoom(seed);
        room.Join(Alice, "Alice");
        room.Join(Bob, "Bob");
        room.Join(Carol, "Carol");
        room.PlayerConnected(Alice);
        room.PlayerConnected(Bob);
        room.PlayerConnected(Carol);
        room.SetTwoCluesMode(Alice, TwoCluesMode.Never);
        return room;
    }

    public static GameRoom Started3(int seed = 42)
    {
        var room = Lobby3(seed);
        room.StartGame(Alice);
        return room;
    }

    /// <summary>Game started and number picked: Alice guessing, Bob and Carol writing clues.</summary>
    public static GameRoom InClueWriting(int seed = 42, int number = 1)
    {
        var room = Started3(seed);
        room.PickNumber(Alice, number);
        return room;
    }

    public static GameRoom InClueReview(int seed = 42, string bobClue = "alpha", string carolClue = "beta")
    {
        var room = InClueWriting(seed);
        room.SubmitClue(Bob, bobClue);
        room.SubmitClue(Carol, carolClue);
        return room;
    }

    public static GameRoom InGuessing(int seed = 42)
    {
        var room = InClueReview(seed);
        room.RevealClues(Bob);
        return room;
    }

    /// <summary>The writer's one and only clue — asserts there is exactly one.</summary>
    public static Clue Only(this Dictionary<Guid, List<Clue>> clues, Guid author) => clues[author].Single();

    /// <summary>Every clue in the round, flattened across writers.</summary>
    public static IEnumerable<Clue> All(this Dictionary<Guid, List<Clue>> clues) => clues.Values.SelectMany(c => c);

    public static GameRuleException ExpectRuleError(Action action)
    {
        try
        {
            action();
        }
        catch (GameRuleException ex)
        {
            return ex;
        }

        throw new InvalidOperationException("Expected a GameRuleException, but the action succeeded.");
    }
}

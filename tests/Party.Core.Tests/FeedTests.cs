namespace Party.Core.Tests;

/// <summary>
/// The room feed is shared plumbing: one ordered stream carrying both chat and game narration.
/// Anyone in the room can talk, narration drops in alongside, and old lines scroll off.
/// </summary>
public class FeedTests
{
    private static readonly Guid Alice = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid Bob = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly Guid Stranger = Guid.Parse("00000000-0000-0000-0000-0000000000ff");

    private static TestRoom Room()
    {
        var room = new TestRoom();
        room.Join(Alice, "Alice");
        room.Join(Bob, "Bob");
        return room;
    }

    private static IReadOnlyList<ChatEntry> Chat(TestRoom room) => [.. room.Feed.OfType<ChatEntry>()];

    [Test]
    public async Task A_chat_message_records_its_sender_and_text()
    {
        var room = Room();
        room.PostChat(Alice, "hello");

        var msg = Chat(room).Single();
        await Assert.That(msg.SenderId).IsEqualTo(Alice);
        await Assert.That(msg.SenderName).IsEqualTo("Alice");
        await Assert.That(msg.Text).IsEqualTo("hello");
    }

    [Test]
    public async Task Chat_and_narration_share_one_ordered_stream()
    {
        var room = Room();
        room.PostChat(Alice, "nice");
        room.Say("Bob drew 7", "draw");
        room.PostChat(Bob, "thanks");

        await Assert.That(room.Feed.Count).IsEqualTo(3);
        await Assert.That(room.Feed[0]).IsTypeOf<ChatEntry>();
        await Assert.That(room.Feed[1]).IsTypeOf<NarrationEntry>();
        await Assert.That(room.Feed[2]).IsTypeOf<ChatEntry>();
        // Seq is monotonic across both kinds, so the panel can render them in one order.
        await Assert.That(room.Feed.Select(e => e.Seq)).IsEquivalentTo(new long[] { 0, 1, 2 });
    }

    [Test]
    public async Task A_spectator_can_still_talk()
    {
        var carol = Guid.Parse("00000000-0000-0000-0000-000000000003");
        var dave = Guid.Parse("00000000-0000-0000-0000-000000000004");
        var room = new TestRoom();
        room.Join(Alice, "Alice");
        room.PlayerConnected(Alice);
        room.Join(Bob, "Bob");
        room.PlayerConnected(Bob);
        room.Join(carol, "Carol");
        room.PlayerConnected(carol);
        room.Start();               // game running, so the next joiner is seated out
        room.Join(dave, "Dave");

        await Assert.That(room.Players.Single(p => p.Id == dave).IsSpectator).IsTrue();
        room.PostChat(dave, "hi from the sidelines");

        await Assert.That(Chat(room).Single().SenderName).IsEqualTo("Dave");
    }

    [Test]
    public async Task Blank_and_whitespace_messages_are_ignored()
    {
        var room = Room();
        room.PostChat(Alice, "   ");
        room.PostChat(Alice, "");

        await Assert.That(room.Feed).IsEmpty();
    }

    [Test]
    public async Task Text_is_trimmed_and_capped()
    {
        var room = Room();
        room.PostChat(Alice, "  spaced  ");
        room.PostChat(Bob, new string('x', RoomBase.MaxChatLength + 50));

        await Assert.That(Chat(room)[0].Text).IsEqualTo("spaced");
        await Assert.That(Chat(room)[1].Text.Length).IsEqualTo(RoomBase.MaxChatLength);
    }

    [Test]
    public async Task Someone_not_in_the_room_cannot_post()
    {
        var room = Room();

        Assert.Throws<GameRuleException>(() => room.PostChat(Stranger, "sneaking in"));
        await Assert.That(room.Feed).IsEmpty();
    }

    [Test]
    public async Task Old_lines_fall_off_but_sequences_keep_rising()
    {
        var room = Room();
        for (var i = 0; i < 400; i++)   // more than the kept history
        {
            room.PostChat(Alice, $"m{i}");
        }

        await Assert.That(room.Feed.Count).IsLessThanOrEqualTo(300);
        // Sequences never repeat, even as old lines fall off, and the newest is last.
        await Assert.That(room.Feed.Select(e => e.Seq).Distinct().Count()).IsEqualTo(room.Feed.Count);
        await Assert.That(((ChatEntry)room.Feed[^1]).Text).IsEqualTo("m399");
    }
}

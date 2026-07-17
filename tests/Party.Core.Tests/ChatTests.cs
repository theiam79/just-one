namespace Party.Core.Tests;

/// <summary>Room chat is shared plumbing: anyone in the room can talk, and it stays ephemeral.</summary>
public class ChatTests
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

    [Test]
    public async Task A_message_records_its_sender_and_text()
    {
        var room = Room();
        room.PostChat(Alice, "hello");

        var msg = room.Chat.Single();
        await Assert.That(msg.SenderId).IsEqualTo(Alice);
        await Assert.That(msg.SenderName).IsEqualTo("Alice");
        await Assert.That(msg.Text).IsEqualTo("hello");
    }

    [Test]
    public async Task A_spectator_can_still_talk()
    {
        // A mid-game joiner watches rather than plays, but the doc promises they can still chat.
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

        await Assert.That(room.Chat.Single().SenderName).IsEqualTo("Dave");
    }

    [Test]
    public async Task Blank_and_whitespace_messages_are_ignored()
    {
        var room = Room();
        room.PostChat(Alice, "   ");
        room.PostChat(Alice, "");

        await Assert.That(room.Chat).IsEmpty();
    }

    [Test]
    public async Task Text_is_trimmed_and_capped()
    {
        var room = Room();
        room.PostChat(Alice, "  spaced  ");
        room.PostChat(Bob, new string('x', RoomBase.MaxChatLength + 50));

        await Assert.That(room.Chat[0].Text).IsEqualTo("spaced");
        await Assert.That(room.Chat[1].Text.Length).IsEqualTo(RoomBase.MaxChatLength);
    }

    [Test]
    public async Task Someone_not_in_the_room_cannot_post()
    {
        var room = Room();

        Assert.Throws<GameRuleException>(() => room.PostChat(Stranger, "sneaking in"));
        await Assert.That(room.Chat).IsEmpty();
    }

    [Test]
    public async Task Sequence_numbers_keep_rising_even_as_old_messages_fall_off()
    {
        var room = Room();
        for (var i = 0; i < 250; i++)   // more than the kept history
        {
            room.PostChat(Alice, $"m{i}");
        }

        await Assert.That(room.Chat.Count).IsLessThanOrEqualTo(200);
        // The oldest kept message is newer than the very first, and sequences never repeat.
        await Assert.That(room.Chat.Select(m => m.Sequence).Distinct().Count()).IsEqualTo(room.Chat.Count);
        await Assert.That(room.Chat[^1].Text).IsEqualTo("m249");
    }
}

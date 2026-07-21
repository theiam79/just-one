namespace Party.Core;

/// <summary>
/// One line in a room's shared feed — chat and game narration in a single stream, oldest first.
/// <see cref="Seq"/> is monotonic across the room's life, so keys stay stable as old lines fall off
/// the end.
/// </summary>
public abstract record FeedEntry(long Seq);

/// <summary>
/// Something a player said. The sender's name is captured when the message is posted, so it still
/// reads correctly after they rename or leave.
/// </summary>
public sealed record ChatEntry(long Seq, Guid SenderId, string SenderName, string Text) : FeedEntry(Seq);

/// <summary>
/// A game narrating what happened — the play-by-play. Styled apart from chat (quieter) but living
/// in the same stream, so it scrolls together. The category is the game's to name; the UI styles by it.
/// </summary>
public sealed record NarrationEntry(long Seq, string Text, string Category) : FeedEntry(Seq);

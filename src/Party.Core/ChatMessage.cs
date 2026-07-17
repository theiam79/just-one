namespace Party.Core;

/// <summary>
/// One line of room chat. The sender's name is captured when the message is posted, so it still
/// reads correctly after they rename or leave.
/// </summary>
public sealed record ChatMessage(int Sequence, Guid SenderId, string SenderName, string Text);

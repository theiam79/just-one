namespace Party.Core;

/// <summary>Thrown when a player attempts an action the rules don't allow. The message is safe to show to players.</summary>
public sealed class GameRuleException(string message) : Exception(message);

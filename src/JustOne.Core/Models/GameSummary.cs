namespace JustOne.Core;

public sealed record GameSummary(int Score, int Total, string Rating, DateTimeOffset CompletedAt);

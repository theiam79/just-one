namespace Party.Flip7;

/// <summary>
/// The categories Flip 7 tags its <see cref="Party.Core.GameLogEntry"/> lines with. Each doubles
/// as a CSS class the log panel styles by, so the strings are part of the contract.
/// </summary>
public static class Flip7LogKind
{
    public const string Draw = "draw";
    public const string Bust = "bust";
    public const string Flip7 = "flip7";
    public const string Freeze = "freeze";
    public const string FlipThree = "flip-three";
    public const string SecondChance = "second-chance";
    public const string Stay = "stay";
    public const string Info = "info";
}

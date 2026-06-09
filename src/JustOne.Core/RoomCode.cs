namespace JustOne.Core;

public static class RoomCode
{
    // No I or O: avoids confusion with 1 and 0 when codes are read out loud on a call.
    public const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ";
    public const int Length = 4;

    public static string New(Random rng) =>
        string.Create(Length, rng, (chars, r) =>
        {
            for (var i = 0; i < chars.Length; i++)
            {
                chars[i] = Alphabet[r.Next(Alphabet.Length)];
            }
        });
}

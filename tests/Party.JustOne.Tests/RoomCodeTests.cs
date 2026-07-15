using Party.Core;
using Party.JustOne;

namespace Party.JustOne.Tests;

public class RoomCodeTests
{
    [Test]
    public async Task Codes_are_four_chars_from_the_safe_alphabet()
    {
        var rng = new Random(7);
        for (var i = 0; i < 200; i++)
        {
            var code = RoomCode.New(rng);
            await Assert.That(code.Length).IsEqualTo(RoomCode.Length);
            await Assert.That(code.All(RoomCode.Alphabet.Contains)).IsTrue();
        }
    }
}

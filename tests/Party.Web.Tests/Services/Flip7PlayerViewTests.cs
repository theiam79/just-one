using Party.Flip7;
using Party.Web.Services;

namespace Party.Web.Tests.Services;

/// <summary>The "you're about to win" projection: bank now and you'd cross the target.</summary>
public class Flip7PlayerViewTests
{
    private static Flip7PlayerView Player(int total, int roundScore, RoundStatus status) =>
        new(Guid.NewGuid(), "P", IsHost: false, IsConnected: true, IsSpectator: false, IsBenched: false,
            IsDealer: false, IsTheirTurn: false, status, Line: [], SpentLine: [], roundScore, total);

    [Test]
    public async Task On_track_when_banking_now_would_reach_the_target()
    {
        // 190 banked + 15 this round = 205, past 200.
        await Assert.That(Player(190, 15, RoundStatus.Active).WillReachTarget).IsTrue();
    }

    [Test]
    public async Task Not_on_track_while_still_short()
    {
        await Assert.That(Player(100, 50, RoundStatus.Active).WillReachTarget).IsFalse();
    }

    [Test]
    public async Task A_busted_line_is_never_on_track()
    {
        // Busted banks nothing this round, however high the running total.
        await Assert.That(Player(300, 0, RoundStatus.Busted).WillReachTarget).IsFalse();
    }

    [Test]
    public async Task Hitting_the_target_exactly_counts()
    {
        // 185 + 15 = 200, and the win condition is >= 200.
        await Assert.That(Player(185, 15, RoundStatus.Active).WillReachTarget).IsTrue();
    }

    [Test]
    public async Task A_stayed_player_on_track_still_shows()
    {
        // They've locked their line in, so banking past the target is now certain.
        await Assert.That(Player(190, 12, RoundStatus.Stayed).WillReachTarget).IsTrue();
    }
}

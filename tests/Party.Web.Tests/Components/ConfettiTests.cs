using Bunit;
using Party.Web.Components.Game;

namespace Party.Web.Tests.Components;

/// <summary>The confetti burst is a dumb overlay: on when told, absent otherwise.</summary>
public class ConfettiTests
{
    [Test]
    public async Task It_shows_a_burst_when_active()
    {
        using var ctx = new BunitContext();
        var confetti = ctx.Render<Confetti>(p => p.Add(x => x.Active, true).Add(x => x.Pieces, 12));

        await Assert.That(confetti.FindAll(".confetti")).Count().IsEqualTo(1);
        await Assert.That(confetti.FindAll(".confetti-bit")).Count().IsEqualTo(12);
    }

    [Test]
    public async Task It_renders_nothing_when_inactive()
    {
        using var ctx = new BunitContext();
        var confetti = ctx.Render<Confetti>(p => p.Add(x => x.Active, false));

        await Assert.That(confetti.FindAll(".confetti")).IsEmpty();
    }
}

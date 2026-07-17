using Bunit;
using Microsoft.AspNetCore.Components;
using Party.Web.Components.Game;

namespace Party.Web.Tests.Components;

/// <summary>The shared countdown: shows its content while live, and reports expiry exactly once.</summary>
public class CountdownTests
{
    private static RenderFragment<TimeSpan> Tick() =>
        _ => builder => builder.AddMarkupContent(0, "<span class=\"tick\"></span>");

    [Test]
    public async Task It_renders_its_content_while_a_deadline_is_live()
    {
        using var ctx = new BunitContext();
        var cut = ctx.Render<Countdown>(p => p
            .Add(x => x.Deadline, DateTimeOffset.UtcNow.AddSeconds(60))
            .Add(x => x.ChildContent, Tick()));

        await Assert.That(cut.FindAll(".tick")).Count().IsEqualTo(1);
    }

    [Test]
    public async Task It_renders_nothing_without_a_deadline()
    {
        using var ctx = new BunitContext();
        var cut = ctx.Render<Countdown>(p => p
            .Add(x => x.Deadline, (DateTimeOffset?)null)
            .Add(x => x.ChildContent, Tick()));

        await Assert.That(cut.FindAll(".tick")).IsEmpty();
    }

    [Test]
    public async Task It_reports_expiry_once_when_the_deadline_has_passed()
    {
        using var ctx = new BunitContext();
        var fired = 0;
        var cut = ctx.Render<Countdown>(p => p
            .Add(x => x.Deadline, DateTimeOffset.UtcNow.AddSeconds(-1))
            .Add(x => x.ChildContent, Tick())
            .Add(x => x.OnExpired, () => fired++));

        cut.WaitForState(() => fired > 0);
        cut.Render();   // a re-render of the same (expired) deadline must not fire it again

        await Assert.That(fired).IsEqualTo(1);
    }
}

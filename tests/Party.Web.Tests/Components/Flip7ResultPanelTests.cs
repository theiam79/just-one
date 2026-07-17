using Bunit;
using Party.Flip7;
using Party.Web.Components.Game;
using Party.Web.Services;

namespace Party.Web.Tests.Components;

/// <summary>The result panel throws confetti on a Flip 7 or a win — unless this browser muted it.</summary>
public class Flip7ResultPanelTests
{
    private static readonly Guid Alice = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid Bob = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly Guid Carol = Guid.Parse("00000000-0000-0000-0000-000000000003");

    /// <summary>A view sitting on a round that ended because Carol hit a Flip 7.</summary>
    private static Flip7View FlipSevenView()
    {
        var deck = new List<Card>
        {
            new NumberCard(1), new NumberCard(2), new NumberCard(3),   // deal: Bob, Carol, Alice
            new NumberCard(4), new NumberCard(9),
            new NumberCard(5), new NumberCard(10),
            new NumberCard(6), new NumberCard(11),
            new NumberCard(7), new NumberCard(12),
            new NumberCard(8), new NumberCard(0),
            new NumberCard(9),                                          // Carol's seventh distinct number
        };
        deck.AddRange(Enumerable.Repeat<Card>(new NumberCard(0), 50));

        var room = new Flip7Room("TEST", _ => deck, new Random(42));
        room.Join(Alice, "Alice");
        room.Join(Bob, "Bob");
        room.Join(Carol, "Carol");
        room.PlayerConnected(Alice);
        room.PlayerConnected(Bob);
        room.PlayerConnected(Carol);
        room.StartGame(Alice);

        room.Stay(Bob);
        for (var i = 0; i < 5; i++)
        {
            room.Hit(Carol);
            room.Hit(Alice);
        }

        room.Hit(Carol);

        return Flip7View.Build(room, Alice);
    }

    private static IRenderedComponent<Flip7ResultPanel> Render(BunitContext ctx, Flip7View view) =>
        ctx.Render<Flip7ResultPanel>(p => p
            .Add(x => x.View, view)
            .Add(x => x.Act, _ => { }));

    [Test]
    public async Task A_flip_7_sets_off_the_confetti_when_it_is_on()
    {
        using var ctx = new BunitContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.JSInterop.Setup<bool>("party.getFlag", _ => true).SetResult(true);

        var panel = Render(ctx, FlipSevenView());

        await Assert.That(panel.FindAll(".confetti")).Count().IsEqualTo(1);
    }

    [Test]
    public async Task Muting_it_leaves_the_celebration_off()
    {
        using var ctx = new BunitContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.JSInterop.Setup<bool>("party.getFlag", _ => true).SetResult(false);

        var panel = Render(ctx, FlipSevenView());

        await Assert.That(panel.FindAll(".confetti")).IsEmpty();
    }

    [Test]
    public async Task The_mute_toggle_is_always_offered()
    {
        using var ctx = new BunitContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.JSInterop.Setup<bool>("party.getFlag", _ => true).SetResult(true);

        var panel = Render(ctx, FlipSevenView());

        await Assert.That(panel.FindAll(".confetti-toggle")).Count().IsEqualTo(1);
    }
}

using AngleSharp.Dom;
using Bunit;
using Party.Flip7;
using Party.Web.Components.Game;
using Party.Web.Services;

namespace Party.Web.Tests.Components;

/// <summary>The scorecard grid: a row per player, a column per finished round, plus the total.</summary>
public class Flip7ScorecardTests
{
    private static readonly Guid Alice = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid Bob = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly Guid Carol = Guid.Parse("00000000-0000-0000-0000-000000000003");

    /// <summary>A view after one round: Bob 5, Carol 4, Alice 3.</summary>
    private static Flip7View OneRoundView()
    {
        var deck = new List<Card> { new NumberCard(5), new NumberCard(4), new NumberCard(3) };
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
        room.Stay(Carol);
        room.Stay(Alice);

        return Flip7View.Build(room, Alice);
    }

    private static IElement Row(IRenderedComponent<Flip7Scorecard> card, string name) =>
        card.FindAll(".scorecard-table tbody tr").Single(r => r.QuerySelector(".sc-name")!.TextContent == name);

    [Test]
    public async Task It_shows_a_row_per_player_and_a_column_per_round()
    {
        using var ctx = new BunitContext();
        var card = ctx.Render<Flip7Scorecard>(p => p.Add(x => x.View, OneRoundView()));

        await Assert.That(card.FindAll(".scorecard-table tbody tr")).Count().IsEqualTo(3);
        await Assert.That(card.Find(".scorecard-table thead").TextContent).Contains("R1");
    }

    [Test]
    public async Task Each_players_round_score_and_total_appear()
    {
        using var ctx = new BunitContext();
        var card = ctx.Render<Flip7Scorecard>(p => p.Add(x => x.View, OneRoundView()));

        // Bob banked a 5 this round; with one round played that's also his total.
        var cells = Row(card, "Bob").QuerySelectorAll("td").Select(c => c.TextContent).ToList();
        await Assert.That(cells).Contains("5");
        await Assert.That(Row(card, "Alice").QuerySelectorAll("td").Select(c => c.TextContent)).Contains("3");
    }

    [Test]
    public async Task A_player_who_left_keeps_their_recorded_scores()
    {
        using var ctx = new BunitContext();
        var deck = new List<Card> { new NumberCard(5), new NumberCard(4), new NumberCard(3) };
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
        room.Stay(Carol);
        room.Stay(Alice);           // round 1 recorded: Carol banked 4
        room.Leave(Carol);          // Carol leaves mid-game — kept as a spectator

        var card = ctx.Render<Flip7Scorecard>(p => p.Add(x => x.View, Flip7View.Build(room, Alice)));

        // Her row survives with the score she'd already banked.
        await Assert.That(Row(card, "Carol").QuerySelectorAll("td").Select(c => c.TextContent)).Contains("4");
    }
}

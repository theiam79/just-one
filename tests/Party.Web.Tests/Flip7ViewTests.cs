using Party.Flip7;
using Party.Web.Services;

namespace Party.Web.Tests;

public class Flip7ViewTests
{
    private static readonly Guid Alice = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid Bob = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly Guid Carol = Guid.Parse("00000000-0000-0000-0000-000000000003");

    private static Flip7Room Room(params Card[] stack)
    {
        var deck = stack.ToList();
        deck.AddRange(Enumerable.Repeat(new NumberCard(0), 100));
        var room = new Flip7Room("TEST", _ => deck, new Random(42));
        room.Join(Alice, "Alice");
        room.Join(Bob, "Bob");
        room.Join(Carol, "Carol");
        room.PlayerConnected(Alice);
        room.PlayerConnected(Bob);
        room.PlayerConnected(Carol);
        room.StartGame(Alice);
        return room;
    }

    [Test]
    public async Task Everyone_sees_the_same_cards()
    {
        // Flip 7 hides nothing, unlike Just One. Two viewers must see identical lines.
        var room = Room(new NumberCard(1), new NumberCard(2), new NumberCard(3));
        var asAlice = Flip7View.Build(room, Alice);
        var asBob = Flip7View.Build(room, Bob);

        foreach (var id in new[] { Alice, Bob, Carol })
        {
            var a = asAlice.Players.Single(p => p.Id == id).Line;
            var b = asBob.Players.Single(p => p.Id == id).Line;
            await Assert.That(a).IsEquivalentTo(b);
        }
    }

    [Test]
    public async Task The_view_marks_whose_turn_it_is()
    {
        var room = Room(new NumberCard(1), new NumberCard(2), new NumberCard(3));
        var view = Flip7View.Build(room, Bob);

        // Alice deals, so Bob leads.
        await Assert.That(view.IAmCurrentPlayer).IsTrue();
        await Assert.That(view.Players.Single(p => p.Id == Bob).IsTheirTurn).IsTrue();
        await Assert.That(Flip7View.Build(room, Alice).IAmCurrentPlayer).IsFalse();
    }

    [Test]
    public async Task A_choice_is_only_offered_to_whoever_has_to_make_it()
    {
        var room = Room(new NumberCard(1), new NumberCard(2), new NumberCard(3), new ActionCard(ActionKind.Freeze));
        room.Hit(Bob);

        var asBob = Flip7View.Build(room, Bob);
        await Assert.That(asBob.IAmChoosing).IsTrue();
        await Assert.That(asBob.MyChoiceCard).IsEqualTo(new ActionCard(ActionKind.Freeze));
        await Assert.That(asBob.MyChoiceTargets.Count).IsEqualTo(3);
        await Assert.That(asBob.ChoosingPlayerName).IsNull();

        var asCarol = Flip7View.Build(room, Carol);
        await Assert.That(asCarol.IAmChoosing).IsFalse();
        await Assert.That(asCarol.MyChoiceTargets).IsEmpty();
        // But she is told who everyone is waiting on.
        await Assert.That(asCarol.ChoosingPlayerName).IsEqualTo("Bob");
    }

    [Test]
    public async Task Cannot_stay_with_nothing_in_front_of_you()
    {
        var room = Room(new ActionCard(ActionKind.Freeze), new NumberCard(2), new NumberCard(3));
        room.ChooseTarget(Bob, Carol);   // Bob plays his only card elsewhere

        var view = Flip7View.Build(room, Bob);
        await Assert.That(view.IAmCurrentPlayer).IsTrue();
        await Assert.That(view.CanStay).IsFalse();
    }

    [Test]
    public async Task The_round_score_shown_is_what_that_line_banks()
    {
        var room = Room(new NumberCard(5), new NumberCard(2), new NumberCard(3), new ModifierCard(ModifierKind.Times2));
        room.Hit(Bob);   // Bob: 5, x2

        var view = Flip7View.Build(room, Alice);
        await Assert.That(view.Players.Single(p => p.Id == Bob).RoundScore).IsEqualTo(10);
    }

    [Test]
    public async Task A_busted_line_shows_as_worth_nothing()
    {
        var room = Room(new NumberCard(5), new NumberCard(2), new NumberCard(3), new NumberCard(5));
        room.Hit(Bob);

        var view = Flip7View.Build(room, Alice);
        var bob = view.Players.Single(p => p.Id == Bob);
        await Assert.That(bob.Status).IsEqualTo(RoundStatus.Busted);
        await Assert.That(bob.RoundScore).IsEqualTo(0);
    }

    [Test]
    public async Task Standings_are_ordered_by_total()
    {
        // Alice deals, so the deal runs Bob, Carol, Alice.
        var room = Room(new NumberCard(1), new NumberCard(9), new NumberCard(5));
        room.Stay(Bob);
        room.Stay(Carol);
        room.Stay(Alice);

        var view = Flip7View.Build(room, Alice);
        await Assert.That(view.Standings.Select(p => p.Name)).IsEquivalentTo(new[] { "Carol", "Alice", "Bob" });
    }

    [Test]
    public async Task The_dealer_is_marked()
    {
        var room = Room(new NumberCard(1), new NumberCard(2), new NumberCard(3));
        var view = Flip7View.Build(room, Alice);

        await Assert.That(view.Players.Single(p => p.Id == Alice).IsDealer).IsTrue();
        await Assert.That(view.Players.Single(p => p.Id == Bob).IsDealer).IsFalse();
    }

    [Test]
    public async Task Host_powers_fall_through_when_the_host_is_away()
    {
        var room = Room(new NumberCard(1), new NumberCard(2), new NumberCard(3));
        await Assert.That(Flip7View.Build(room, Bob).HasHostPowers).IsFalse();

        room.PlayerDisconnected(Alice);
        await Assert.That(Flip7View.Build(room, Bob).HasHostPowers).IsTrue();
    }

    [Test]
    public async Task A_freeze_stays_visible_in_front_of_the_player_it_stopped()
    {
        var room = Room(new NumberCard(1), new NumberCard(2), new NumberCard(3), new ActionCard(ActionKind.Freeze));
        room.Hit(Bob);
        room.ChooseTarget(Bob, Carol);

        var carol = Flip7View.Build(room, Alice).Players.Single(p => p.Id == Carol);
        await Assert.That(carol.Status).IsEqualTo(RoundStatus.Frozen);
        await Assert.That(carol.IsFrozenByCard).IsTrue();
    }
}

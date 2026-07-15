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

        // The table renders Line, so that is what has to carry the Freeze.
        var carol = Flip7View.Build(room, Alice).Players.Single(p => p.Id == Carol);
        await Assert.That(carol.Status).IsEqualTo(RoundStatus.Frozen);
        await Assert.That(carol.Line).Contains(new ActionCard(ActionKind.Freeze));
    }

    // The view is built positionally from a record with six consecutive bools, and the panels
    // read these fields to decide what to say. Each of these pins one that a mutation could
    // otherwise silently flip.

    [Test]
    public async Task The_choice_kind_says_which_card_is_being_placed()
    {
        // The turn panel picks its wording from this alone: null it, and every Second Chance
        // prompt silently reads as a Flip Three.
        var room = Room(new NumberCard(1), new NumberCard(2), new NumberCard(3), new ActionCard(ActionKind.Freeze));
        room.Hit(Bob);

        await Assert.That(Flip7View.Build(room, Bob).MyChoiceKind).IsEqualTo(ChoiceKind.ActionTarget);
    }

    [Test]
    public async Task A_surplus_second_chance_is_a_different_kind_of_choice()
    {
        var room = Room(
            new NumberCard(1), new NumberCard(2), new NumberCard(3),
            new ActionCard(ActionKind.SecondChance), new NumberCard(9), new NumberCard(8),
            new ActionCard(ActionKind.SecondChance));
        room.Hit(Bob);
        room.Hit(Carol);
        room.Hit(Alice);
        room.Hit(Bob);

        await Assert.That(Flip7View.Build(room, Bob).MyChoiceKind).IsEqualTo(ChoiceKind.SecondChanceRecipient);
    }

    [Test]
    public async Task The_round_number_and_deck_are_reported()
    {
        var room = Room(new NumberCard(1), new NumberCard(2), new NumberCard(3));
        var view = Flip7View.Build(room, Alice);

        await Assert.That(view.RoundNumber).IsEqualTo(1);
        await Assert.That(view.DeckCount).IsEqualTo(room.DeckCount);
        await Assert.That(view.DeckCount).IsGreaterThan(0);
    }

    [Test]
    public async Task The_code_and_the_viewer_are_reported()
    {
        var view = Flip7View.Build(Room(new NumberCard(1), new NumberCard(2), new NumberCard(3)), Bob);

        await Assert.That(view.Code).IsEqualTo("TEST");
        await Assert.That(view.MyId).IsEqualTo(Bob);
        await Assert.That(view.HostName).IsEqualTo("Alice");
    }

    [Test]
    public async Task A_watcher_is_marked_as_one_and_a_player_is_not()
    {
        var room = Room(new NumberCard(1), new NumberCard(2), new NumberCard(3));
        var dave = Guid.Parse("00000000-0000-0000-0000-000000000004");
        room.Join(dave, "Dave");   // mid-game: watches

        var view = Flip7View.Build(room, dave);
        await Assert.That(view.IAmSpectator).IsTrue();
        await Assert.That(view.Players.Single(p => p.Id == dave).IsSpectator).IsTrue();
        await Assert.That(view.Players.Single(p => p.Id == Bob).IsSpectator).IsFalse();
        await Assert.That(Flip7View.Build(room, Bob).IAmSpectator).IsFalse();
    }

    [Test]
    public async Task A_player_sat_out_is_marked_as_benched()
    {
        var room = Room(new NumberCard(1), new NumberCard(2), new NumberCard(3));
        room.PlayerDisconnected(Carol);
        room.BenchPlayer(Alice, Carol);

        var carol = Flip7View.Build(room, Alice).Players.Single(p => p.Id == Carol);
        await Assert.That(carol.IsBenched).IsTrue();
        await Assert.That(carol.IsConnected).IsFalse();
        await Assert.That(Flip7View.Build(room, Alice).Players.Single(p => p.Id == Bob).IsBenched).IsFalse();
    }

    [Test]
    public async Task The_flip7_player_is_named()
    {
        // Bob leads and takes seven different numbers.
        var room = Room(
            new NumberCard(1), new NumberCard(2), new NumberCard(3),
            new NumberCard(4), new NumberCard(9),
            new NumberCard(5), new NumberCard(10),
            new NumberCard(6), new NumberCard(11),
            new NumberCard(7), new NumberCard(12),
            new NumberCard(8), new NumberCard(0),
            new NumberCard(9));

        // Bob leads the turn order, so he takes one before the others bow out.
        room.Hit(Bob);
        room.Stay(Carol);
        room.Stay(Alice);
        for (var i = 0; i < 5; i++)
        {
            room.Hit(Bob);
        }

        await Assert.That(Flip7View.Build(room, Alice).Flip7PlayerId).IsEqualTo(Bob);
    }

    [Test]
    public async Task Nobody_is_the_flip7_player_or_the_winner_by_default()
    {
        var room = Room(new NumberCard(1), new NumberCard(2), new NumberCard(3));
        var view = Flip7View.Build(room, Alice);

        await Assert.That(view.Flip7PlayerId).IsNull();
        await Assert.That(view.WinnerId).IsNull();
    }

    [Test]
    public async Task The_roster_carries_every_seat_through_to_the_shared_list()
    {
        var room = Room(new NumberCard(1), new NumberCard(2), new NumberCard(3));
        room.PlayerDisconnected(Carol);

        var roster = Flip7View.Build(room, Alice).Roster;
        await Assert.That(roster.Select(r => r.Name)).IsEquivalentTo(new[] { "Alice", "Bob", "Carol" });
        await Assert.That(roster.Single(r => r.Id == Alice).IsHost).IsTrue();
        await Assert.That(roster.Single(r => r.Id == Carol).IsConnected).IsFalse();
    }
}

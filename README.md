# Just One — web edition

A web version of the cooperative party game **Just One**, built for playing with friends
while you're together on a call. One player guesses a mystery word; everyone else secretly
writes a one-word clue — but identical clues cancel out and are never shown.

- **Aspire 13.4** orchestration, **Blazor Server** UI on **.NET 10** — no JS framework
- Rooms are joined with a 4-letter code (or a shared link); state is in-memory only and
  rooms expire after an hour of inactivity
- Standard rules: 13 cards per game, guesser picks a number 1–5, duplicate clues
  auto-cancel (case/accent-insensitive) plus tap-to-cancel for variants, exact guesses
  auto-score, near-misses go to an accept/reject vote, a wrong guess burns the next card too
- 3–12 players; refreshing the page rejoins your seat (identity lives in `localStorage`);
  mid-game joiners spectate until the next game; the host can skip stuck players or rounds

## Run it

```bash
dotnet run --project src/JustOne.AppHost     # with the Aspire dashboard
# or, standalone (same app, fixed ports):
dotnet run --project src/JustOne.Web         # http://localhost:5201
```

The web app always listens on **http://localhost:5201** (launch profile), under both the
AppHost and standalone runs.

## Host for friends via dev tunnels

The AppHost includes Aspire's native dev tunnels integration (`Aspire.Hosting.DevTunnels`):
a `tunnel` resource fronts the game's http endpoint with anonymous access enabled, so
`dotnet run --project src/JustOne.AppHost` is all it takes — the public
`https://….devtunnels.ms` URL appears on the **tunnel** resource in the Aspire dashboard.
Share that URL (or URL + room code) with your friends.

One-time setup: install the [devtunnel CLI](https://learn.microsoft.com/azure/developer/dev-tunnels/get-started#install)
and run `devtunnel user login`. If the CLI is missing the tunnel resource reports it in
the dashboard and the game still runs locally on port 5201.

Notes:
- The tunnel exposes the **http** endpoint; dev tunnels terminate TLS at the edge, and
  the app deliberately does not redirect to its local https endpoint.
- WebSockets (Blazor's circuit transport) work through dev tunnels out of the box.
- Guests may see a one-time anonymous-access interstitial on first visit — just continue.
- Don't want the tunnel on a given run? Comment out the `AddDevTunnel` block in
  `src/JustOne.AppHost/AppHost.cs`, or run the web project standalone.

## Project layout

```
src/JustOne.AppHost          Aspire app host (run this)
src/JustOne.ServiceDefaults  OpenTelemetry / health checks / service discovery defaults
src/JustOne.Core             Pure game engine — rules state machine, no ASP.NET deps
src/JustOne.Web              Blazor Server app: rooms, real-time fan-out, UI
tests/JustOne.Core.Tests     TUnit tests for the engine
```

How real-time works: a singleton `RoomManager` holds each room behind a lock
(`RoomHandle`); every mutation raises a `Changed` event and each player's Blazor circuit
re-renders from an immutable per-viewer snapshot (`RoomView`). Information hiding happens
in the snapshot, so the guesser's browser never receives the mystery word or unrevealed
clues.

## Tests

```bash
dotnet run --project tests/JustOne.Core.Tests   # TUnit / Microsoft.Testing.Platform
# `dotnet test` works too
```

## Manual smoke script (multi-browser)

1. Open three browser windows (use private windows so each gets its own identity),
   create a room as the host, join with the other two.
2. Start the game, pick a number, submit `Café` and `cafe` as clues → both get struck
   through in review; reveal → guesser sees no clues; pass.
3. Next round: distinct clues, tap one to cancel/uncancel it, reveal, guess the word with
   different casing → auto-scores.
4. Wrong guess → reject in the judgment step → two cards gone.
5. Refresh a window mid-round → same seat; join with a fourth window mid-game → spectator
   until the next game.

using Microsoft.AspNetCore.Components;
using Party.Core;
using Party.Web.Services;

namespace Party.Web.Components.Game;

/// <summary>
/// The circuit plumbing every game's room needs: joining, subscribing to the room's changes,
/// re-rendering on them, surfacing rejected moves, and leaving on teardown.
/// </summary>
/// <remarks>
/// Generic over the game's state machine rather than over a shared room base, because there
/// isn't one yet — each game still owns its own roster. What that costs is two one-line
/// overrides per game; what it buys is that this, the fiddly part, is written once. See #23.
/// </remarks>
public abstract class RoomStage<TRoom> : ComponentBase, IDisposable
    where TRoom : RoomBase
{
    [Parameter, EditorRequired] public required RoomHandle<TRoom> Handle { get; set; }

    [Parameter, EditorRequired] public required Guid PlayerId { get; set; }

    [Parameter, EditorRequired] public required string PlayerName { get; set; }

    [Inject] private RoomManager Rooms { get; set; } = default!;

    /// <summary>The last rejected move's message, shown as a banner until dismissed.</summary>
    protected string? Error { get; set; }

    protected bool RoomClosed { get; private set; }

    /// <summary>What the "room closed" notice should say — the same in every game.</summary>
    protected string ClosedDetail => Handle.CloseReason switch
    {
        RoomCloseReason.HostClosed => "The host closed this room.",
        _ => "This room was closed after an hour of inactivity.",
    };

    protected bool Joined { get; private set; }

    private Action? _changedHandler;

    /// <summary>Puts this player in the room and marks them connected.</summary>
    protected abstract void JoinRoom(TRoom room, Guid playerId, string name);

    /// <summary>Marks this player's circuit gone.</summary>
    protected abstract void LeaveRoom(TRoom room, Guid playerId);

    /// <summary>Rebuilds this viewer's snapshot of the room.</summary>
    protected abstract void BuildView(RoomHandle<TRoom> handle, Guid playerId);

    protected abstract void ClearView();

    protected override void OnInitialized()
    {
        // Changes are raised on whichever circuit made the move, so hop back onto ours before
        // touching component state.
        _changedHandler = () => _ = InvokeAsync(OnRoomChanged);
        Handle.Changed += _changedHandler;
        Join();
    }

    private void Join()
    {
        try
        {
            Handle.Mutate(room => JoinRoom(room, PlayerId, PlayerName));
            Joined = true;
            Error = null;
            Refresh();
        }
        catch (GameRuleException ex)
        {
            Error = ex.Message;
            RoomClosed = Handle.IsClosed;
        }
    }

    /// <summary>Runs a move for this player, surfacing a rule violation as a banner.</summary>
    protected void Act(Action<TRoom> action)
    {
        try
        {
            Handle.Mutate(action);
            Error = null;
        }
        catch (GameRuleException ex)
        {
            Error = ex.Message;
        }

        Refresh();

        // Act runs from a child panel's event handler, so Blazor only re-renders that child.
        // A rejected move raises no Changed event either, so without this the banner never
        // appears and the move just seems to do nothing.
        StateHasChanged();
    }

    protected void DismissError() => Error = null;

    /// <summary>
    /// Host ends the room for everyone, now, rather than waiting on the idle sweep. Guarded the
    /// same way every host power is: the host, or anyone if the host has gone. Closing raises
    /// Changed, so every circuit — including this one — drops to the "room closed" notice.
    /// </summary>
    protected void CloseRoom()
    {
        if (Handle.IsClosed || !Handle.Read(room => room.CanActAsHost(PlayerId)))
        {
            return;
        }

        Rooms.Remove(Handle.Code);
        Handle.Close(RoomCloseReason.HostClosed);
    }

    private void OnRoomChanged()
    {
        Refresh();
        StateHasChanged();
    }

    private void Refresh()
    {
        if (Handle.IsClosed)
        {
            RoomClosed = true;
            ClearView();
            return;
        }

        if (Joined)
        {
            BuildView(Handle, PlayerId);
        }
    }

    public void Dispose()
    {
        if (_changedHandler is not null)
        {
            Handle.Changed -= _changedHandler;
        }

        if (Joined && !Handle.IsClosed)
        {
            try
            {
                Handle.Mutate(room => LeaveRoom(room, PlayerId));
            }
            catch
            {
                // The room may have been closed between the check and the call; nothing to clean up then.
            }
        }

        GC.SuppressFinalize(this);
    }
}

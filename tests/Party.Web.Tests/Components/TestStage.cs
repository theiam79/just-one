using Microsoft.AspNetCore.Components.Rendering;
using Party.Flip7;
using Party.Web.Components.Game;
using Party.Web.Services;

namespace Party.Web.Tests.Components;

/// <summary>
/// The smallest thing that is a stage: it renders whatever the base tells it to and nothing else.
/// </summary>
/// <remarks>
/// Lets the base's own contract be tested without a game's UI in the way. In particular it makes
/// the re-render observable: <see cref="RoomStage{TRoom}.Act"/> has to call StateHasChanged
/// itself, because a rejected move raises no Changed event and the real Act is invoked from a
/// child panel's handler, which only re-renders the child. That gap once made every rule
/// violation in the app fail silently.
/// </remarks>
internal sealed class TestStage : RoomStage<Flip7Room>
{
    public string? Phase { get; private set; }

    public int Renders { get; private set; }

    /// <summary>Runs a move exactly as a child panel's handler would.</summary>
    public void Do(Action<Flip7Room> action) => Act(action);

    public void Dismiss() => DismissError();

    protected override void JoinRoom(Flip7Room room, Guid playerId, string name)
    {
        room.Join(playerId, name);
        room.PlayerConnected(playerId);
    }

    protected override void LeaveRoom(Flip7Room room, Guid playerId) => room.PlayerDisconnected(playerId);

    protected override void BuildView(RoomHandle<Flip7Room> handle, Guid playerId) =>
        Phase = handle.Read(r => r.Phase.ToString());

    protected override void ClearView() => Phase = null;

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        Renders++;

        if (RoomClosed)
        {
            builder.AddMarkupContent(0, "<div class=\"closed\"></div>");
            return;
        }

        builder.AddMarkupContent(1, $"<div class=\"phase\">{Phase}</div>");

        if (Error is not null)
        {
            builder.AddMarkupContent(2, $"<div class=\"banner error\">{Error}</div>");
        }
    }
}

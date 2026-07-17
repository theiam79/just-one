using Bunit;
using Party.Core;
using Party.Web.Components.Game;

namespace Party.Web.Tests.Components;

/// <summary>The shared chat panel: shows messages, marks your own, and sends trimmed text.</summary>
public class ChatPanelTests
{
    private static readonly Guid Me = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid Other = Guid.Parse("00000000-0000-0000-0000-000000000002");

    private static IRenderedComponent<ChatPanel> Render(BunitContext ctx, List<ChatMessage> messages, Action<string>? onSend = null) =>
        ctx.Render<ChatPanel>(p => p
            .Add(x => x.Messages, messages)
            .Add(x => x.MyId, Me)
            .Add(x => x.OnSend, s => onSend?.Invoke(s)));

    [Test]
    public async Task It_shows_messages_and_flags_your_own()
    {
        using var ctx = new BunitContext();
        var panel = Render(ctx,
        [
            new ChatMessage(0, Other, "Bob", "hi all"),
            new ChatMessage(1, Me, "Me", "hello"),
        ]);

        var msgs = panel.FindAll(".chat-msg");
        await Assert.That(msgs).Count().IsEqualTo(2);
        await Assert.That(panel.FindAll(".chat-msg.mine")).Count().IsEqualTo(1);
    }

    [Test]
    public async Task An_empty_room_prompts_you_to_say_hi()
    {
        using var ctx = new BunitContext();
        var panel = Render(ctx, []);

        await Assert.That(panel.FindAll(".chat-msg")).IsEmpty();
        await Assert.That(panel.Find(".chat-empty").TextContent).Contains("say hi");
    }

    [Test]
    public async Task Sending_emits_the_trimmed_text_and_clears_the_box()
    {
        using var ctx = new BunitContext();
        string? sent = null;
        var panel = Render(ctx, [], s => sent = s);

        panel.Find(".chat-input").Input("  well played  ");
        await panel.Find(".chat-form").SubmitAsync();

        await Assert.That(sent).IsEqualTo("well played");
        await Assert.That(panel.Find(".chat-input").GetAttribute("value")).IsNullOrEmpty();
    }

    [Test]
    public async Task A_blank_draft_sends_nothing()
    {
        using var ctx = new BunitContext();
        var sends = 0;
        var panel = Render(ctx, [], _ => sends++);

        panel.Find(".chat-input").Input("   ");
        await panel.Find(".chat-form").SubmitAsync();

        await Assert.That(sends).IsEqualTo(0);
    }
}

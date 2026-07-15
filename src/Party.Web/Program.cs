using Party.Web.Components;
using Party.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddSingleton<RoomManager>();
builder.Services.AddHostedService<RoomJanitor>();

var app = builder.Build();

// No HTTPS redirection: the http endpoint is what gets exposed through devtunnels
// (the tunnel terminates TLS at the edge), and a redirect to localhost https would break guests.
app.UseStaticFiles();
app.UseAntiforgery();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.MapDefaultEndpoints();

app.Run();

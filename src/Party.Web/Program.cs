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
// MapStaticAssets (not UseStaticFiles) so wwwroot files are served under a content hash.
// Without it party.js and app.css keep their bare names, and a returning player's browser
// happily reuses a cached copy from a previous deploy — which is how a stale party.js
// missing a newly added function took down the whole circuit.
app.MapStaticAssets();
app.UseAntiforgery();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.MapDefaultEndpoints();

app.Run();

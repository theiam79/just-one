var builder = DistributedApplication.CreateBuilder(args);

var web = builder.AddProject<Projects.JustOne_Web>("web")
    .WithExternalHttpEndpoints();

// Public access for remote players: a Microsoft dev tunnel in front of the game's
// http endpoint (the tunnel terminates TLS at the edge). Requires the devtunnel CLI
// and a one-time `devtunnel user login`; the public URL shows up on the tunnel
// resource in the dashboard. Anonymous access is on so friends can join without
// a Microsoft account — share the room code, not just the URL.
builder.AddDevTunnel("tunnel")
    .WithAnonymousAccess()
    .WithReference(web.GetEndpoint("http"));

builder.Build().Run();

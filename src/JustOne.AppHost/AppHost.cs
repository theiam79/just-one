var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.JustOne_Web>("web")
    .WithExternalHttpEndpoints();

builder.Build().Run();

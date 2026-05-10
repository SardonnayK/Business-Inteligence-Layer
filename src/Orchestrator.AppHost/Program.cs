var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithImage("pgvector/pgvector", "pg16")
    .WithLifetime(ContainerLifetime.Persistent);

var db = postgres.AddDatabase("orchestrator");

var api = builder.AddProject<Projects.Orchestrator_Api>("api")
    .WithReference(db)
    .WaitFor(db);

builder.AddViteApp("dashboard", "../../src/dashboard")
    .WithReference(api)
    .WithEnvironment("BROWSER", "none");

builder.Build().Run();

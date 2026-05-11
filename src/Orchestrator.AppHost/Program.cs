var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithImage("pgvector/pgvector", "pg16")
    .WithLifetime(ContainerLifetime.Persistent);

var db = postgres.AddDatabase("orchestrator");

var api = builder.AddProject<Projects.Orchestrator_Api>("api")
    .WithReference(db)
    .WaitFor(db)
    // Docker Model Runner (Docker Desktop 4.40+) is always on port 12434 on the host.
    // The API seeds it as the system-default embedding/chat provider on first boot.
    .WithEnvironment("ModelRunner__Endpoint", "http://localhost:12434");

builder.AddViteApp("dashboard", "../../src/dashboard")
    .WithReference(api)
    .WithEnvironment("BROWSER", "none");

builder.Build().Run();

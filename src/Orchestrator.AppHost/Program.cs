var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithImage("pgvector/pgvector", "pg16")
    .WithLifetime(ContainerLifetime.Persistent);

var db = postgres.AddDatabase("orchestrator");

builder.AddProject<Projects.Orchestrator_Api>("api")
    .WithReference(db)
    .WaitFor(db);

builder.Build().Run();

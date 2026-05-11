var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithImage("pgvector/pgvector", "pg16")
    .WithLifetime(ContainerLifetime.Persistent);

var db = postgres.AddDatabase("orchestrator");

// Docker Model Runner is built into Docker Desktop — it runs on the host at port 12434.
// The API picks this up on startup and seeds it as the system-default provider if none is configured.
var modelRunnerEndpoint = builder.AddParameter("ModelRunnerEndpoint", defaultValue: "http://localhost:12434", secret: false);

var api = builder.AddProject<Projects.Orchestrator_Api>("api")
    .WithReference(db)
    .WaitFor(db)
    .WithEnvironment("ModelRunner__Endpoint", modelRunnerEndpoint);

builder.AddViteApp("dashboard", "../../src/dashboard")
    .WithReference(api)
    .WithEnvironment("BROWSER", "none");

builder.Build().Run();

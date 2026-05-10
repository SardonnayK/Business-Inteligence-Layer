var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithImage("pgvector/pgvector", "pg16")
    .WithLifetime(ContainerLifetime.Persistent);

var db = postgres.AddDatabase("orchestrator");

// Set via: dotnet user-secrets set "Parameters:openai-key" "sk-..." --project src/Orchestrator.AppHost
var openAiKey = builder.AddParameter("openai-key", secret: true);

var api = builder.AddProject<Projects.Orchestrator_Api>("api")
    .WithReference(db)
    .WaitFor(db)
    .WithEnvironment("OpenAI__ApiKey", openAiKey);

builder.AddViteApp("dashboard", "../../src/dashboard")
    .WithReference(api)
    .WithEnvironment("BROWSER", "none");

builder.Build().Run();

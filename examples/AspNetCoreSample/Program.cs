using CloudSealed.ML.Engine.Scoring;
using CloudSealed.ML.Engine.Models;

var builder = WebApplication.CreateBuilder(args);

// Register CloudSealed Architecture Analyzer in Dependency Injection
builder.Services.AddSingleton<ArchitectureAnalyzer>();

var app = builder.Build();

app.MapGet("/", () => new
{
    Message = "CloudSealed Predictive-ML-Core Integration Sample",
    Endpoints = new[] { "/health/architecture-risk" }
});

// Endpoint: Evaluate architecture risk on demand
app.MapPost("/health/architecture-risk", (ArchitectureAnalyzer analyzer, PredictArchitectureRequest request) =>
{
    var response = analyzer.Analyze(request);
    return Results.Ok(response);
});

// GET Endpoint with predefined sample system inventory
app.MapGet("/health/architecture-risk", (ArchitectureAnalyzer analyzer) =>
{
    var sampleRequest = new PredictArchitectureRequest
    {
        CompanyName = "Acme Global Enterprise",
        Systems = new List<SystemInput>
        {
            new SystemInput
            {
                Name = "checkout-api",
                Type = "API",
                Criticality = "CRITICAL",
                PublicFacing = true,
                AuthMethod = null // Single point of failure + unauthenticated public risk
            },
            new SystemInput
            {
                Name = "primary-db",
                Type = "DATABASE",
                Criticality = "HIGH",
                PublicFacing = false
            }
        }
    };

    var response = analyzer.Analyze(sampleRequest);
    return Results.Ok(response);
});

app.Run();

using CloudSealed.ML.Engine.Models;
using CloudSealed.ML.Engine.Scoring;

var builder = WebApplication.CreateBuilder(args);

var port = Environment.GetEnvironmentVariable("PORT") ?? "8092";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

const long MaxPayloadBytes = 2_000_000; // ~2MB: inventário de sistemas não precisa de mais que isso
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = MaxPayloadBytes;
});

builder.Services.AddSingleton<ArchitectureAnalyzer>();

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapPost("/v1/predict-architecture", (
    PredictArchitectureRequest? request,
    HttpRequest httpRequest,
    ArchitectureAnalyzer analyzer) =>
{
    var apiKey = Environment.GetEnvironmentVariable("PREDICTIVE_ML_CORE_API_KEY");
    if (!string.IsNullOrEmpty(apiKey))
    {
        var providedKey = httpRequest.Headers["X-Api-Key"].ToString();
        if (providedKey != apiKey)
        {
            return Results.Json(new { error = "X-Api-Key inválida ou ausente" }, statusCode: StatusCodes.Status401Unauthorized);
        }
    }

    if (request is null || string.IsNullOrWhiteSpace(request.CompanyName) || request.Systems is not { Count: > 0 })
    {
        return Results.BadRequest(new { error = "companyName e systems[] são obrigatórios" });
    }

    var response = analyzer.Analyze(request);
    return Results.Ok(response);
});

app.Run();

public partial class Program { }

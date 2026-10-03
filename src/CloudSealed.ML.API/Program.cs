using CloudSealed.ML.Engine;
using CloudSealed.ML.Engine.Models;
using CloudSealed.ML.Engine.Notifications;
using CloudSealed.ML.Engine.Scoring;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

var port = Environment.GetEnvironmentVariable("PORT") ?? "8092";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

const long MaxPayloadBytes = 2_000_000; // ~2MB: system inventory does not need more than that
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = MaxPayloadBytes;
});

builder.Services.AddSingleton<ArchitectureAnalyzer>();
builder.Services.AddHttpClient("webhook");
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "CloudSealed Predictive-ML-Core", Version = "v1" });
    options.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
    {
        Name = "X-Api-Key",
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Header,
        Description = "Only required when PREDICTIVE_ML_CORE_API_KEY is configured on the server.",
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "ApiKey" } },
            Array.Empty<string>()
        },
    });
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

bool IsAuthorized(HttpRequest httpRequest)
{
    var apiKey = Environment.GetEnvironmentVariable("PREDICTIVE_ML_CORE_API_KEY");
    if (string.IsNullOrEmpty(apiKey)) return true;
    return httpRequest.Headers["X-Api-Key"].ToString() == apiKey;
}

app.MapPost("/v1/predict-architecture", async (
    PredictArchitectureRequest? request,
    HttpRequest httpRequest,
    ArchitectureAnalyzer analyzer,
    IHttpClientFactory httpClientFactory) =>
{
    if (!IsAuthorized(httpRequest))
    {
        return Results.Json(new { error = "X-Api-Key invalid or missing" }, statusCode: StatusCodes.Status401Unauthorized);
    }

    if (request is null || string.IsNullOrWhiteSpace(request.CompanyName) || request.Systems is not { Count: > 0 })
    {
        return Results.BadRequest(new { error = "companyName and systems[] are required" });
    }

    var response = analyzer.Analyze(request);

    if (!string.IsNullOrWhiteSpace(request.WebhookUrl))
    {
        await WebhookNotifier.NotifyAsync(
            httpClientFactory.CreateClient("webhook"), request.WebhookUrl, response, request.CompanyName);
    }

    return Results.Ok(response);
});

app.MapPost("/v1/predict-severity", (PredictSeverityRequest? request, HttpRequest httpRequest) =>
{
    if (!IsAuthorized(httpRequest))
    {
        return Results.Json(new { error = "X-Api-Key invalid or missing" }, statusCode: StatusCodes.Status401Unauthorized);
    }

    if (request is null || request.Candidate is null || string.IsNullOrWhiteSpace(request.Candidate.Dimension))
    {
        return Results.BadRequest(new { error = "candidate.dimension is required" });
    }

    var engine = new SeverityPredictionEngine();

    if (request.TrainingReviews.Count < SeverityPredictionEngine.MinimumTrainingSamples)
    {
        return Results.Ok(new PredictSeverityResponse
        {
            Trained = false,
            TrainingSampleCount = request.TrainingReviews.Count,
            MinimumTrainingSamples = SeverityPredictionEngine.MinimumTrainingSamples,
            Message = $"Only {request.TrainingReviews.Count} real review(s) available; " +
                $"at least {SeverityPredictionEngine.MinimumTrainingSamples} are required to train without overfit. " +
                "Use the default/static severity until more real reviews have been accumulated.",
        });
    }

    var trainingData = request.TrainingReviews.Select(r => new FindingReview
    {
        Dimension = r.Dimension,
        Category = r.Category,
        Title = r.Title,
        Description = r.Description,
        Severity = r.Severity ?? "",
    });

    engine.TrainModel(trainingData);

    var (predicted, probabilities) = engine.Predict(new FindingReview
    {
        Dimension = request.Candidate.Dimension,
        Category = request.Candidate.Category,
        Title = request.Candidate.Title,
        Description = request.Candidate.Description,
    });

    return Results.Ok(new PredictSeverityResponse
    {
        Trained = true,
        TrainingSampleCount = engine.TrainingSampleCount,
        MinimumTrainingSamples = SeverityPredictionEngine.MinimumTrainingSamples,
        PredictedSeverity = predicted,
        ClassProbabilities = probabilities,
    });
});

app.Run();

public partial class Program { }

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

app.MapPost("/v1/forecast-billing", (BillingForecastRequest? request, HttpRequest httpRequest) =>
{
    if (!IsAuthorized(httpRequest))
    {
        return Results.Json(new { error = "X-Api-Key invalid or missing" }, statusCode: StatusCodes.Status401Unauthorized);
    }

    if (request is null || request.DailySpends is null || request.DailySpends.Count == 0)
    {
        return Results.BadRequest(new { error = "dailySpends is required and cannot be empty" });
    }

    var forecaster = new CloudBillingForecaster();
    var prediction = forecaster.ForecastMonthEnd(request.DailySpends, request.RemainingDays);
    return Results.Ok(prediction);
});

app.MapPost("/v1/predict-financial-blast-radius", (List<ServiceFinancialNode>? nodes, HttpRequest httpRequest) =>
{
    if (!IsAuthorized(httpRequest))
    {
        return Results.Json(new { error = "X-Api-Key invalid or missing" }, statusCode: StatusCodes.Status401Unauthorized);
    }

    if (nodes is null || nodes.Count == 0)
    {
        return Results.BadRequest(new { error = "nodes array is required and cannot be empty" });
    }

    var analyzer = new FinancialBlastRadiusAnalyzer();
    var report = analyzer.Analyze(nodes);
    return Results.Ok(report);
});

app.MapPost("/v1/evaluate-circuit-breaker", (CloudSealed.ML.Engine.Policies.FinancialCircuitBreakerPolicy.CircuitBreakerInput? input, HttpRequest httpRequest) =>
{
    if (!IsAuthorized(httpRequest))
    {
        return Results.Json(new { error = "X-Api-Key invalid or missing" }, statusCode: StatusCodes.Status401Unauthorized);
    }

    if (input is null)
    {
        return Results.BadRequest(new { error = "circuit breaker input is required" });
    }

    var policy = new CloudSealed.ML.Engine.Policies.FinancialCircuitBreakerPolicy();
    var result = policy.Evaluate(input);
    return Results.Ok(result);
});

app.MapPost("/v1/analyze-genai-tokens", (GenAITokenForensicsRequest? request, HttpRequest httpRequest) =>
{
    if (!IsAuthorized(httpRequest))
    {
        return Results.Json(new { error = "X-Api-Key invalid or missing" }, statusCode: StatusCodes.Status401Unauthorized);
    }

    if (request is null || request.Models is null || request.Models.Count == 0)
    {
        return Results.BadRequest(new { error = "models list is required and cannot be empty" });
    }

    var analyzer = new GenAITokenForensicsAnalyzer();
    var report = analyzer.Analyze(request.Models, request.Rag);
    return Results.Ok(report);
});

app.MapPost("/v1/evaluate-commitment-risk", (CommitmentPlanInput? input, HttpRequest httpRequest) =>
{
    if (!IsAuthorized(httpRequest))
    {
        return Results.Json(new { error = "X-Api-Key invalid or missing" }, statusCode: StatusCodes.Status401Unauthorized);
    }

    if (input is null)
    {
        return Results.BadRequest(new { error = "commitment plan input is required" });
    }

    var analyzer = new CommitmentRiskAnalyzer();
    var report = analyzer.Analyze(input);
    return Results.Ok(report);
});

app.MapPost("/v1/optimize-spot-fleet", (SpotFleetOptimizationRequest? request, HttpRequest httpRequest) =>
{
    if (!IsAuthorized(httpRequest))
    {
        return Results.Json(new { error = "X-Api-Key invalid or missing" }, statusCode: StatusCodes.Status401Unauthorized);
    }

    if (request is null || request.Requirement is null || request.Candidates is null || request.Candidates.Count == 0)
    {
        return Results.BadRequest(new { error = "requirement and candidates list are required" });
    }

    var engine = new SpotTerminationRiskEngine();
    var report = engine.OptimizeFleet(request.Requirement, request.Candidates);
    return Results.Ok(report);
});

app.Run();

public class BillingForecastRequest
{
    public List<float> DailySpends { get; set; } = new();
    public int RemainingDays { get; set; } = 15;
}

public class GenAITokenForensicsRequest
{
    public List<ModelUsageTelemetry> Models { get; set; } = new();
    public RagPipelineTelemetry? Rag { get; set; }
}

public class SpotFleetOptimizationRequest
{
    public WorkloadRequirement Requirement { get; set; } = new();
    public List<SpotPoolCandidate> Candidates { get; set; } = new();
}

public partial class Program { }


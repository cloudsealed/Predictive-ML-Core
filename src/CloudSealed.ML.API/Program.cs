using CloudSealed.ML.Engine.Models;
using CloudSealed.ML.Engine.Scoring;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

var port = Environment.GetEnvironmentVariable("PORT") ?? "8092";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

const long MaxPayloadBytes = 2_000_000; // ~2MB: inventário de sistemas não precisa de mais que isso
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = MaxPayloadBytes;
});

builder.Services.AddSingleton<ArchitectureAnalyzer>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "CloudSealed Predictive-ML-Core", Version = "v1" });
    options.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
    {
        Name = "X-Api-Key",
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Header,
        Description = "Só é exigida quando PREDICTIVE_ML_CORE_API_KEY está configurada no servidor.",
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

using System.Net;
using System.Net.Http.Json;
using CloudSealed.ML.Engine.Models;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CloudSealed.ML.Tests;

// Env var tests (X-Api-Key) mutate process state, so this class
// must not run in parallel with any other that also touches PREDICTIVE_ML_CORE_API_KEY.
// See xunit.runner.json/CollectionBehavior in the assembly.
public class ApiEndpointTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;

    public ApiEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        Environment.SetEnvironmentVariable("PREDICTIVE_ML_CORE_API_KEY", null);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("PREDICTIVE_ML_CORE_API_KEY", null);
    }

    private static PredictArchitectureRequest SamplePayload() => new()
    {
        CompanyName = "Acme",
        Systems =
        [
            new SystemInput
            {
                Name = "checkout-api",
                Type = "API",
                Criticality = "CRITICAL",
                PublicFacing = true,
            },
        ],
    };

    [Fact]
    public async Task Health_ReturnsOk()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Predict_MissingCompanyName_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/v1/predict-architecture", new { systems = Array.Empty<object>() });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Predict_ValidPayloadNoApiKeyConfigured_ReturnsOkWithContractShape()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/v1/predict-architecture", SamplePayload());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PredictArchitectureResponse>();
        Assert.NotNull(body);
        var prediction = Assert.Single(body!.Predictions);
        Assert.Equal("checkout-api", prediction.SystemName);
        Assert.InRange(body.OverallArchitectureScore, 0, 100);
    }

    [Fact]
    public async Task Predict_ApiKeyConfiguredButMissingHeader_ReturnsUnauthorized()
    {
        Environment.SetEnvironmentVariable("PREDICTIVE_ML_CORE_API_KEY", "secret-key");
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/v1/predict-architecture", SamplePayload());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Predict_ApiKeyConfiguredWithCorrectHeader_ReturnsOk()
    {
        Environment.SetEnvironmentVariable("PREDICTIVE_ML_CORE_API_KEY", "secret-key");
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "secret-key");

        var response = await client.PostAsJsonAsync("/v1/predict-architecture", SamplePayload());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static FindingReviewInput SampleCandidate() => new()
    {
        Dimension = "SECURITY",
        Category = "Identity & Access",
        Title = "MFA missing on admin account",
        Description = "Admin account with elevated privileges has no MFA enabled.",
    };

    private static List<FindingReviewInput> SyntheticTrainingReviews(int count)
    {
        var reviews = new List<FindingReviewInput>();
        for (var i = 0; i < count; i++)
        {
            var authIssue = i % 2 == 0;
            reviews.Add(new FindingReviewInput
            {
                Dimension = authIssue ? "SECURITY" : "COST",
                Category = authIssue ? "Identity & Access" : "Optimization",
                Title = authIssue ? "MFA missing on admin account" : "Idle resource detected",
                Description = authIssue
                    ? "Admin account with elevated privileges has no MFA enabled."
                    : "Instance unused for the last 30 days.",
                Severity = authIssue ? "CRITICAL" : "MEDIUM",
            });
        }
        return reviews;
    }

    [Fact]
    public async Task PredictSeverity_MissingCandidateDimension_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/v1/predict-severity",
            new { trainingReviews = Array.Empty<object>(), candidate = new { } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PredictSeverity_BelowMinimumSamples_ReturnsUntrainedWithMessage()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/v1/predict-severity", new PredictSeverityRequest
        {
            TrainingReviews = SyntheticTrainingReviews(5).Select(r => r).ToList(),
            Candidate = SampleCandidate(),
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PredictSeverityResponse>();
        Assert.NotNull(body);
        Assert.False(body!.Trained);
        Assert.Null(body.PredictedSeverity);
        Assert.Contains("Use the default/static severity", body.Message);
    }

    [Fact]
    public async Task PredictSeverity_AtMinimumSamples_TrainsAndPredicts()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/v1/predict-severity", new PredictSeverityRequest
        {
            TrainingReviews = SyntheticTrainingReviews(40),
            Candidate = SampleCandidate(),
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PredictSeverityResponse>();
        Assert.NotNull(body);
        Assert.True(body!.Trained);
        Assert.Equal(40, body.TrainingSampleCount);
        Assert.False(string.IsNullOrEmpty(body.PredictedSeverity));
        Assert.NotEmpty(body.ClassProbabilities);
    }
}

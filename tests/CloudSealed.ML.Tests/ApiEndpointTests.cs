using System.Net;
using System.Net.Http.Json;
using CloudSealed.ML.Engine.Models;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CloudSealed.ML.Tests;

// Testes de env var (X-Api-Key) mutam estado de processo, então esta classe
// não pode rodar em paralelo com outra que também mexa em PREDICTIVE_ML_CORE_API_KEY.
// Ver xunit.runner.json/CollectionBehavior no assembly.
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
}

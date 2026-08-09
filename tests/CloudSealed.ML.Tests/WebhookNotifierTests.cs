using System.Net;
using System.Text.Json;
using CloudSealed.ML.Engine.Models;
using CloudSealed.ML.Engine.Notifications;

namespace CloudSealed.ML.Tests;

public class WebhookNotifierTests
{
    private sealed class RecordingHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new HttpRequestException("boom");
    }

    private static PredictArchitectureResponse ResponseWith(string severity) => new()
    {
        ArchitectureSummary = "1 finding.",
        OverallArchitectureScore = 58,
        Predictions =
        [
            new ArchitecturePrediction
            {
                SystemName = "checkout-api",
                Findings = [new Finding { Title = "Ponto único de falha", Severity = severity, Description = "..." }],
                Recommendations = [new Recommendation { Title = "Adicionar redundância", Effort = "MEDIUM" }],
            },
        ],
    };

    [Fact]
    public void IsSlackWebhook_DetectsSlackUrls()
    {
        Assert.True(WebhookNotifier.IsSlackWebhook("https://hooks.slack.com/services/x"));
        Assert.False(WebhookNotifier.IsSlackWebhook("https://example.com/webhook"));
    }

    [Fact]
    public async Task NotifyAsync_SendsSlackPayload_WhenSeverityMeetsThreshold()
    {
        var handler = new RecordingHandler();
        using var client = new HttpClient(handler);

        var sent = await WebhookNotifier.NotifyAsync(
            client, "https://hooks.slack.com/services/x", ResponseWith("CRITICAL"), "Acme");

        Assert.True(sent);
        Assert.NotNull(handler.LastBody);
        using var doc = JsonDocument.Parse(handler.LastBody!);
        Assert.True(doc.RootElement.TryGetProperty("text", out var text));
        Assert.Contains("Acme", text.GetString());
    }

    [Fact]
    public async Task NotifyAsync_SendsFullResponse_ForGenericWebhook()
    {
        var handler = new RecordingHandler();
        using var client = new HttpClient(handler);

        await WebhookNotifier.NotifyAsync(client, "https://example.com/webhook", ResponseWith("CRITICAL"), "Acme");

        using var doc = JsonDocument.Parse(handler.LastBody!);
        Assert.True(doc.RootElement.TryGetProperty("overallArchitectureScore", out _));
    }

    [Fact]
    public async Task NotifyAsync_SkipsWhenBelowThreshold()
    {
        var handler = new RecordingHandler();
        using var client = new HttpClient(handler);

        var sent = await WebhookNotifier.NotifyAsync(client, "https://example.com/webhook", ResponseWith("LOW"), "Acme");

        Assert.False(sent);
        Assert.Null(handler.LastRequest);
    }

    [Fact]
    public async Task NotifyAsync_SwallowsNetworkErrors()
    {
        using var client = new HttpClient(new ThrowingHandler());

        var sent = await WebhookNotifier.NotifyAsync(client, "https://example.com/webhook", ResponseWith("CRITICAL"), "Acme");

        Assert.False(sent);
    }
}

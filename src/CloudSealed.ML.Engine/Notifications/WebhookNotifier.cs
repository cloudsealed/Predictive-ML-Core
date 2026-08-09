using System.Net.Http.Json;
using System.Text.Json;
using CloudSealed.ML.Engine.Models;

namespace CloudSealed.ML.Engine.Notifications;

// Efeito colateral, não parte do contrato de análise: uma falha de rede é
// engolida (nunca propagada), para que um webhook fora do ar não derrube uma
// resposta que já foi calculada com sucesso. Espelha cloudsealed_jit/notify.py.
public static class WebhookNotifier
{
    private static readonly Dictionary<string, int> SeverityRank = new()
    {
        ["LOW"] = 0,
        ["MEDIUM"] = 1,
        ["HIGH"] = 2,
        ["CRITICAL"] = 3,
    };

    public static bool IsSlackWebhook(string url) => url.Contains("hooks.slack.com");

    public static object BuildSlackPayload(PredictArchitectureResponse response, string companyName)
    {
        var lines = new List<string>
        {
            $"*🏗️ Predictive-ML-Core — {companyName}*",
            response.ArchitectureSummary,
            $"Overall architecture score: *{response.OverallArchitectureScore}/100*",
        };

        foreach (var prediction in response.Predictions)
        {
            foreach (var finding in prediction.Findings.Take(5))
            {
                lines.Add($"• `{prediction.SystemName}` *{finding.Severity}* — {finding.Title}");
            }
        }

        foreach (var prediction in response.Predictions)
        {
            foreach (var recommendation in prediction.Recommendations.Take(3))
            {
                lines.Add($"→ *{recommendation.Title}* ({prediction.SystemName}, effort {recommendation.Effort})");
            }
        }

        return new { text = string.Join("\n", lines) };
    }

    private static bool MeetsThreshold(PredictArchitectureResponse response, string minSeverity)
    {
        var threshold = SeverityRank.GetValueOrDefault(minSeverity.ToUpperInvariant(), SeverityRank["HIGH"]);
        return response.Predictions
            .SelectMany(p => p.Findings)
            .Any(f => SeverityRank.GetValueOrDefault(f.Severity, 0) >= threshold);
    }

    // Retorna se o webhook foi enviado (false = abaixo do limiar ou falha de rede).
    public static async Task<bool> NotifyAsync(
        HttpClient client,
        string webhookUrl,
        PredictArchitectureResponse response,
        string companyName,
        string minSeverity = "HIGH")
    {
        if (!MeetsThreshold(response, minSeverity))
        {
            return false;
        }

        object payload = IsSlackWebhook(webhookUrl)
            ? BuildSlackPayload(response, companyName)
            : response;

        try
        {
            using var httpResponse = await client.PostAsJsonAsync(webhookUrl, payload);
            return true;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return false;
        }
    }
}

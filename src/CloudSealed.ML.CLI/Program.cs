using System.Text.Json;
using CloudSealed.ML.Engine.Models;
using CloudSealed.ML.Engine.Notifications;
using CloudSealed.ML.Engine.Reporting;
using CloudSealed.ML.Engine.Scoring;

namespace CloudSealed.ML.CLI;

// Roda a mesma análise do endpoint HTTP a partir de um arquivo JSON local, sem
// subir servidor. Equivalente ao `cloudsealed-jit export.csv [--json]` do JIT.
internal static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private static async Task<int> Main(string[] args)
    {
        var webhookUrl = ExtractOptionValue(args, "--webhook-url");
        var htmlPath = ExtractOptionValue(args, "--html");
        var consumed = new[] { "--json", webhookUrl, "--webhook-url", htmlPath, "--html" };
        var positional = args.Where(a => !consumed.Contains(a)).ToArray();
        if (positional.Length != 1)
        {
            Console.Error.WriteLine(
                "Uso: cloudsealed-predictive-ml <inventory.json> [--json] [--html PATH] [--webhook-url URL]");
            return 1;
        }

        var inputPath = positional[0];
        if (!File.Exists(inputPath))
        {
            Console.Error.WriteLine($"Arquivo não encontrado: {inputPath}");
            return 1;
        }

        PredictArchitectureRequest? request;
        try
        {
            var raw = File.ReadAllText(inputPath);
            request = JsonSerializer.Deserialize<PredictArchitectureRequest>(raw, JsonOptions);
        }
        catch (JsonException ex)
        {
            Console.Error.WriteLine($"JSON inválido: {ex.Message}");
            return 1;
        }

        if (request is null || string.IsNullOrWhiteSpace(request.CompanyName) || request.Systems is not { Count: > 0 })
        {
            Console.Error.WriteLine("companyName e systems[] são obrigatórios no JSON de entrada.");
            return 1;
        }

        var response = new ArchitectureAnalyzer().Analyze(request);

        if (webhookUrl is not null)
        {
            using var client = new HttpClient();
            await WebhookNotifier.NotifyAsync(client, webhookUrl, response, request.CompanyName);
        }

        if (htmlPath is not null)
        {
            await File.WriteAllTextAsync(htmlPath, HtmlReportRenderer.Render(response, request.CompanyName));
        }

        if (args.Contains("--json"))
        {
            Console.WriteLine(JsonSerializer.Serialize(response, JsonOptions));
            return 0;
        }

        PrintHumanReadable(request, response);
        return 0;
    }

    private static string? ExtractOptionValue(string[] args, string optionName)
    {
        var index = Array.IndexOf(args, optionName);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }

    private static void PrintHumanReadable(PredictArchitectureRequest request, PredictArchitectureResponse response)
    {
        Console.WriteLine($"Predictive-ML-Core — {request.CompanyName}");
        Console.WriteLine(response.ArchitectureSummary);
        Console.WriteLine();

        foreach (var prediction in response.Predictions)
        {
            Console.WriteLine($"# {prediction.SystemName}");
            Console.WriteLine($"  SPOF={prediction.RiskScores.SinglePointOfFailure} " +
                $"Coupling={prediction.RiskScores.ExcessiveCoupling} " +
                $"ScalabilityGap={prediction.RiskScores.ScalabilityGap}");

            foreach (var finding in prediction.Findings)
            {
                Console.WriteLine($"  [{finding.Severity}] {finding.Title} — {finding.Description}");
                Console.WriteLine($"    Remediação: {finding.Remediation}");
            }

            foreach (var recommendation in prediction.Recommendations)
            {
                Console.WriteLine($"  Recomendação ({recommendation.Effort}): {recommendation.Title} — {recommendation.Description}");
            }

            Console.WriteLine();
        }
    }
}

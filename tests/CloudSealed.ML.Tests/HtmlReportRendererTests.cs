using CloudSealed.ML.Engine.Models;
using CloudSealed.ML.Engine.Reporting;

namespace CloudSealed.ML.Tests;

public class HtmlReportRendererTests
{
    private static PredictArchitectureResponse SampleResponse() => new()
    {
        ArchitectureSummary = "1 finding.",
        OverallArchitectureScore = 58,
        Predictions =
        [
            new ArchitecturePrediction
            {
                SystemName = "checkout-api",
                Findings = [new Finding { Title = "Ponto único de falha", Severity = "CRITICAL", Description = "..." }],
                Recommendations = [new Recommendation { Title = "Adicionar redundância", Effort = "MEDIUM", Description = "..." }],
            },
        ],
    };

    [Fact]
    public void Render_IsWellFormedHtml()
    {
        var output = HtmlReportRenderer.Render(SampleResponse(), "Acme");

        Assert.StartsWith("<!DOCTYPE html>", output.TrimStart());
        Assert.Equal(CountOccurrences(output, "<table>"), CountOccurrences(output, "</table>"));
    }

    [Fact]
    public void Render_IncludesKeyValues()
    {
        var output = HtmlReportRenderer.Render(SampleResponse(), "Acme");

        Assert.Contains("Acme", output);
        Assert.Contains("58/100", output);
        Assert.Contains("checkout-api", output);
        Assert.Contains("CRITICAL", output);
        Assert.Contains("Adicionar redundância", output);
    }

    [Fact]
    public void Render_HandlesNoFindings()
    {
        var response = new PredictArchitectureResponse { ArchitectureSummary = "Nothing found." };

        var output = HtmlReportRenderer.Render(response, "Acme");

        Assert.Contains("None found.", output);
        Assert.Contains("None.", output);
    }

    [Fact]
    public void Render_EscapesUntrustedText()
    {
        var response = SampleResponse();
        response.Predictions[0].Findings[0].Description = "<script>alert(1)</script>";

        var output = HtmlReportRenderer.Render(response, "Acme");

        Assert.DoesNotContain("<script>alert(1)</script>", output);
        Assert.Contains("&lt;script&gt;", output);
    }

    private static int CountOccurrences(string source, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = source.IndexOf(value, index, StringComparison.Ordinal)) != -1)
        {
            count++;
            index += value.Length;
        }
        return count;
    }
}

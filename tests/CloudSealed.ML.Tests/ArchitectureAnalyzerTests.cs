using CloudSealed.ML.Engine.Models;
using CloudSealed.ML.Engine.Scoring;

namespace CloudSealed.ML.Tests;

public class ArchitectureAnalyzerTests
{
    private readonly ArchitectureAnalyzer _analyzer = new();

    private static SystemInput LowRiskSystem(string name = "internal-tool") => new()
    {
        Name = name,
        Type = "APPLICATION",
        Criticality = "LOW",
        PublicFacing = false,
    };

    private static SystemInput HighRiskSystem(string name = "checkout-api") => new()
    {
        Name = name,
        Type = "THIRD_PARTY_SERVICE",
        Criticality = "CRITICAL",
        PublicFacing = true,
        AuthMethod = null,
    };

    [Fact]
    public void Analyze_LowRiskSystem_ProducesNoFindings()
    {
        var request = new PredictArchitectureRequest
        {
            CompanyName = "Acme",
            Systems = [LowRiskSystem()],
        };

        var response = _analyzer.Analyze(request);

        var prediction = Assert.Single(response.Predictions);
        Assert.Empty(prediction.Findings);
        Assert.True(prediction.RiskScores.SinglePointOfFailure < RiskRules.SpofFindingThreshold);
        Assert.True(prediction.RiskScores.ExcessiveCoupling < RiskRules.CouplingFindingThreshold);
    }

    [Fact]
    public void Analyze_CriticalPublicThirdPartyNoAuth_TriggersSpofAndCouplingFindings()
    {
        var request = new PredictArchitectureRequest
        {
            CompanyName = "Acme",
            Systems = [HighRiskSystem()],
        };

        var response = _analyzer.Analyze(request);

        var prediction = Assert.Single(response.Predictions);
        Assert.True(prediction.RiskScores.SinglePointOfFailure >= RiskRules.SpofFindingThreshold);
        Assert.True(prediction.RiskScores.ExcessiveCoupling >= RiskRules.CouplingFindingThreshold);
        Assert.Contains(prediction.Findings, f => f.Title.Contains("Single point of failure"));
        Assert.Contains(prediction.Findings, f => f.Title.Contains("Excessive coupling"));
        Assert.Contains(prediction.Recommendations, r => r.Title == "Implement redundancy");
    }

    [Fact]
    public void Analyze_PublicFacingWithAuth_HasLowerCouplingThanWithoutAuth()
    {
        var withAuth = new SystemInput
        {
            Name = "checkout-api",
            Type = "APPLICATION",
            Criticality = "CRITICAL",
            PublicFacing = true,
            AuthMethod = "OAUTH2",
        };
        var withoutAuth = new SystemInput
        {
            Name = "checkout-api",
            Type = "APPLICATION",
            Criticality = "CRITICAL",
            PublicFacing = true,
            AuthMethod = null,
        };

        var scoreWithAuth = _analyzer.Analyze(new PredictArchitectureRequest { CompanyName = "Acme", Systems = [withAuth] })
            .Predictions[0].RiskScores.ExcessiveCoupling;
        var scoreWithoutAuth = _analyzer.Analyze(new PredictArchitectureRequest { CompanyName = "Acme", Systems = [withoutAuth] })
            .Predictions[0].RiskScores.ExcessiveCoupling;

        Assert.True(scoreWithAuth < scoreWithoutAuth);
    }

    [Fact]
    public void Analyze_FanOutOfThirdPartyServices_IncreasesCouplingBeyondFreeCount()
    {
        SystemInput ThirdParty(string name) => new()
        {
            Name = name,
            Type = "THIRD_PARTY_SERVICE",
            Criticality = "LOW",
            PublicFacing = false,
        };

        var twoDependencies = new PredictArchitectureRequest
        {
            CompanyName = "Acme",
            Systems = [ThirdParty("a"), ThirdParty("b")],
        };
        var fiveDependencies = new PredictArchitectureRequest
        {
            CompanyName = "Acme",
            Systems = [ThirdParty("a"), ThirdParty("b"), ThirdParty("c"), ThirdParty("d"), ThirdParty("e")],
        };

        var couplingWithTwo = _analyzer.Analyze(twoDependencies).Predictions[0].RiskScores.ExcessiveCoupling;
        var couplingWithFive = _analyzer.Analyze(fiveDependencies).Predictions[0].RiskScores.ExcessiveCoupling;

        Assert.True(couplingWithFive > couplingWithTwo);
    }

    [Fact]
    public void Analyze_HistoricalMetricsWithHighP99_TriggersScalabilityFinding()
    {
        var request = new PredictArchitectureRequest
        {
            CompanyName = "Acme",
            Systems = [LowRiskSystem()],
            HistoricalMetrics = new HistoricalMetrics { AvgLatencyMs = 100, P99LatencyMs = 1500 },
        };

        var response = _analyzer.Analyze(request);

        var prediction = response.Predictions[0];
        Assert.True(prediction.RiskScores.ScalabilityGap >= RiskRules.ScalabilityFindingThreshold);
        Assert.Contains(prediction.Findings, f => f.Title.Contains("Scalability gap"));
        Assert.Contains(prediction.Findings, f => f.Description.Contains("historicalMetrics declared in the request"));
    }

    [Fact]
    public void Analyze_NoHistoricalMetrics_CriticalDatabaseWithSensitiveData_TriggersConditionalScalabilityFinding()
    {
        var system = new SystemInput
        {
            Name = "customer-db",
            Type = "DATABASE",
            Criticality = "CRITICAL",
            PublicFacing = false,
            DataSensitivity = "PII",
        };

        var response = _analyzer.Analyze(new PredictArchitectureRequest { CompanyName = "Acme", Systems = [system] });

        var prediction = response.Predictions[0];
        Assert.True(prediction.RiskScores.ScalabilityGap >= RiskRules.ScalabilityFindingThreshold);
        Assert.Contains(prediction.Findings, f => f.Description.Contains("unknown risk"));
    }

    [Fact]
    public void Analyze_OverallScore_WeightsCriticalSystemMoreThanManyLowSystems()
    {
        var manyLowSystems = Enumerable.Range(0, 9).Select(i => LowRiskSystem($"low-{i}")).ToList();
        var withOneCritical = new List<SystemInput>(manyLowSystems) { HighRiskSystem() };

        var baselineScore = _analyzer.Analyze(new PredictArchitectureRequest { CompanyName = "Acme", Systems = manyLowSystems })
            .OverallArchitectureScore;
        var withCriticalScore = _analyzer.Analyze(new PredictArchitectureRequest { CompanyName = "Acme", Systems = withOneCritical })
            .OverallArchitectureScore;

        // A naive average of 9 LOW + 1 CRITICAL would dilute the CRITICAL to ~10% weight;
        // criticality weighting must pull the score noticeably lower.
        Assert.True(baselineScore - withCriticalScore > 10);
    }

    [Fact]
    public void Analyze_EmptySystemsIsNotReachedByAnalyzer_ButHandlesGracefullyIfCalled()
    {
        var response = _analyzer.Analyze(new PredictArchitectureRequest { CompanyName = "Acme", Systems = [] });

        Assert.Empty(response.Predictions);
        Assert.Equal(100, response.OverallArchitectureScore);
    }
}

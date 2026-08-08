using CloudSealed.ML.Engine.Models;
using CloudSealed.ML.Engine.Scoring;

namespace CloudSealed.ML.Tests;

// A explainability só vale se for fiel: cada score precisa ser exatamente a
// soma (com teto) das contribuições que o breakdown declara. Se um dia o
// analyzer somar um peso "escondido" que não aparece no breakdown, estes
// testes quebram.
public class ScoreBreakdownTests
{
    private readonly ArchitectureAnalyzer _analyzer = new();

    private static PredictArchitectureRequest Req(params SystemInput[] systems) =>
        new() { CompanyName = "Acme", Systems = systems.ToList() };

    private static SystemInput System(
        string name = "svc",
        string type = "API",
        string criticality = "CRITICAL",
        bool publicFacing = true,
        string? auth = null,
        string? sensitivity = null) => new()
    {
        Name = name,
        Type = type,
        Criticality = criticality,
        PublicFacing = publicFacing,
        AuthMethod = auth,
        DataSensitivity = sensitivity,
    };

    private static int Capped(IEnumerable<RuleContribution> contributions) =>
        Math.Min(contributions.Sum(c => c.Points), 100);

    [Fact]
    public void Breakdown_ReconstructsEveryRiskScore()
    {
        var response = _analyzer.Analyze(Req(
            System(name: "checkout", type: "API", criticality: "CRITICAL", publicFacing: true, auth: null),
            System(name: "db", type: "DATABASE", criticality: "HIGH", publicFacing: false, sensitivity: "PII"),
            System(name: "vendor", type: "THIRD_PARTY_SERVICE", criticality: "CRITICAL", publicFacing: false)));

        foreach (var p in response.Predictions)
        {
            Assert.Equal(p.RiskScores.SinglePointOfFailure, Capped(p.ScoreBreakdown.SinglePointOfFailure));
            Assert.Equal(p.RiskScores.ExcessiveCoupling, Capped(p.ScoreBreakdown.ExcessiveCoupling));
            Assert.Equal(p.RiskScores.ScalabilityGap, Capped(p.ScoreBreakdown.ScalabilityGap));
        }
    }

    [Fact]
    public void EveryContribution_HasRuleKeyAndRationale()
    {
        var response = _analyzer.Analyze(Req(System(auth: null)));
        var p = Assert.Single(response.Predictions);

        var all = p.ScoreBreakdown.SinglePointOfFailure
            .Concat(p.ScoreBreakdown.ExcessiveCoupling)
            .Concat(p.ScoreBreakdown.ScalabilityGap);

        Assert.NotEmpty(all);
        foreach (var c in all)
        {
            Assert.False(string.IsNullOrWhiteSpace(c.Rule));
            Assert.False(string.IsNullOrWhiteSpace(c.Rationale));
            Assert.True(c.Points > 0);
        }
    }

    [Fact]
    public void RemovingAuth_RaisesCoupling_AndShowsUpInBreakdown()
    {
        var withAuth = _analyzer.Analyze(Req(System(type: "APPLICATION", auth: "OAUTH2"))).Predictions[0];
        var withoutAuth = _analyzer.Analyze(Req(System(type: "APPLICATION", auth: null))).Predictions[0];

        Assert.True(withoutAuth.RiskScores.ExcessiveCoupling > withAuth.RiskScores.ExcessiveCoupling);
        Assert.Contains(withoutAuth.ScoreBreakdown.ExcessiveCoupling, c => c.Rule.Contains("authMethod=null"));
    }

    [Fact]
    public void Response_CarriesEngineProvenance()
    {
        var response = _analyzer.Analyze(Req(System()));
        Assert.Equal(EngineInfo.Version, response.EngineVersion);
        Assert.Equal(EngineInfo.Method, response.Method);
        Assert.Equal("deterministic-rule-scoring", response.Method);
    }
}

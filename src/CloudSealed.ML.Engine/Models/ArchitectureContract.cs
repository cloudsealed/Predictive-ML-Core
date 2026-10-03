namespace CloudSealed.ML.Engine.Models;

// Mirrors 1:1 framework4d-predictive-ml-client.ts (cloudsealed-os).
// Field names and types do not change without versioning the endpoint.

public class SystemInput
{
    public string Name { get; set; } = string.Empty;

    // 'APPLICATION' | 'DATABASE' | 'API' | 'THIRD_PARTY_SERVICE'
    public string Type { get; set; } = string.Empty;

    // 'LOW' | 'MEDIUM' | 'HIGH' | 'CRITICAL'
    public string Criticality { get; set; } = string.Empty;

    public bool PublicFacing { get; set; }

    public string? DataSensitivity { get; set; }

    public string? AuthMethod { get; set; }
}

public class HistoricalMetrics
{
    public double? AvgLatencyMs { get; set; }

    public double? P99LatencyMs { get; set; }

    public double? RequestsPerSecond { get; set; }
}

public class PredictArchitectureRequest
{
    public string CompanyName { get; set; } = string.Empty;

    public List<SystemInput> Systems { get; set; } = new();

    public HistoricalMetrics? HistoricalMetrics { get; set; }

    // Optional/additive: if set, the result is sent to this URL
    // (Slack incoming webhook or generic listener) when any finding
    // reaches HIGH/CRITICAL. See WebhookNotifier.
    public string? WebhookUrl { get; set; }
}

public class RiskScores
{
    public int SinglePointOfFailure { get; set; }

    public int ExcessiveCoupling { get; set; }

    public int ScalabilityGap { get; set; }
}

// A rule that fired and its point contribution to a risk score.
// This is what makes scoring auditable: every point traceable to a field in the
// request and the reason it carries weight. It is the explicit differentiator
// against a black-box model.
public class RuleContribution
{
    public string Rule { get; set; } = string.Empty; // stable key, e.g. "criticality=CRITICAL"

    public int Points { get; set; } // points added to the dimension score

    public string Rationale { get; set; } = string.Empty; // why this rule carries weight
}

// Breakdown, by risk dimension, of the rules that produced each score.
// The final score is min(sum of points, 100) — the sum may exceed the cap.
public class ScoreBreakdown
{
    public List<RuleContribution> SinglePointOfFailure { get; set; } = new();

    public List<RuleContribution> ExcessiveCoupling { get; set; } = new();

    public List<RuleContribution> ScalabilityGap { get; set; } = new();
}

public class Finding
{
    public string Title { get; set; } = string.Empty;

    // 'LOW' | 'MEDIUM' | 'HIGH' | 'CRITICAL'
    public string Severity { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Remediation { get; set; } = string.Empty;
}

public class Recommendation
{
    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    // 'LOW' | 'MEDIUM' | 'HIGH'
    public string Effort { get; set; } = string.Empty;
}

public class ArchitecturePrediction
{
    public string SystemName { get; set; } = string.Empty;

    public RiskScores RiskScores { get; set; } = new();

    // Optional/additive to the contract: traceable breakdown of each riskScore.
    // Old clients ignore it; new clients can audit the calculation.
    public ScoreBreakdown ScoreBreakdown { get; set; } = new();

    public List<Finding> Findings { get; set; } = new();

    public List<Recommendation> Recommendations { get; set; } = new();
}

public class PredictArchitectureResponse
{
    public List<ArchitecturePrediction> Predictions { get; set; } = new();

    public string ArchitectureSummary { get; set; } = string.Empty;

    public int OverallArchitectureScore { get; set; }

    // Provenance: which engine/method generated this result. Persisted by the
    // consumer alongside the findings to track the origin in audits.
    public string EngineVersion { get; set; } = EngineInfo.Version;

    public string Method { get; set; } = EngineInfo.Method;
}

// Engine identity, embedded in every response as provenance.
public static class EngineInfo
{
    public const string Version = "0.2.0";

    public const string Method = "deterministic-rule-scoring";
}

namespace CloudSealed.ML.Engine.Models;

// Espelha 1:1 framework4d-predictive-ml-client.ts (cloudsealed-os).
// Nomes e tipos de campo não mudam sem versionar o endpoint.

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
}

public class RiskScores
{
    public int SinglePointOfFailure { get; set; }

    public int ExcessiveCoupling { get; set; }

    public int ScalabilityGap { get; set; }
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

    public List<Finding> Findings { get; set; } = new();

    public List<Recommendation> Recommendations { get; set; } = new();
}

public class PredictArchitectureResponse
{
    public List<ArchitecturePrediction> Predictions { get; set; } = new();

    public string ArchitectureSummary { get; set; } = string.Empty;

    public int OverallArchitectureScore { get; set; }
}

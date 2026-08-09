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

    // Opcional/aditivo: se definido, o resultado é enviado para esta URL
    // (Slack incoming webhook ou listener genérico) quando algum finding
    // atinge HIGH/CRITICAL. Ver WebhookNotifier.
    public string? WebhookUrl { get; set; }
}

public class RiskScores
{
    public int SinglePointOfFailure { get; set; }

    public int ExcessiveCoupling { get; set; }

    public int ScalabilityGap { get; set; }
}

// Uma regra que disparou e sua contribuição em pontos para um score de risco.
// É o que torna o scoring auditável: cada ponto rastreável até um campo do
// request e o motivo pelo qual pesa. É o diferencial explícito frente a um
// modelo caixa-preta.
public class RuleContribution
{
    public string Rule { get; set; } = string.Empty; // chave estável, ex.: "criticality=CRITICAL"

    public int Points { get; set; } // pontos somados ao score da dimensão

    public string Rationale { get; set; } = string.Empty; // por que essa regra pesa
}

// Decomposição, por dimensão de risco, das regras que produziram cada score.
// O score final é min(soma dos pontos, 100) — a soma pode exceder o teto.
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

    // Opcional/aditivo ao contrato: decomposição rastreável de cada riskScore.
    // Clients antigos ignoram; clients novos podem auditar o cálculo.
    public ScoreBreakdown ScoreBreakdown { get; set; } = new();

    public List<Finding> Findings { get; set; } = new();

    public List<Recommendation> Recommendations { get; set; } = new();
}

public class PredictArchitectureResponse
{
    public List<ArchitecturePrediction> Predictions { get; set; } = new();

    public string ArchitectureSummary { get; set; } = string.Empty;

    public int OverallArchitectureScore { get; set; }

    // Proveniência: qual motor/método gerou este resultado. Persistido pelo
    // consumidor junto dos findings para rastrear a origem em auditorias.
    public string EngineVersion { get; set; } = EngineInfo.Version;

    public string Method { get; set; } = EngineInfo.Method;
}

// Identidade do motor, embutida em toda resposta como proveniência.
public static class EngineInfo
{
    public const string Version = "0.2.0";

    public const string Method = "deterministic-rule-scoring";
}

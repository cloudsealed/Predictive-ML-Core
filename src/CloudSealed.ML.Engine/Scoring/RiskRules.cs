namespace CloudSealed.ML.Engine.Scoring;

// Weights and thresholds for deterministic scoring. No trained model: the request
// only carries declared inventory (name/type/criticality/exposure), not real
// telemetry or a dependency graph, so each weight here is an explicit architecture
// rule, not a learned coefficient. See README for the rationale.
public static class RiskRules
{
    // ---- Single Point of Failure ----
    // Base score by declared criticality impact. The schema does not expose a
    // redundancy field, so a single instance is assumed (worst case) — this
    // assumption is stated in the finding text, not hidden in the number.
    public const int SpofBaseLow = 0;
    public const int SpofBaseMedium = 15;
    public const int SpofBaseHigh = 35;
    public const int SpofBaseCritical = 55;

    public const int SpofModifierDatabase = 15;    // state is more expensive to replicate than stateless
    public const int SpofModifierThirdParty = 20;   // outside of control, no declared fallback
    public const int SpofModifierAppOrApi = 5;

    public const int SpofFindingThreshold = 50;      // above this, generates a finding

    // ---- Excessive Coupling ----
    // Without a dependency graph in the request, the coupling proxy is exposure +
    // presence of authentication + third-party dependency (per system and aggregate).
    public const int CouplingPublicNoAuth = 40;
    public const int CouplingPublicWithAuth = 15;
    public const int CouplingInternalBase = 5;
    public const int CouplingThirdPartyType = 25;

    public const int CouplingFanOutFreeCount = 2;    // up to 2 THIRD_PARTY_SERVICE in the request incurs no penalty
    public const int CouplingFanOutStep = 5;         // +5 per dependency beyond the 2nd
    public const int CouplingFanOutCap = 20;         // cap on the aggregate fan-out bonus

    public const int CouplingFindingThreshold = 50;

    // ---- Scalability Gap ----
    // Preference for real metrics (historicalMetrics) over declarative signal.
    public const double P99LatencyThresholdMs = 1000;
    public const int ScalabilityP99Weight = 30;

    public const double TailRatioThreshold = 3.0;    // p99/avg above this = heavy tail under load
    public const int ScalabilityTailRatioWeight = 20;

    // Conditional fallback when there are no historicalMetrics — labelled as
    // "unknown risk"/assumption in the finding, not treated as a measurement.
    public const int ScalabilityDataSensitivityDbWeight = 20;
    public const int ScalabilityCriticalNoMetricsWeight = 15;

    // 30 and not 40: the DATABASE+dataSensitivity pair (20) + CRITICAL with no metrics (15)
    // sums to 35 and must cross the threshold on its own — neither signal in isolation
    // should generate a finding, only the combination.
    public const int ScalabilityFindingThreshold = 30;

    public const int MaxRiskScore = 100;

    public static int SpofBaseByCriticality(string criticality) => criticality switch
    {
        "CRITICAL" => SpofBaseCritical,
        "HIGH" => SpofBaseHigh,
        "MEDIUM" => SpofBaseMedium,
        _ => SpofBaseLow,
    };

    public static int SpofModifierByType(string type) => type switch
    {
        "DATABASE" => SpofModifierDatabase,
        "THIRD_PARTY_SERVICE" => SpofModifierThirdParty,
        "APPLICATION" or "API" => SpofModifierAppOrApi,
        _ => 0,
    };

    // Criticality weight used to factor the overallArchitectureScore: a
    // high-risk CRITICAL must not be diluted among several LOWs in the overall calculation.
    public static int OverallWeightByCriticality(string criticality) => criticality switch
    {
        "CRITICAL" => 4,
        "HIGH" => 3,
        "MEDIUM" => 2,
        _ => 1,
    };
}

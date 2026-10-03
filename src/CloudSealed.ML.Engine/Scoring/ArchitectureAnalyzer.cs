using CloudSealed.ML.Engine.Models;

namespace CloudSealed.ML.Engine.Scoring;

// Orchestrates deterministic scoring: RiskRules -> riskScores per system
// -> findings/recommendations per triggered rule -> overall score weighted by criticality.
// No step depends on data not present in PredictArchitectureRequest.
public class ArchitectureAnalyzer
{
    public PredictArchitectureResponse Analyze(PredictArchitectureRequest request)
    {
        var thirdPartyCount = request.Systems.Count(s => s.Type == "THIRD_PARTY_SERVICE");
        var predictions = request.Systems
            .Select(system => AnalyzeSystem(system, thirdPartyCount, request.HistoricalMetrics))
            .ToList();

        var overallScore = ComputeOverallScore(request.Systems, predictions);
        var summary = BuildSummary(request, predictions, overallScore);

        return new PredictArchitectureResponse
        {
            Predictions = predictions,
            ArchitectureSummary = summary,
            OverallArchitectureScore = overallScore,
        };
    }

    private static ArchitecturePrediction AnalyzeSystem(
        SystemInput system,
        int thirdPartyCount,
        HistoricalMetrics? historicalMetrics)
    {
        var (spof, spofBreakdown) = ScoreSpof(system);
        var (coupling, couplingBreakdown) = ScoreCoupling(system, thirdPartyCount);
        var (scalability, scalabilityBreakdown) = ScoreScalabilityGap(system, historicalMetrics);

        var risks = new RiskScores
        {
            SinglePointOfFailure = spof,
            ExcessiveCoupling = coupling,
            ScalabilityGap = scalability,
        };

        var breakdown = new ScoreBreakdown
        {
            SinglePointOfFailure = spofBreakdown,
            ExcessiveCoupling = couplingBreakdown,
            ScalabilityGap = scalabilityBreakdown,
        };

        var findings = new List<Finding>();
        var recommendations = new List<Recommendation>();

        if (risks.SinglePointOfFailure >= RiskRules.SpofFindingThreshold)
        {
            findings.Add(new Finding
            {
                Title = $"Single point of failure: {system.Name}",
                Severity = SeverityFor(risks.SinglePointOfFailure),
                Description = $"System '{system.Name}' (criticality {system.Criticality}, type {system.Type}) " +
                    "declares no redundancy. Assumption: the input schema exposes no redundancy field, " +
                    "so a single instance is assumed (worst case).",
                Remediation = "Declare and/or implement automatic failover and data replication to " +
                    "eliminate the single-instance dependency.",
            });
            recommendations.Add(new Recommendation
            {
                Title = "Implement redundancy",
                Description = $"Add failover/replica for {system.Name}.",
                Effort = risks.SinglePointOfFailure >= 70 ? "HIGH" : "MEDIUM",
            });
        }

        if (risks.ExcessiveCoupling >= RiskRules.CouplingFindingThreshold)
        {
            var exposedWithoutAuth = system.PublicFacing && string.IsNullOrWhiteSpace(system.AuthMethod);
            findings.Add(new Finding
            {
                Title = $"Excessive coupling: {system.Name}",
                Severity = SeverityFor(risks.ExcessiveCoupling),
                Description = exposedWithoutAuth
                    ? $"System '{system.Name}' is publicly exposed with no authMethod declared."
                    : $"System '{system.Name}' has high coupling due to public exposure and/or third-party " +
                      "dependency declared in the inventory.",
                Remediation = exposedWithoutAuth
                    ? "Implement authentication (OAuth2, API key, or mTLS) before maintaining public exposure."
                    : "Review declared external dependencies and isolate coupling with versioned contracts " +
                      "and circuit breakers.",
            });
            recommendations.Add(new Recommendation
            {
                Title = "Reduce coupling",
                Description = $"Isolate external dependencies and strengthen authentication on {system.Name}.",
                Effort = "MEDIUM",
            });
        }

        if (risks.ScalabilityGap >= RiskRules.ScalabilityFindingThreshold)
        {
            var hasMetrics = historicalMetrics is { P99LatencyMs: not null } or { AvgLatencyMs: not null };
            var basis = hasMetrics
                ? "based on historicalMetrics declared in the request"
                : "no historicalMetrics — conditional risk, labeled as unknown risk";
            findings.Add(new Finding
            {
                Title = $"Scalability gap: {system.Name}",
                Severity = SeverityFor(risks.ScalabilityGap),
                Description = $"Insufficient scalability signal for '{system.Name}' ({basis}).",
                Remediation = "Run a load test and configure auto-scaling/partitioning before expanding traffic.",
            });
            recommendations.Add(new Recommendation
            {
                Title = "Improve scalability",
                Description = $"Validate {system.Name} capacity under load and configure automatic scaling.",
                Effort = "MEDIUM",
            });
        }

        return new ArchitecturePrediction
        {
            SystemName = system.Name,
            RiskScores = risks,
            ScoreBreakdown = breakdown,
            Findings = findings,
            Recommendations = recommendations,
        };
    }

    // Each Score* returns the score (0-100, capped) and the list of rules that produced it.
    // score == min(sum of points, MaxRiskScore).
    private static int Total(List<RuleContribution> contributions) =>
        Math.Min(contributions.Sum(c => c.Points), RiskRules.MaxRiskScore);

    private static (int Score, List<RuleContribution> Breakdown) ScoreSpof(SystemInput system)
    {
        var b = new List<RuleContribution>();

        var basePts = RiskRules.SpofBaseByCriticality(system.Criticality);
        if (basePts > 0)
        {
            b.Add(new RuleContribution
            {
                Rule = $"criticality={system.Criticality}",
                Points = basePts,
                Rationale = "Failure impact grows with the declared criticality of the system.",
            });
        }

        var typePts = RiskRules.SpofModifierByType(system.Type);
        if (typePts > 0)
        {
            b.Add(new RuleContribution
            {
                Rule = $"type={system.Type}",
                Points = typePts,
                Rationale = system.Type switch
                {
                    "DATABASE" => "State is more expensive to replicate than a stateless service.",
                    "THIRD_PARTY_SERVICE" => "Third-party dependency is outside your control and has no declared fallback.",
                    _ => "Application/API service adds moderate single-instance risk.",
                },
            });
        }

        return (Total(b), b);
    }

    private static (int Score, List<RuleContribution> Breakdown) ScoreCoupling(SystemInput system, int thirdPartyCount)
    {
        var b = new List<RuleContribution>();

        if (system.PublicFacing && string.IsNullOrWhiteSpace(system.AuthMethod))
        {
            b.Add(new RuleContribution
            {
                Rule = "publicFacing=true,authMethod=null",
                Points = RiskRules.CouplingPublicNoAuth,
                Rationale = "Public exposure with no declared authentication is a direct attack surface.",
            });
        }
        else if (system.PublicFacing)
        {
            b.Add(new RuleContribution
            {
                Rule = "publicFacing=true,authMethod=set",
                Points = RiskRules.CouplingPublicWithAuth,
                Rationale = "Public exposure with authentication still widens the integration surface.",
            });
        }
        else
        {
            b.Add(new RuleContribution
            {
                Rule = "publicFacing=false",
                Points = RiskRules.CouplingInternalBase,
                Rationale = "Minimum coupling baseline for any internal system.",
            });
        }

        if (system.Type == "THIRD_PARTY_SERVICE")
        {
            b.Add(new RuleContribution
            {
                Rule = "type=THIRD_PARTY_SERVICE",
                Points = RiskRules.CouplingThirdPartyType,
                Rationale = "Third-party dependency couples the system to an external contract.",
            });
        }

        var fanOutBeyondFree = Math.Max(0, thirdPartyCount - RiskRules.CouplingFanOutFreeCount);
        var fanOutBonus = Math.Min(fanOutBeyondFree * RiskRules.CouplingFanOutStep, RiskRules.CouplingFanOutCap);
        if (fanOutBonus > 0)
        {
            b.Add(new RuleContribution
            {
                Rule = $"orgThirdPartyFanOut={thirdPartyCount}",
                Points = fanOutBonus,
                Rationale = $"Inventory declares {thirdPartyCount} third-party dependencies; " +
                    "org-level coupling above the free threshold.",
            });
        }

        return (Total(b), b);
    }

    private static (int Score, List<RuleContribution> Breakdown) ScoreScalabilityGap(
        SystemInput system, HistoricalMetrics? historicalMetrics)
    {
        var b = new List<RuleContribution>();

        if (historicalMetrics is { P99LatencyMs: not null } or { AvgLatencyMs: not null })
        {
            if (historicalMetrics!.P99LatencyMs > RiskRules.P99LatencyThresholdMs)
            {
                b.Add(new RuleContribution
                {
                    Rule = $"p99LatencyMs>{RiskRules.P99LatencyThresholdMs}",
                    Points = RiskRules.ScalabilityP99Weight,
                    Rationale = "Tail latency (p99) above threshold indicates saturation under load.",
                });
            }

            if (historicalMetrics.P99LatencyMs is > 0 && historicalMetrics.AvgLatencyMs is > 0
                && historicalMetrics.P99LatencyMs.Value / historicalMetrics.AvgLatencyMs.Value > RiskRules.TailRatioThreshold)
            {
                b.Add(new RuleContribution
                {
                    Rule = $"p99/avg>{RiskRules.TailRatioThreshold}",
                    Points = RiskRules.ScalabilityTailRatioWeight,
                    Rationale = "High p99/avg ratio reveals a heavy tail — a bottleneck that appears under peak load.",
                });
            }
        }
        else
        {
            if (system.Type == "DATABASE" && !string.IsNullOrWhiteSpace(system.DataSensitivity))
            {
                b.Add(new RuleContribution
                {
                    Rule = "type=DATABASE,dataSensitivity=set (no metrics)",
                    Points = RiskRules.ScalabilityDataSensitivityDbWeight,
                    Rationale = "Database with sensitive data restricts naive scaling; conditional signal (no metrics).",
                });
            }

            if (system.Criticality == "CRITICAL")
            {
                b.Add(new RuleContribution
                {
                    Rule = "criticality=CRITICAL (no metrics)",
                    Points = RiskRules.ScalabilityCriticalNoMetricsWeight,
                    Rationale = "Critical system with no observed load metric — unknown risk, treated as conditional.",
                });
            }
        }

        return (Total(b), b);
    }

    private static string SeverityFor(int score) => score switch
    {
        >= 70 => "CRITICAL",
        >= 50 => "HIGH",
        >= 30 => "MEDIUM",
        _ => "LOW",
    };

    // Weighted average by criticality: a high-risk CRITICAL system must not be
    // diluted by several LOW-risk systems in the overall score.
    private static int ComputeOverallScore(List<SystemInput> systems, List<ArchitecturePrediction> predictions)
    {
        if (predictions.Count == 0)
        {
            return 100;
        }

        double weightedRiskSum = 0;
        double weightSum = 0;

        foreach (var (system, prediction) in systems.Zip(predictions))
        {
            var weight = RiskRules.OverallWeightByCriticality(system.Criticality);
            var risk = prediction.RiskScores;
            var avgRisk = (risk.SinglePointOfFailure + risk.ExcessiveCoupling + risk.ScalabilityGap) / 3.0;
            weightedRiskSum += avgRisk * weight;
            weightSum += weight;
        }

        var weightedAvgRisk = weightSum > 0 ? weightedRiskSum / weightSum : 0;
        return (int)Math.Round(Math.Clamp(100 - weightedAvgRisk, 0, 100));
    }

    private static string BuildSummary(
        PredictArchitectureRequest request,
        List<ArchitecturePrediction> predictions,
        int overallScore)
    {
        var totalFindings = predictions.Sum(p => p.Findings.Count);
        var totalRecommendations = predictions.Sum(p => p.Recommendations.Count);
        return $"Analysis of {request.Systems.Count} system(s) for {request.CompanyName} complete. " +
            $"Overall score (weighted by criticality): {overallScore}/100. " +
            $"{totalFindings} finding(s), {totalRecommendations} recommendation(s).";
    }
}

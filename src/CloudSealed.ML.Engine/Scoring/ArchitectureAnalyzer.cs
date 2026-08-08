using CloudSealed.ML.Engine.Models;

namespace CloudSealed.ML.Engine.Scoring;

// Orquestra o scoring determinístico: regras de RiskRules -> riskScores por
// sistema -> findings/recommendations por regra disparada -> score geral
// ponderado por criticidade. Nenhum passo depende de dado não presente no
// PredictArchitectureRequest.
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
                Title = $"Ponto único de falha: {system.Name}",
                Severity = SeverityFor(risks.SinglePointOfFailure),
                Description = $"Sistema '{system.Name}' (criticidade {system.Criticality}, tipo {system.Type}) " +
                    "não declara redundância. Premissa: o schema de entrada não expõe campo de redundância, " +
                    "então assume-se instância única (pior caso).",
                Remediation = "Declarar e/ou implementar failover automático e replicação de dados para " +
                    "eliminar a dependência de uma única instância.",
            });
            recommendations.Add(new Recommendation
            {
                Title = "Implementar redundância",
                Description = $"Adicionar failover/réplica para {system.Name}.",
                Effort = risks.SinglePointOfFailure >= 70 ? "HIGH" : "MEDIUM",
            });
        }

        if (risks.ExcessiveCoupling >= RiskRules.CouplingFindingThreshold)
        {
            var exposedWithoutAuth = system.PublicFacing && string.IsNullOrWhiteSpace(system.AuthMethod);
            findings.Add(new Finding
            {
                Title = $"Acoplamento excessivo: {system.Name}",
                Severity = SeverityFor(risks.ExcessiveCoupling),
                Description = exposedWithoutAuth
                    ? $"Sistema '{system.Name}' está exposto publicamente sem authMethod declarado."
                    : $"Sistema '{system.Name}' tem acoplamento elevado por exposição pública e/ou dependência " +
                      "de terceiros declarada no inventário.",
                Remediation = exposedWithoutAuth
                    ? "Implementar autenticação (OAuth2, API key ou mTLS) antes de manter exposição pública."
                    : "Revisar dependências externas declaradas e isolar acoplamento com contratos " +
                      "versionados e circuit breakers.",
            });
            recommendations.Add(new Recommendation
            {
                Title = "Reduzir acoplamento",
                Description = $"Isolar dependências externas e reforçar autenticação em {system.Name}.",
                Effort = "MEDIUM",
            });
        }

        if (risks.ScalabilityGap >= RiskRules.ScalabilityFindingThreshold)
        {
            var hasMetrics = historicalMetrics is { P99LatencyMs: not null } or { AvgLatencyMs: not null };
            var basis = hasMetrics
                ? "com base em historicalMetrics declarado no request"
                : "sem historicalMetrics — risco condicional, rotulado como unknown risk";
            findings.Add(new Finding
            {
                Title = $"Gargalo de escalabilidade: {system.Name}",
                Severity = SeverityFor(risks.ScalabilityGap),
                Description = $"Sinal de escalabilidade insuficiente em '{system.Name}' ({basis}).",
                Remediation = "Executar teste de carga e configurar auto-scaling/particionamento antes de " +
                    "expandir o tráfego.",
            });
            recommendations.Add(new Recommendation
            {
                Title = "Melhorar escalabilidade",
                Description = $"Validar capacidade de {system.Name} sob carga e configurar scaling automático.",
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

    // Cada Score* devolve o score (0-100, com teto) e a lista de regras que o
    // produziram. score == min(soma dos pontos, MaxRiskScore).
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
                Rationale = "O impacto de uma falha cresce com a criticidade declarada do sistema.",
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
                    "DATABASE" => "Estado é mais caro de replicar que um serviço stateless.",
                    "THIRD_PARTY_SERVICE" => "Dependência de terceiro está fora do seu controle e sem fallback declarado.",
                    _ => "Serviço de aplicação/API adiciona risco moderado de instância única.",
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
                Rationale = "Exposição pública sem autenticação declarada é superfície de ataque direta.",
            });
        }
        else if (system.PublicFacing)
        {
            b.Add(new RuleContribution
            {
                Rule = "publicFacing=true,authMethod=set",
                Points = RiskRules.CouplingPublicWithAuth,
                Rationale = "Exposição pública com autenticação ainda amplia a superfície de integração.",
            });
        }
        else
        {
            b.Add(new RuleContribution
            {
                Rule = "publicFacing=false",
                Points = RiskRules.CouplingInternalBase,
                Rationale = "Base mínima de acoplamento para qualquer sistema interno.",
            });
        }

        if (system.Type == "THIRD_PARTY_SERVICE")
        {
            b.Add(new RuleContribution
            {
                Rule = "type=THIRD_PARTY_SERVICE",
                Points = RiskRules.CouplingThirdPartyType,
                Rationale = "Dependência de terceiro acopla o sistema a um contrato externo.",
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
                Rationale = $"O inventário declara {thirdPartyCount} dependências de terceiro; " +
                    "acoplamento em nível de organização acima do limiar livre.",
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
                    Rationale = "Latência de cauda (p99) acima do limiar indica saturação sob carga.",
                });
            }

            if (historicalMetrics.P99LatencyMs is > 0 && historicalMetrics.AvgLatencyMs is > 0
                && historicalMetrics.P99LatencyMs.Value / historicalMetrics.AvgLatencyMs.Value > RiskRules.TailRatioThreshold)
            {
                b.Add(new RuleContribution
                {
                    Rule = $"p99/avg>{RiskRules.TailRatioThreshold}",
                    Points = RiskRules.ScalabilityTailRatioWeight,
                    Rationale = "Razão p99/média alta revela cauda pesada — gargalo que aparece nos picos.",
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
                    Rationale = "Banco com dados sensíveis restringe escalonamento ingênuo; sinal condicional (sem métricas).",
                });
            }

            if (system.Criticality == "CRITICAL")
            {
                b.Add(new RuleContribution
                {
                    Rule = "criticality=CRITICAL (no metrics)",
                    Points = RiskRules.ScalabilityCriticalNoMetricsWeight,
                    Rationale = "Sistema crítico sem métrica de carga observada — risco desconhecido, tratado como condicional.",
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

    // Média ponderada por criticidade: um CRITICAL de risco alto não pode se
    // diluir no meio de vários LOW no score geral.
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
        return $"Análise de {request.Systems.Count} sistema(s) de {request.CompanyName} concluída. " +
            $"Score geral (ponderado por criticidade): {overallScore}/100. " +
            $"{totalFindings} finding(s), {totalRecommendations} recomendação(ões).";
    }
}

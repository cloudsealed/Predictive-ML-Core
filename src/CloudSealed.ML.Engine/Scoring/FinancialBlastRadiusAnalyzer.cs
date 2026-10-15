using System;
using System.Collections.Generic;
using System.Linq;

namespace CloudSealed.ML.Engine.Scoring
{
    public class ServiceFinancialNode
    {
        public string ServiceId { get; set; } = string.Empty;
        public string ServiceName { get; set; } = string.Empty;
        public double MonthlyCostUsd { get; set; }
        public double CriticalityMultiplier { get; set; } = 1.0;
        public List<string> DependentServiceIds { get; set; } = new();
        public double BlastPropagationFactor { get; set; } = 0.5; // taxa de propagação de custo/falha
    }

    public class BlastRadiusEvaluation
    {
        public string SourceServiceId { get; set; } = string.Empty;
        public string SourceServiceName { get; set; } = string.Empty;
        public double DirectFinancialLossUsd { get; set; }
        public double CascadingFinancialImpactUsd { get; set; }
        public double TotalFinancialBlastRadiusUsd { get; set; }
        public List<string> ImpactedServiceIds { get; set; } = new();
        public double BlastRadiusRatio { get; set; } // % do custo total da infraestrutura em risco
        public string RiskTier { get; set; } = "LOW"; // LOW, MEDIUM, HIGH, CATASTROPHIC
    }

    public class FinancialBlastRadiusReport
    {
        public double TotalMonthlyInfrastructureCostUsd { get; set; }
        public List<BlastRadiusEvaluation> ServiceEvaluations { get; set; } = new();
        public string HighestRiskNodeId { get; set; } = string.Empty;
        public double MaxSingleFailureCostUsd { get; set; }
        public string ExecutiveSummary { get; set; } = string.Empty;
    }

    /// <summary>
    /// Financial Blast Radius Analyzer:
    /// Modela o gráfico topológico de microsserviços e calcula o efeito dominó financeiro
    /// caso um componente crítico (banco, gateway, mensageria) entre em colapso ou sofra um loop de retry infinito.
    /// Diferencial FinOps que AWS/GCP não calculam nativamente.
    /// </summary>
    public class FinancialBlastRadiusAnalyzer
    {
        public FinancialBlastRadiusReport Analyze(List<ServiceFinancialNode> nodes)
        {
            if (nodes == null || nodes.Count == 0)
            {
                return new FinancialBlastRadiusReport
                {
                    ExecutiveSummary = "Nenhum nó de serviço fornecido para análise de blast radius financeiro."
                };
            }

            var nodeMap = nodes.ToDictionary(n => n.ServiceId, n => n);
            double totalCost = nodes.Sum(n => n.MonthlyCostUsd);
            var evaluations = new List<BlastRadiusEvaluation>();

            foreach (var node in nodes)
            {
                var visited = new HashSet<string>();
                var queue = new Queue<(string serviceId, double factor)>();
                
                visited.Add(node.ServiceId);
                double cascadingLoss = 0.0;

                // Propaga para dependentes
                foreach (var depId in node.DependentServiceIds)
                {
                    if (nodeMap.ContainsKey(depId))
                    {
                        queue.Enqueue((depId, node.BlastPropagationFactor));
                    }
                }

                while (queue.Count > 0)
                {
                    var (currId, factor) = queue.Dequeue();
                    if (!visited.Contains(currId) && nodeMap.TryGetValue(currId, out var depNode))
                    {
                        visited.Add(currId);
                        // Perda cascata = custo do dependente ponderado pelo fator de propagação e criticalidade
                        double lossFromDep = depNode.MonthlyCostUsd * factor * depNode.CriticalityMultiplier;
                        cascadingLoss += lossFromDep;

                        foreach (var nextDep in depNode.DependentServiceIds)
                        {
                            if (!visited.Contains(nextDep) && nodeMap.ContainsKey(nextDep))
                            {
                                queue.Enqueue((nextDep, factor * depNode.BlastPropagationFactor));
                            }
                        }
                    }
                }

                double directLoss = node.MonthlyCostUsd * node.CriticalityMultiplier;
                double totalBlast = Math.Round(directLoss + cascadingLoss, 2);
                double blastRatio = totalCost > 0 ? Math.Round((totalBlast / totalCost) * 100, 2) : 0;

                string riskTier = "LOW";
                if (blastRatio > 60 || totalBlast > 50000) riskTier = "CATASTROPHIC";
                else if (blastRatio > 35 || totalBlast > 15000) riskTier = "HIGH";
                else if (blastRatio > 15 || totalBlast > 5000) riskTier = "MEDIUM";

                evaluations.Add(new BlastRadiusEvaluation
                {
                    SourceServiceId = node.ServiceId,
                    SourceServiceName = node.ServiceName,
                    DirectFinancialLossUsd = Math.Round(directLoss, 2),
                    CascadingFinancialImpactUsd = Math.Round(cascadingLoss, 2),
                    TotalFinancialBlastRadiusUsd = totalBlast,
                    ImpactedServiceIds = visited.Where(id => id != node.ServiceId).ToList(),
                    BlastRadiusRatio = blastRatio,
                    RiskTier = riskTier
                });
            }

            var highestRisk = evaluations.OrderByDescending(e => e.TotalFinancialBlastRadiusUsd).FirstOrDefault();

            return new FinancialBlastRadiusReport
            {
                TotalMonthlyInfrastructureCostUsd = Math.Round(totalCost, 2),
                ServiceEvaluations = evaluations.OrderByDescending(e => e.TotalFinancialBlastRadiusUsd).ToList(),
                HighestRiskNodeId = highestRisk?.SourceServiceId ?? string.Empty,
                MaxSingleFailureCostUsd = highestRisk?.TotalFinancialBlastRadiusUsd ?? 0.0,
                ExecutiveSummary = highestRisk != null
                    ? $"O nó com maior Raio de Destruição Financeira é '{highestRisk.SourceServiceName}' (${highestRisk.TotalFinancialBlastRadiusUsd:N2} USD em risco cascata, {highestRisk.BlastRadiusRatio}% da infraestrutura)."
                    : "Análise de blast radius concluída sem riscos críticos detectados."
            };
        }
    }
}

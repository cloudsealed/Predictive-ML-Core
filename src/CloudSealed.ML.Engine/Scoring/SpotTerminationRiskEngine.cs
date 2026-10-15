using System;
using System.Collections.Generic;
using System.Linq;

namespace CloudSealed.ML.Engine.Scoring
{
    public class SpotPoolCandidate
    {
        public string InstanceFamily { get; set; } = string.Empty; // ex: c6g.2xlarge, m5.xlarge, n2-standard-4
        public string Region { get; set; } = string.Empty;
        public string AvailabilityZone { get; set; } = string.Empty;
        public double SpotPriceUsdPerHour { get; set; }
        public double OnDemandPriceUsdPerHour { get; set; }
        public double HistoricalInterruptionFrequencyPercent { get; set; } // % de quedas nos últimos 30 dias (ex: 5% a 20%)
        public double DemandVolatilityScore { get; set; } // 0 - 100 (pressão de mercado no pool)
    }

    public class WorkloadRequirement
    {
        public string WorkloadName { get; set; } = string.Empty;
        public int TotalCoresNeeded { get; set; }
        public int TotalMemoryGbNeeded { get; set; }
        public bool IsStateful { get; set; } // Se true, tolerância zero a interrupção abrupta
        public int MaxAllowableInterruptionRatePercent { get; set; } = 10;
    }

    public class OptimalFleetAllocation
    {
        public string InstanceFamily { get; set; } = string.Empty;
        public string AvailabilityZone { get; set; } = string.Empty;
        public int TargetInstanceCount { get; set; }
        public double AllocatedSpotPercent { get; set; }
        public double AllocatedOnDemandPercent { get; set; }
        public double ProjectedHourlyCostUsd { get; set; }
        public double ExpectedInterruptionProbability { get; set; }
    }

    public class SpotRiskReport
    {
        public double TotalOnDemandCostMonthlyUsd { get; set; }
        public double TotalOptimizedSpotCostMonthlyUsd { get; set; }
        public double ProjectedMonthlySavingsUsd { get; set; }
        public double FleetSavingsPercent { get; set; }
        public double WeightedFleetInterruptionRiskPercent { get; set; }
        public List<OptimalFleetAllocation> Allocations { get; set; } = new();
        public string StrategyRecommendation { get; set; } = string.Empty;
    }

    /// <summary>
    /// Multi-Cloud Spot Volatility & Termination Risk Engine:
    /// Calcula a probabilidade de despejo (eviction) de instâncias Spot/Preemptible
    /// e gera a matriz de alocação de frota diversificada para rodar produção com até 80% de desconto.
    /// </summary>
    public class SpotTerminationRiskEngine
    {
        public SpotRiskReport OptimizeFleet(
            WorkloadRequirement requirement,
            List<SpotPoolCandidate> candidates)
        {
            if (candidates == null || candidates.Count == 0)
            {
                return new SpotRiskReport
                {
                    StrategyRecommendation = "Nenhum pool de instâncias Spot fornecido para otimização."
                };
            }

            // Ordena os pools por menor probabilidade de interrupção e maior economia
            var scoredPools = candidates.Select(p =>
            {
                double savingsRatio = p.OnDemandPriceUsdPerHour > 0
                    ? (p.OnDemandPriceUsdPerHour - p.SpotPriceUsdPerHour) / p.OnDemandPriceUsdPerHour
                    : 0.0;

                // Risco = Frequência histórica ponderada pela volatilidade atual
                double riskScore = p.HistoricalInterruptionFrequencyPercent * (1.0 + (p.DemandVolatilityScore / 100.0));

                return new
                {
                    Pool = p,
                    SavingsRatio = savingsRatio,
                    CompositeRisk = Math.Round(riskScore, 1)
                };
            })
            .OrderBy(p => p.CompositeRisk)
            .ThenByDescending(p => p.SavingsRatio)
            .ToList();

            double totalOnDemandMonthly = 0.0;
            double totalOptimizedMonthly = 0.0;
            var allocations = new List<OptimalFleetAllocation>();

            // Se a carga for Stateful, reservamos 40% On-Demand e 60% Spot diversificado.
            // Se for Stateless, 85% Spot e 15% On-Demand.
            double spotShare = requirement.IsStateful ? 0.60 : 0.85;
            double onDemandShare = 1.0 - spotShare;

            int selectedPoolsCount = Math.Min(3, scoredPools.Count);
            var topPools = scoredPools.Take(selectedPoolsCount).ToList();

            foreach (var item in topPools)
            {
                var pool = item.Pool;
                double hourlyOnDemand = pool.OnDemandPriceUsdPerHour;
                double hourlyBlended = (pool.SpotPriceUsdPerHour * spotShare) + (pool.OnDemandPriceUsdPerHour * onDemandShare);

                totalOnDemandMonthly += (hourlyOnDemand * 730);
                totalOptimizedMonthly += (hourlyBlended * 730);

                allocations.Add(new OptimalFleetAllocation
                {
                    InstanceFamily = pool.InstanceFamily,
                    AvailabilityZone = pool.AvailabilityZone,
                    TargetInstanceCount = 2,
                    AllocatedSpotPercent = Math.Round(spotShare * 100, 1),
                    AllocatedOnDemandPercent = Math.Round(onDemandShare * 100, 1),
                    ProjectedHourlyCostUsd = Math.Round(hourlyBlended, 3),
                    ExpectedInterruptionProbability = item.CompositeRisk
                });
            }

            double savings = Math.Max(0, totalOnDemandMonthly - totalOptimizedMonthly);
            double savingsPercent = totalOnDemandMonthly > 0 ? (savings / totalOnDemandMonthly) * 100.0 : 0.0;
            double weightedRisk = allocations.Count > 0 ? allocations.Average(a => a.ExpectedInterruptionProbability) : 0.0;

            string strategy = $"Estratégia de Frota Spot Diversificada: economia de {savingsPercent:N1}% com risco de despejo ponderado de apenas {weightedRisk:N1}%. Recomendado provisionamento via Karpenter / Spot Ocean.";

            return new SpotRiskReport
            {
                TotalOnDemandCostMonthlyUsd = Math.Round(totalOnDemandMonthly, 2),
                TotalOptimizedSpotCostMonthlyUsd = Math.Round(totalOptimizedMonthly, 2),
                ProjectedMonthlySavingsUsd = Math.Round(savings, 2),
                FleetSavingsPercent = Math.Round(savingsPercent, 1),
                WeightedFleetInterruptionRiskPercent = Math.Round(weightedRisk, 1),
                Allocations = allocations,
                StrategyRecommendation = strategy
            };
        }
    }
}

using System.Collections.Generic;
using CloudSealed.ML.Engine.Policies;
using CloudSealed.ML.Engine.Scoring;
using Xunit;

namespace CloudSealed.ML.Tests
{
    public class FinOpsAdvancedMetricsTests
    {
        [Fact]
        public void FinancialBlastRadiusAnalyzer_CalculatesCascadingFailureAccurately()
        {
            var analyzer = new FinancialBlastRadiusAnalyzer();
            var nodes = new List<ServiceFinancialNode>
            {
                new ServiceFinancialNode
                {
                    ServiceId = "db-primary",
                    ServiceName = "PostgreSQL Primary Cluster",
                    MonthlyCostUsd = 1200.0,
                    CriticalityMultiplier = 1.5,
                    BlastPropagationFactor = 0.8,
                    DependentServiceIds = new List<string> { "api-gateway", "billing-worker" }
                },
                new ServiceFinancialNode
                {
                    ServiceId = "api-gateway",
                    ServiceName = "Kong API Gateway",
                    MonthlyCostUsd = 600.0,
                    CriticalityMultiplier = 1.2,
                    BlastPropagationFactor = 0.5,
                    DependentServiceIds = new List<string> { "frontend" }
                },
                new ServiceFinancialNode
                {
                    ServiceId = "billing-worker",
                    ServiceName = "Celery Billing Worker",
                    MonthlyCostUsd = 400.0,
                    CriticalityMultiplier = 1.0,
                    BlastPropagationFactor = 0.2,
                    DependentServiceIds = new List<string>()
                },
                new ServiceFinancialNode
                {
                    ServiceId = "frontend",
                    ServiceName = "Next.js Frontend",
                    MonthlyCostUsd = 300.0,
                    CriticalityMultiplier = 1.0,
                    BlastPropagationFactor = 0.0,
                    DependentServiceIds = new List<string>()
                }
            };

            var report = analyzer.Analyze(nodes);

            Assert.NotNull(report);
            Assert.Equal(2500.0, report.TotalMonthlyInfrastructureCostUsd);
            Assert.Equal("db-primary", report.HighestRiskNodeId);
            Assert.True(report.MaxSingleFailureCostUsd > 1800.0);
            
            var dbEval = report.ServiceEvaluations.Find(e => e.SourceServiceId == "db-primary");
            Assert.NotNull(dbEval);
            Assert.True(dbEval.CascadingFinancialImpactUsd > 0);
            Assert.Contains("api-gateway", dbEval.ImpactedServiceIds);
            Assert.Contains("billing-worker", dbEval.ImpactedServiceIds);
            Assert.Contains("frontend", dbEval.ImpactedServiceIds);
        }

        [Fact]
        public void FinancialCircuitBreakerPolicy_TripsHardStopOnBudgetBreach()
        {
            var policy = new FinancialCircuitBreakerPolicy();
            var input = new FinancialCircuitBreakerPolicy.CircuitBreakerInput
            {
                CurrentHourSpendUsd = 200.0,
                BaselineHourSpendUsd = 50.0,
                CumulativeDaySpendUsd = 1050.0,
                DailyBudgetLimitUsd = 1000.0,
                VelocityAnomalyScore = 95,
                UnsettledCommittedSpendUsd = 50.0
            };

            var result = policy.Evaluate(input);

            Assert.True(result.IsTripped);
            Assert.Equal("OPEN", result.State);
            Assert.Equal("HARD_STOP", result.ActionRequired);
            Assert.True(result.BudgetBurnPercent >= 100.0);
        }

        [Fact]
        public void FinancialCircuitBreakerPolicy_ThrottlesAutonomousAgentsOnSpike()
        {
            var policy = new FinancialCircuitBreakerPolicy();
            var input = new FinancialCircuitBreakerPolicy.CircuitBreakerInput
            {
                CurrentHourSpendUsd = 110.0,
                BaselineHourSpendUsd = 50.0,
                CumulativeDaySpendUsd = 450.0,
                DailyBudgetLimitUsd = 1000.0,
                VelocityAnomalyScore = 65,
                UnsettledCommittedSpendUsd = 0.0
            };

            var result = policy.Evaluate(input);

            Assert.False(result.IsTripped);
            Assert.Equal("HALF_OPEN", result.State);
            Assert.Equal("THROTTLE_AUTONOMOUS_AGENTS", result.ActionRequired);
        }

        [Fact]
        public void GenAITokenForensicsAnalyzer_DetectsWastedTokensAndLowCacheHitRate()
        {
            var analyzer = new GenAITokenForensicsAnalyzer();
            var models = new List<ModelUsageTelemetry>
            {
                new ModelUsageTelemetry
                {
                    ModelName = "gpt-4o",
                    PromptTokens = 10_000_000,
                    CompletionTokens = 2_000_000,
                    CachedPromptTokens = 1_000_000, // apenas 10% de cache hit rate
                    TotalRequests = 50_000,
                    InputCostPerMillion = 2.50,
                    OutputCostPerMillion = 10.00,
                    CachedInputCostPerMillion = 1.25,
                }
            };

            var report = analyzer.Analyze(models);

            Assert.NotNull(report);
            Assert.True(report.TotalMonthlyGenAICostUsd > 40.0);
            Assert.True(report.TotalWastedTokenCostUsd > 0.0);
            Assert.Equal(10.0, report.OverallPromptCacheHitRate);
            Assert.NotEmpty(report.ForensicRecommendations);
        }

        [Fact]
        public void CommitmentRiskAnalyzer_FlagsHighRiskOnLongTermVolatileArchitectures()
        {
            var analyzer = new CommitmentRiskAnalyzer();
            var input = new CommitmentPlanInput
            {
                CommitmentType = "RESERVED_INSTANCE",
                TermYears = 3,
                UpfrontPaymentUsd = 5000.0,
                HourlyCommitmentUsd = 1.20,
                OnDemandHourlyRateEquivalentUsd = 2.00,
                HistoricalHourlySpendUsd = 1.25, // Quase 100% comprometido
                ArchitectureVolatilityRisk = 0.80 // Alta probabilidade de refatoração para Serverless
            };

            var report = analyzer.Analyze(input);

            Assert.NotNull(report);
            Assert.True(report.ProbabilityOfUnderutilization > 50.0);
            Assert.Contains("Risco alto de desperdício", report.Recommendation);
            Assert.True(report.PotentialStrandedCostUsd > 1000.0);
        }

        [Fact]
        public void SpotTerminationRiskEngine_GeneratesDiversifiedFleetWithHighSavings()
        {
            var engine = new SpotTerminationRiskEngine();
            var req = new WorkloadRequirement
            {
                WorkloadName = "Data-Analytics-Worker",
                TotalCoresNeeded = 32,
                TotalMemoryGbNeeded = 128,
                IsStateful = false,
                MaxAllowableInterruptionRatePercent = 15
            };

            var candidates = new List<SpotPoolCandidate>
            {
                new SpotPoolCandidate
                {
                    InstanceFamily = "c6g.4xlarge",
                    Region = "us-east-1",
                    AvailabilityZone = "us-east-1a",
                    SpotPriceUsdPerHour = 0.20,
                    OnDemandPriceUsdPerHour = 0.68,
                    HistoricalInterruptionFrequencyPercent = 4.5,
                    DemandVolatilityScore = 20
                },
                new SpotPoolCandidate
                {
                    InstanceFamily = "c6i.4xlarge",
                    Region = "us-east-1",
                    AvailabilityZone = "us-east-1b",
                    SpotPriceUsdPerHour = 0.25,
                    OnDemandPriceUsdPerHour = 0.72,
                    HistoricalInterruptionFrequencyPercent = 6.0,
                    DemandVolatilityScore = 30
                }
            };

            var report = engine.OptimizeFleet(req, candidates);

            Assert.NotNull(report);
            Assert.True(report.FleetSavingsPercent > 50.0);
            Assert.True(report.ProjectedMonthlySavingsUsd > 0.0);
            Assert.NotEmpty(report.Allocations);
            Assert.Contains("Diversificada", report.StrategyRecommendation);
        }
    }
}

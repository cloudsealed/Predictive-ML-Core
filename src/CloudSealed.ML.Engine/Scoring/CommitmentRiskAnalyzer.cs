using System;
using System.Collections.Generic;

namespace CloudSealed.ML.Engine.Scoring
{
    public class CommitmentPlanInput
    {
        public string CommitmentType { get; set; } = "SAVINGS_PLAN"; // SAVINGS_PLAN ou RESERVED_INSTANCE
        public int TermYears { get; set; } = 1; // 1 ou 3 anos
        public double UpfrontPaymentUsd { get; set; } = 0.0;
        public double HourlyCommitmentUsd { get; set; } // Gasto comprometido por hora
        public double OnDemandHourlyRateEquivalentUsd { get; set; } // Custo equivalente sem desconto
        public double HistoricalHourlySpendUsd { get; set; } // Uso real médio da empresa
        public double ArchitectureVolatilityRisk { get; set; } = 0.20; // Probabilidade de migração/refatoração
    }

    public class CommitmentRiskReport
    {
        public double TotalContractObligationUsd { get; set; }
        public double ProjectedGrossSavingsUsd { get; set; }
        public double BreakEvenMonths { get; set; }
        public double ProbabilityOfUnderutilization { get; set; } // % de chance de desperdício
        public double PotentialStrandedCostUsd { get; set; } // Custo preso em capacidade ociosa
        public string Recommendation { get; set; } = string.Empty;
        public string SafeCommitmentLevelRecommendation { get; set; } = string.Empty;
    }

    /// <summary>
    /// Commitment Arbitrage & Break-Even Analyzer:
    /// Analisa se a assinatura de planos de economia de longo prazo (Savings Plans / RIs)
    /// faz sentido matemático ou se é uma armadilha financeira da nuvem devido a refatorações futuras.
    /// </summary>
    public class CommitmentRiskAnalyzer
    {
        public CommitmentRiskReport Analyze(CommitmentPlanInput input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));

            int totalHours = input.TermYears * 365 * 24;
            double totalObligation = input.UpfrontPaymentUsd + (input.HourlyCommitmentUsd * totalHours);
            double onDemandCost = input.OnDemandHourlyRateEquivalentUsd * totalHours;
            double grossSavings = Math.Max(0, onDemandCost - totalObligation);

            // Break-even em meses
            double monthlySavings = (onDemandCost - totalObligation) / (input.TermYears * 12.0);
            double breakEvenMonths = monthlySavings > 0
                ? Math.Round((input.UpfrontPaymentUsd > 0 ? input.UpfrontPaymentUsd / monthlySavings : 3.0), 1)
                : (input.TermYears * 12.0);

            // Risco de subutilização = se o compromisso horário for maior que 85% do uso histórico,
            // ou se a volatilidade arquitetural for alta (migração para container/serverless)
            double utilizationRatio = input.HistoricalHourlySpendUsd > 0
                ? input.HourlyCommitmentUsd / input.HistoricalHourlySpendUsd
                : 1.0;

            double probUnderutilization = 0.10;
            if (utilizationRatio > 0.85) probUnderutilization += 0.35;
            if (input.TermYears == 3) probUnderutilization += 0.30;
            probUnderutilization += (input.ArchitectureVolatilityRisk * 0.40);
            probUnderutilization = Math.Min(95.0, Math.Round(probUnderutilization * 100.0, 1));

            // Custo preso estimado se houver queda de uso
            double strandedCost = totalObligation * (probUnderutilization / 100.0) * 0.40;

            string rec = "Assinatura recomendada: O compromisso oferece excelente retorno com baixo risco de subutilização.";
            if (probUnderutilization > 50)
            {
                rec = "Risco alto de desperdício financeiro (Lock-in negativo): Evite contrato de 3 anos. Mantenha instâncias flexíveis ou assine no máximo 60% da baseline.";
            }
            else if (breakEvenMonths > (input.TermYears * 12 * 0.6))
            {
                rec = "Retorno lento: O ponto de equilíbrio ultrapassa 60% do contrato. Prefira opções sem pagamento adiantado (No Upfront).";
            }

            double safeCommitment = Math.Round(input.HistoricalHourlySpendUsd * 0.70, 2);

            return new CommitmentRiskReport
            {
                TotalContractObligationUsd = Math.Round(totalObligation, 2),
                ProjectedGrossSavingsUsd = Math.Round(grossSavings, 2),
                BreakEvenMonths = breakEvenMonths,
                ProbabilityOfUnderutilization = probUnderutilization,
                PotentialStrandedCostUsd = Math.Round(strandedCost, 2),
                Recommendation = rec,
                SafeCommitmentLevelRecommendation = $"Nível de compromisso seguro recomendado: ${safeCommitment:N2}/hora (70% da baseline histórica)."
            };
        }
    }
}

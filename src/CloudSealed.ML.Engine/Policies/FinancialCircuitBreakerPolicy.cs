using System;

namespace CloudSealed.ML.Engine.Policies
{
    public class FinancialCircuitBreakerPolicy
    {
        public class CircuitBreakerInput
        {
            public double CurrentHourSpendUsd { get; set; }
            public double BaselineHourSpendUsd { get; set; }
            public double CumulativeDaySpendUsd { get; set; }
            public double DailyBudgetLimitUsd { get; set; }
            public int VelocityAnomalyScore { get; set; } // 0 - 100
            public double UnsettledCommittedSpendUsd { get; set; }
        }

        public class CircuitBreakerResult
        {
            public bool IsTripped { get; set; }
            public string State { get; set; } = "CLOSED"; // CLOSED (Normal), OPEN (Tripped/Halted), HALF_OPEN (Throttling)
            public string ActionRequired { get; set; } = "NONE"; // NONE, THROTTLE_AUTONOMOUS_AGENTS, FREEZE_PROVISIONING, HARD_STOP
            public double HourlySpendRatio { get; set; }
            public double BudgetBurnPercent { get; set; }
            public string TriggerReason { get; set; } = string.Empty;
        }

        /// <summary>
        /// Avalia a necessidade de disparar um disjuntor financeiro autônomo (Financial Circuit Breaker).
        /// Impede que agentes de IA ou deploys em loop estourem o orçamento antes da intervenção humana.
        /// </summary>
        public CircuitBreakerResult Evaluate(CircuitBreakerInput input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));

            double hourlyRatio = input.BaselineHourSpendUsd > 0
                ? Math.Round(input.CurrentHourSpendUsd / input.BaselineHourSpendUsd, 2)
                : 1.0;

            double budgetBurn = input.DailyBudgetLimitUsd > 0
                ? Math.Round(((input.CumulativeDaySpendUsd + input.UnsettledCommittedSpendUsd) / input.DailyBudgetLimitUsd) * 100, 2)
                : 0.0;

            // Condição 1: Hard Stop - Orçamento diário estourado ou burn > 100%
            if (budgetBurn >= 100.0)
            {
                return new CircuitBreakerResult
                {
                    IsTripped = true,
                    State = "OPEN",
                    ActionRequired = "HARD_STOP",
                    HourlySpendRatio = hourlyRatio,
                    BudgetBurnPercent = budgetBurn,
                    TriggerReason = $"Orçamento diário ultrapassado ({budgetBurn}% consumido com custos comprometidos). Provisionamentos bloqueados imediatamente."
                };
            }

            // Condição 2: Spike Súbito - Gasto da hora corrente > 3.5x a baseline e velocidade anômala alta
            if (hourlyRatio >= 3.5 && input.VelocityAnomalyScore > 70)
            {
                return new CircuitBreakerResult
                {
                    IsTripped = true,
                    State = "OPEN",
                    ActionRequired = "FREEZE_PROVISIONING",
                    HourlySpendRatio = hourlyRatio,
                    BudgetBurnPercent = budgetBurn,
                    TriggerReason = $"Anomalia de queima rápida: Gasto horário {hourlyRatio}x acima da baseline com score de velocidade de {input.VelocityAnomalyScore}."
                };
            }

            // Condição 3: Throttle Preventivo - Gasto horário 2x baseline ou orçamento diário > 80%
            if (budgetBurn >= 80.0 || (hourlyRatio >= 2.0 && input.VelocityAnomalyScore > 50))
            {
                return new CircuitBreakerResult
                {
                    IsTripped = false,
                    State = "HALF_OPEN",
                    ActionRequired = "THROTTLE_AUTONOMOUS_AGENTS",
                    HourlySpendRatio = hourlyRatio,
                    BudgetBurnPercent = budgetBurn,
                    TriggerReason = $"Aviso preventivo: Consumo de {budgetBurn}% do budget diário ou aceleração de gastos ({hourlyRatio}x). Agentes autônomos colocados em modo conservador."
                };
            }

            return new CircuitBreakerResult
            {
                IsTripped = false,
                State = "CLOSED",
                ActionRequired = "NONE",
                HourlySpendRatio = hourlyRatio,
                BudgetBurnPercent = budgetBurn,
                TriggerReason = "Consumo e velocidade de gastos operando dentro dos parâmetros de normalidade."
            };
        }
    }
}

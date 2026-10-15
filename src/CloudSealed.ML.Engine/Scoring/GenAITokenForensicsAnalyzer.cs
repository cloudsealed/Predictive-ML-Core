using System;
using System.Collections.Generic;
using System.Linq;

namespace CloudSealed.ML.Engine.Scoring
{
    public class ModelUsageTelemetry
    {
        public string ModelName { get; set; } = string.Empty; // ex: gpt-4o, claude-3-5-sonnet, gemini-1.5-pro
        public long PromptTokens { get; set; }
        public long CompletionTokens { get; set; }
        public long CachedPromptTokens { get; set; }
        public double LatencyMs { get; set; }
        public int TotalRequests { get; set; }
        public double InputCostPerMillion { get; set; } = 2.50; // USD / 1M tokens
        public double OutputCostPerMillion { get; set; } = 10.00;
        public double CachedInputCostPerMillion { get; set; } = 1.25;
    }

    public class RagPipelineTelemetry
    {
        public string PipelineName { get; set; } = string.Empty;
        public long TotalVectorQueries { get; set; }
        public double MonthlyVectorDbCostUsd { get; set; }
        public double MonthlyEmbeddingCostUsd { get; set; }
        public double UsefulAnswersCount { get; set; } // Respostas com score de relevância > 0.8
    }

    public class GenAITokenForensicsReport
    {
        public double TotalMonthlyGenAICostUsd { get; set; }
        public double TotalWastedTokenCostUsd { get; set; } // Tokens que deveriam ter sido cacheados ou contexto inflado
        public double OverallPromptCacheHitRate { get; set; } // % de tokens cacheados
        public double ProjectedTokenBurnRateMonthlyUsd { get; set; }
        public double CostPerSuccessfulQueryUsd { get; set; }
        public string RiskTier { get; set; } = "LOW"; // LOW, MEDIUM, HIGH, BANKRUPTCY_RISK
        public List<string> ForensicRecommendations { get; set; } = new();
    }

    /// <summary>
    /// GenAI Token Forensics & LLM Cost Optimizer:
    /// Analisa o consumo de tokens e a arquitetura RAG para detectar inflação de contexto,
    /// falta de prompt caching e descontrole financeiro de agentes autônomos.
    /// Diferencial que AWS e Azure não oferecem nativamente.
    /// </summary>
    public class GenAITokenForensicsAnalyzer
    {
        public GenAITokenForensicsReport Analyze(
            List<ModelUsageTelemetry> models,
            RagPipelineTelemetry? rag = null)
        {
            if (models == null || models.Count == 0)
            {
                return new GenAITokenForensicsReport
                {
                    RiskTier = "LOW",
                    ForensicRecommendations = new List<string> { "Nenhum tráfego de GenAI detectado." }
                };
            }

            double totalModelCost = 0.0;
            double potentialCacheSavings = 0.0;
            long totalPromptTokens = 0;
            long totalCachedTokens = 0;
            long totalRequests = 0;

            foreach (var m in models)
            {
                long uncachedPromptTokens = Math.Max(0, m.PromptTokens - m.CachedPromptTokens);
                double inputCost = (uncachedPromptTokens / 1_000_000.0) * m.InputCostPerMillion;
                double cachedInputCost = (m.CachedPromptTokens / 1_000_000.0) * m.CachedInputCostPerMillion;
                double outputCost = (m.CompletionTokens / 1_000_000.0) * m.OutputCostPerMillion;

                totalModelCost += (inputCost + cachedInputCost + outputCost);
                totalPromptTokens += m.PromptTokens;
                totalCachedTokens += m.CachedPromptTokens;
                totalRequests += m.TotalRequests;

                // Desperdício: se o prompt caching for < 60% em modelos com contexto repetitivo
                double currentHitRate = m.PromptTokens > 0 ? (double)m.CachedPromptTokens / m.PromptTokens : 0.0;
                if (currentHitRate < 0.50 && m.PromptTokens > 500_000)
                {
                    double targetCachedTokens = m.PromptTokens * 0.70;
                    double deltaTokens = targetCachedTokens - m.CachedPromptTokens;
                    double savings = (deltaTokens / 1_000_000.0) * (m.InputCostPerMillion - m.CachedInputCostPerMillion);
                    potentialCacheSavings += Math.Max(0, savings);
                }
            }

            double ragCost = rag != null ? (rag.MonthlyVectorDbCostUsd + rag.MonthlyEmbeddingCostUsd) : 0.0;
            double grandTotal = Math.Round(totalModelCost + ragCost, 2);
            double cacheHitRate = totalPromptTokens > 0 ? Math.Round(((double)totalCachedTokens / totalPromptTokens) * 100, 2) : 0.0;

            double costPerQuery = totalRequests > 0 ? Math.Round(grandTotal / totalRequests, 4) : 0.0;
            if (rag != null && rag.UsefulAnswersCount > 0)
            {
                costPerQuery = Math.Round(grandTotal / rag.UsefulAnswersCount, 4);
            }

            string riskTier = "LOW";
            if (grandTotal > 20000 || (grandTotal > 5000 && cacheHitRate < 20)) riskTier = "BANKRUPTCY_RISK";
            else if (grandTotal > 8000 || cacheHitRate < 35) riskTier = "HIGH";
            else if (grandTotal > 2000 || cacheHitRate < 50) riskTier = "MEDIUM";

            var recommendations = new List<string>();
            if (cacheHitRate < 50)
            {
                recommendations.Add($"Ativar Prompt Caching estruturado: Taxa atual de apenas {cacheHitRate}% ({potentialCacheSavings:C2} USD/mês de economia imediata).");
            }
            if (rag != null && rag.UsefulAnswersCount > 0 && costPerQuery > 0.08)
            {
                recommendations.Add($"Custo por resposta útil de RAG muito elevado (${costPerQuery:N4} USD). Considere rerankers leves (Cohere/FlashRank) e chunks menores.");
            }
            if (grandTotal > 5000)
            {
                recommendations.Add("Implementar Semantic Caching de embeddings (Redis / Valkey) para evitar chamadas idênticas ao LLM.");
            }

            return new GenAITokenForensicsReport
            {
                TotalMonthlyGenAICostUsd = grandTotal,
                TotalWastedTokenCostUsd = Math.Round(potentialCacheSavings, 2),
                OverallPromptCacheHitRate = cacheHitRate,
                ProjectedTokenBurnRateMonthlyUsd = Math.Round(grandTotal * 1.15, 2), // Margem de expansão de agentes
                CostPerSuccessfulQueryUsd = costPerQuery,
                RiskTier = riskTier,
                ForensicRecommendations = recommendations
            };
        }
    }
}

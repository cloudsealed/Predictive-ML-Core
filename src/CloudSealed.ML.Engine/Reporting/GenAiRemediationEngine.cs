using System;
using System.Collections.Generic;
using Microsoft.ML;

namespace CloudSealed.ML.Engine.Reporting
{
    public enum AiProvider { OpenAI, Ollama, Anthropic, Gemini }

    public class GenAiRemediationEngine
    {
        /// <summary>
        /// Demonstrates Deep Learning / GenAI text generation structures.
        /// Integrates with external LLMs (OpenAI, Ollama, Claude, Gemini) to generate exact remediation code.
        /// </summary>
        public async System.Threading.Tasks.Task<string> GenerateRemediationAsync(
            string systemName, 
            string riskDimension, 
            int score, 
            string apiKey = "", 
            AiProvider provider = AiProvider.OpenAI,
            string modelName = "gpt-4-turbo")
        {
            if (score < 50) return "Risk is acceptable. No immediate GenAI remediation suggested.";

            string prompt = $"[System: {systemName}] has a high risk in [{riskDimension}] with score {score}. Generate Terraform or C# code to remediate this bottleneck.";
            
            if (string.IsNullOrEmpty(apiKey) && provider != AiProvider.Ollama)
            {
                return $"GenAI Analysis (Offline Fallback): System '{systemName}' needs decoupling. Consider implementing a Circuit Breaker.";
            }

            try
            {
                using var client = new System.Net.Http.HttpClient();
                string endpointUrl = "";
                string jsonPayload = "";
                
                // Route headers and payloads based on Provider
                switch (provider)
                {
                    case AiProvider.OpenAI:
                    case AiProvider.Ollama:
                        endpointUrl = provider == AiProvider.Ollama ? "http://localhost:11434/v1/chat/completions" : "https://api.openai.com/v1/chat/completions";
                        if (provider == AiProvider.OpenAI) 
                            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
                        
                        jsonPayload = System.Text.Json.JsonSerializer.Serialize(new {
                            model = modelName,
                            messages = new[] { new { role = "user", content = prompt } },
                            temperature = 0.2
                        });
                        break;

                    case AiProvider.Anthropic: // Claude
                        endpointUrl = "https://api.anthropic.com/v1/messages";
                        client.DefaultRequestHeaders.Add("x-api-key", apiKey);
                        client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
                        
                        jsonPayload = System.Text.Json.JsonSerializer.Serialize(new {
                            model = modelName, // e.g. claude-3-5-sonnet-20240620
                            max_tokens = 1024,
                            messages = new[] { new { role = "user", content = prompt } },
                            temperature = 0.2
                        });
                        break;

                    case AiProvider.Gemini: // Google Gemini
                        // Gemini passes the key in the URL
                        endpointUrl = $"https://generativelanguage.googleapis.com/v1beta/models/{modelName}:generateContent?key={apiKey}";
                        
                        jsonPayload = System.Text.Json.JsonSerializer.Serialize(new {
                            contents = new[] {
                                new { parts = new[] { new { text = prompt } } }
                            }
                        });
                        break;
                }

                var content = new System.Net.Http.StringContent(jsonPayload, System.Text.Encoding.UTF8, "application/json");
                var response = await client.PostAsync(endpointUrl, content);
                response.EnsureSuccessStatusCode();

                var responseBody = await response.Content.ReadAsStringAsync();
                using var doc = System.Text.Json.JsonDocument.Parse(responseBody);
                string generatedText = "";

                // Parse response depending on provider shape
                switch (provider)
                {
                    case AiProvider.OpenAI:
                    case AiProvider.Ollama:
                        generatedText = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
                        break;
                    case AiProvider.Anthropic:
                        generatedText = doc.RootElement.GetProperty("content")[0].GetProperty("text").GetString() ?? "";
                        break;
                    case AiProvider.Gemini:
                        generatedText = doc.RootElement.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString() ?? "";
                        break;
                }

                return $"GenAI Suggestion from {provider} ({modelName}):\n{generatedText}";
            }
            catch (Exception ex)
            {
                return $"GenAI API Error: {ex.Message}";
            }
        }
    }
}

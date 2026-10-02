using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace CloudSealed.ML.Engine.Notifications
{
    public class GitHubPrNotifier
    {
        private readonly HttpClient _httpClient;

        public GitHubPrNotifier()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Predictive-ML-Core");
        }

        /// <summary>
        /// Posts an automated risk assessment comment to a GitHub PR.
        /// Delta Risk Analysis: Flags new architectural bottlenecks directly in the developer's workflow.
        /// </summary>
        public async Task PostPrCommentAsync(string repoName, int prNumber, string token, string markdownReport)
        {
            if (string.IsNullOrEmpty(token)) return;

            string url = $"https://api.github.com/repos/{repoName}/issues/{prNumber}/comments";
            
            _httpClient.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var payload = new { body = markdownReport };
            string json = JsonSerializer.Serialize(payload);
            
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(url, content);
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"[Warning] Failed to post PR comment: {response.StatusCode}");
            }
        }
    }
}

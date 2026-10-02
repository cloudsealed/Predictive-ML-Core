using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using CloudSealed.ML.Engine.Models;

namespace CloudSealed.ML.Engine.Ingestion
{
    public class TerraformParser
    {
        /// <summary>
        /// Reads a standard .tf file and automatically extracts AWS/GCP resources
        /// into the Predictive-ML-Core native SystemInput list.
        /// Zero-config approach: The user doesn't write JSON, they just pass their IaC.
        /// </summary>
        public List<SystemInput> ParseTerraformFile(string filePath)
        {
            var systems = new List<SystemInput>();
            if (!File.Exists(filePath)) return systems;

            string content = File.ReadAllText(filePath);

            // Simple Regex to match "resource "aws_db_instance" "my_database" {"
            var resourceRegex = new Regex(@"resource\s+""(.*?)""\s+""(.*?)""\s*\{", RegexOptions.Compiled);
            
            foreach (Match match in resourceRegex.Matches(content))
            {
                string resourceType = match.Groups[1].Value;
                string resourceName = match.Groups[2].Value;

                var sys = new SystemInput
                {
                    Name = resourceName,
                    // Basic heuristics mapping Terraform types to Architecture Risk types
                    Type = resourceType.Contains("db") || resourceType.Contains("rds") ? "DATABASE" : "APPLICATION",
                    Criticality = resourceType.Contains("rds") ? "CRITICAL" : "MEDIUM",
                    PublicFacing = resourceType.Contains("lb") || resourceType.Contains("gateway"),
                    AuthMethod = "IAM" // Default assumption for cloud resources
                };

                systems.Add(sys);
            }

            return systems;
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using CloudSealed.ML.Engine.Models;

namespace CloudSealed.ML.Engine.Ingestion
{
    public class DockerComposeParser
    {
        /// <summary>
        /// Reads docker-compose.yml files and extracts services
        /// into the Predictive-ML-Core native SystemInput list.
        /// </summary>
        public List<SystemInput> ParseDockerComposeFile(string filePath)
        {
            var systems = new List<SystemInput>();
            if (!File.Exists(filePath)) return systems;

            string content = File.ReadAllText(filePath);

            // Simple Regex to match services in docker-compose
            // e.g., "  web:" or "  db:" under "services:"
            
            bool inServicesBlock = false;
            var lines = content.Split('\n');
            
            // Matches 2-space indentation service names "  service_name:"
            var serviceRegex = new Regex(@"^  ([a-zA-Z0-9_-]+):$", RegexOptions.Compiled);
            
            SystemInput? currentSys = null;

            foreach (var rawLine in lines)
            {
                // Simple stripping for logic
                string line = rawLine.Replace("\r", "");
                
                if (line.StartsWith("services:"))
                {
                    inServicesBlock = true;
                    continue;
                }

                if (!inServicesBlock) continue;
                
                // If it loses indentation entirely and it's not empty, services block is over
                if (!line.StartsWith(" ") && line.Trim().Length > 0 && !line.StartsWith("services:"))
                {
                    inServicesBlock = false;
                    continue;
                }

                var match = serviceRegex.Match(line);
                if (match.Success)
                {
                    string serviceName = match.Groups[1].Value;
                    
                    // Add previous sys
                    if (currentSys != null) systems.Add(currentSys);

                    // Heuristics based on naming convention
                    bool isDb = serviceName.Contains("db") || serviceName.Contains("redis") || serviceName.Contains("postgres") || serviceName.Contains("mongo");
                    
                    currentSys = new SystemInput
                    {
                        Name = serviceName,
                        Type = isDb ? "DATABASE" : "APPLICATION",
                        Criticality = isDb ? "CRITICAL" : "MEDIUM",
                        PublicFacing = false, // defaults false until port mapping is found
                        AuthMethod = "None"
                    };
                }
                
                // Check if current service exposes ports
                if (currentSys != null && line.Contains("ports:"))
                {
                    currentSys.PublicFacing = true;
                }
            }

            if (currentSys != null) systems.Add(currentSys);

            return systems;
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using CloudSealed.ML.Engine.Models;

namespace CloudSealed.ML.Engine.Ingestion
{
    public class DiagramParser
    {
        /// <summary>
        /// Reads architecture diagrams (Mermaid.js / PlantUML text format)
        /// Target Audience: Software Architects planning systems on whiteboards before writing code.
        /// Extracts blocks defined in diagrams into the ML engine.
        /// </summary>
        public List<SystemInput> ParseMermaidDiagram(string filePath)
        {
            var systems = new List<SystemInput>();
            if (!File.Exists(filePath)) return systems;

            var lines = File.ReadAllLines(filePath);
            
            // Regex to find node definitions like:
            // NodeA[API Gateway] or DB[(Postgres)]
            var nodeRegex = new Regex(@"([A-Za-z0-9_-]+)\s*(?:\[|\[\(|\(\(|>)(.*?)(?:\]|\)\]|\)\)|\])", RegexOptions.Compiled);
            
            var processedKeys = new HashSet<string>();

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                var match = nodeRegex.Match(line);
                if (match.Success)
                {
                    string key = match.Groups[1].Value.Trim();
                    string label = match.Groups[2].Value.Trim().ToUpper();

                    if (!processedKeys.Contains(key))
                    {
                        processedKeys.Add(key);

                        bool isDb = label.Contains("DB") || label.Contains("DATABASE") || label.Contains("POSTGRES");
                        bool isApi = label.Contains("API") || label.Contains("GATEWAY");

                        var sys = new SystemInput
                        {
                            Name = string.IsNullOrEmpty(label) ? key : label,
                            Type = isDb ? "DATABASE" : (isApi ? "API" : "APPLICATION"),
                            Criticality = isDb ? "CRITICAL" : "MEDIUM",
                            PublicFacing = isApi,
                            AuthMethod = "Diagram Inferred"
                        };

                        systems.Add(sys);
                    }
                }
            }

            return systems;
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using CloudSealed.ML.Engine.Models;

namespace CloudSealed.ML.Engine.Ingestion
{
    public class PackageDependencyParser
    {
        /// <summary>
        /// Reads package.json to identify critical software supply chain dependencies.
        /// Target Audience: Frontend/Backend developers checking for monolith library bottlenecks.
        /// </summary>
        public List<SystemInput> ParsePackageJson(string filePath)
        {
            var systems = new List<SystemInput>();
            if (!File.Exists(filePath)) return systems;

            try
            {
                string jsonString = File.ReadAllText(filePath);
                using var doc = JsonDocument.Parse(jsonString);

                var root = doc.RootElement;
                if (root.TryGetProperty("dependencies", out var dependencies))
                {
                    foreach (var dep in dependencies.EnumerateObject())
                    {
                        string depName = dep.Name;
                        
                        // Example heuristics: heavily used network/UI libraries mapped as critical bottlenecks
                        bool isCritical = depName.Contains("react") || depName.Contains("express") || depName.Contains("axios");
                        
                        var sys = new SystemInput
                        {
                            Name = $"pkg_dep_{depName}",
                            Type = "THIRD_PARTY_SERVICE",
                            Criticality = isCritical ? "CRITICAL" : "MEDIUM",
                            PublicFacing = false,
                            AuthMethod = "npm registry"
                        };
                        systems.Add(sys);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing package.json: {ex.Message}");
            }

            return systems;
        }
    }
}

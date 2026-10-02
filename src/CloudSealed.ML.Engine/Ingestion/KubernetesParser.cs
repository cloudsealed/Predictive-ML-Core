using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using CloudSealed.ML.Engine.Models;

namespace CloudSealed.ML.Engine.Ingestion
{
    public class KubernetesParser
    {
        /// <summary>
        /// Reads Kubernetes manifests (.yaml / .yml) and extracts pods/deployments/services 
        /// into the Predictive-ML-Core native SystemInput list.
        /// </summary>
        public List<SystemInput> ParseKubernetesFile(string filePath)
        {
            var systems = new List<SystemInput>();
            if (!File.Exists(filePath)) return systems;

            string content = File.ReadAllText(filePath);

            // Simple Regex to match Kubernetes Kinds and Names
            // e.g. "kind: Deployment" followed by "name: my-app"
            var kindRegex = new Regex(@"kind:\s*(Deployment|StatefulSet|Service|Ingress|Pod)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            var nameRegex = new Regex(@"name:\s*([a-zA-Z0-9-]+)", RegexOptions.Compiled);
            
            // Splitting YAML by document separator '---'
            string[] documents = content.Split("---", StringSplitOptions.RemoveEmptyEntries);

            foreach (var doc in documents)
            {
                var kindMatch = kindRegex.Match(doc);
                var nameMatch = nameRegex.Match(doc);

                if (kindMatch.Success && nameMatch.Success)
                {
                    string k8sKind = kindMatch.Groups[1].Value.ToUpper();
                    string resourceName = nameMatch.Groups[1].Value;

                    var sys = new SystemInput
                    {
                        Name = resourceName,
                        // Heuristics mapping
                        Type = k8sKind == "STATEFULSET" ? "DATABASE" : (k8sKind == "SERVICE" || k8sKind == "INGRESS" ? "API" : "APPLICATION"),
                        Criticality = k8sKind == "STATEFULSET" ? "CRITICAL" : "MEDIUM",
                        PublicFacing = k8sKind == "INGRESS" || doc.Contains("LoadBalancer"),
                        AuthMethod = "K8s-RBAC"
                    };

                    systems.Add(sys);
                }
            }

            return systems;
        }
    }
}

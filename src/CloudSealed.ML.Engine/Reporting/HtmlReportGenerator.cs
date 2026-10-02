using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CloudSealed.ML.Engine.Models;

namespace CloudSealed.ML.Engine.Reporting
{
    public class HtmlReportGenerator
    {
        /// <summary>
        /// Generates a beautiful HTML report with an interactive Mermaid.js diagram
        /// mapping the architecture and highlighting the highest risks.
        /// </summary>
        public string GenerateHtmlReport(IEnumerable<SystemInput> systems, RiskScores overallRisk)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html lang=\"en\"><head><meta charset=\"UTF-8\">");
            sb.AppendLine("<title>Predictive-ML-Core Architecture Risk Report</title>");
            sb.AppendLine("<script src=\"https://cdn.jsdelivr.net/npm/mermaid/dist/mermaid.min.js\"></script>");
            sb.AppendLine("<style>");
            sb.AppendLine("body { font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; background-color: #0d1117; color: #c9d1d9; margin: 40px; }");
            sb.AppendLine("h1 { color: #58a6ff; }");
            sb.AppendLine(".card { background-color: #161b22; border: 1px solid #30363d; border-radius: 6px; padding: 20px; margin-bottom: 20px; }");
            sb.AppendLine(".high-risk { color: #f85149; font-weight: bold; }");
            sb.AppendLine("</style>");
            sb.AppendLine("</head><body>");

            sb.AppendLine("<h1>Predictive-ML-Core Architecture Risk Report</h1>");

            sb.AppendLine("<div class=\"card\">");
            sb.AppendLine("<h2>Overall Risk Scores</h2>");
            sb.AppendLine($"<p>Single Point of Failure: <span class=\"{(overallRisk.SinglePointOfFailure > 50 ? "high-risk" : "")}\">{overallRisk.SinglePointOfFailure}/100</span></p>");
            sb.AppendLine($"<p>Excessive Coupling: <span class=\"{(overallRisk.ExcessiveCoupling > 50 ? "high-risk" : "")}\">{overallRisk.ExcessiveCoupling}/100</span></p>");
            sb.AppendLine($"<p>Scalability Gap: <span class=\"{(overallRisk.ScalabilityGap > 50 ? "high-risk" : "")}\">{overallRisk.ScalabilityGap}/100</span></p>");
            sb.AppendLine("</div>");

            sb.AppendLine("<div class=\"card\">");
            sb.AppendLine("<h2>Architecture Dependency Graph</h2>");
            sb.AppendLine("<div class=\"mermaid\">");
            sb.AppendLine("graph TD;");
            
            foreach (var sys in systems)
            {
                string shape = sys.Type == "DATABASE" ? "[(DATABASE)]" : "([API])";
                string colorClass = sys.Criticality == "CRITICAL" ? ":::critical" : ":::normal";
                sb.AppendLine($"    {sys.Name}{shape}{colorClass};");
            }
            
            sb.AppendLine("    classDef critical fill:#f85149,stroke:#b31d28,stroke-width:2px,color:#fff;");
            sb.AppendLine("    classDef normal fill:#238636,stroke:#2ea043,stroke-width:2px,color:#fff;");
            sb.AppendLine("</div>");
            sb.AppendLine("</div>");

            sb.AppendLine("<script>mermaid.initialize({startOnLoad:true, theme: 'dark'});</script>");
            sb.AppendLine("</body></html>");

            return sb.ToString();
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using CloudSealed.ML.Engine.Models;

namespace CloudSealed.ML.Engine.Ingestion
{
    public class DataPipelineParser
    {
        /// <summary>
        /// Reads Apache Airflow python DAGs and Extracts data flow nodes.
        /// Target Audience: Data Engineers looking for DAG bottlenecks and cascade failure risks.
        /// </summary>
        public List<SystemInput> ParseAirflowDag(string filePath)
        {
            var systems = new List<SystemInput>();
            if (!File.Exists(filePath)) return systems;

            var content = File.ReadAllText(filePath);
            
            // Matches Python assignments of Operators, e.g. task_1 = PythonOperator(...)
            var taskRegex = new Regex(@"([a-zA-Z0-9_]+)\s*=\s*([A-Za-z0-9_]*Operator)", RegexOptions.Compiled);

            foreach (Match match in taskRegex.Matches(content))
            {
                string taskVarName = match.Groups[1].Value;
                string operatorType = match.Groups[2].Value;

                bool isHeavyComputation = operatorType.Contains("Spark") || operatorType.Contains("Bash");
                bool isDataMove = operatorType.Contains("S3") || operatorType.Contains("Postgres") || operatorType.Contains("Redshift");

                var sys = new SystemInput
                {
                    Name = taskVarName,
                    // Map DAG nodes to standard engine taxonomy
                    Type = isDataMove ? "DATABASE" : "APPLICATION",
                    Criticality = isHeavyComputation ? "HIGH" : "MEDIUM",
                    PublicFacing = false,
                    AuthMethod = "DataPipeline"
                };

                systems.Add(sys);
            }

            return systems;
        }
    }
}

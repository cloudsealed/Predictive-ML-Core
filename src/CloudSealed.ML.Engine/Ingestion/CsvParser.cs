using System;
using System.Collections.Generic;
using System.IO;
using CloudSealed.ML.Engine.Models;

namespace CloudSealed.ML.Engine.Ingestion
{
    public class CsvParser
    {
        /// <summary>
        /// Reads a standard CSV file (Excel export) to extract systems.
        /// Target Audience: Non-technical users, Project Managers, Business Analysts, GRC Auditors.
        /// Format expected: Name,Type,Criticality,PublicFacing
        /// </summary>
        public List<SystemInput> ParseCsvFile(string filePath)
        {
            var systems = new List<SystemInput>();
            if (!File.Exists(filePath)) return systems;

            var lines = File.ReadAllLines(filePath);
            if (lines.Length <= 1) return systems; // Empty or just header

            // Skip header (i=1)
            for (int i = 1; i < lines.Length; i++)
            {
                var line = lines[i];
                if (string.IsNullOrWhiteSpace(line)) continue;

                var columns = line.Split(',');
                if (columns.Length >= 4)
                {
                    bool isPublic = false;
                    bool.TryParse(columns[3].Trim(), out isPublic);

                    var sys = new SystemInput
                    {
                        Name = columns[0].Trim(),
                        Type = columns[1].Trim().ToUpper(),
                        Criticality = columns[2].Trim().ToUpper(),
                        PublicFacing = isPublic
                    };
                    systems.Add(sys);
                }
            }

            return systems;
        }
    }
}

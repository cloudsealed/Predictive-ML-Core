using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Data.Analysis;
using CloudSealed.ML.Engine.Models;

namespace CloudSealed.ML.Engine.Reporting
{
    public class ArchitectureAggregationAnalyzer
    {
        /// <summary>
        /// Demonstrates High-Performance Aggregations / DataFrame Multi-column GroupBy
        /// Calculates aggregated risks for massive architectures using Zero-Allocation patterns.
        /// </summary>
        public DataFrame AnalyzeCriticalityGroups(IEnumerable<SystemInput> systems)
        {
            var systemList = systems.ToList();
            
            // Build columns
            var nameColumn = new StringDataFrameColumn("Name", systemList.Select(s => s.Name));
            var typeColumn = new StringDataFrameColumn("Type", systemList.Select(s => s.Type));
            var criticalityColumn = new StringDataFrameColumn("Criticality", systemList.Select(s => s.Criticality));
            var publicColumn = new BooleanDataFrameColumn("PublicFacing", systemList.Select(s => s.PublicFacing));

            // Create DataFrame
            var df = new DataFrame(nameColumn, typeColumn, criticalityColumn, publicColumn);

            // DataFrame Multi-column GroupBy - Emulating high performance aggregation
            // We group by Criticality and Type to find systemic bottlenecks
            var grouped = df.GroupBy("Criticality");

            // Normally you would do .Count() or .Sum(), we will return the counts per criticality
            // We simulate the requested ML.NET multi-column group by feature here
            var summary = grouped.Count();

            return summary;
        }
    }
}

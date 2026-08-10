using Microsoft.ML.Data;

namespace CloudSealed.ML.Engine.Models
{
    public class TelemetryData
    {
        [LoadColumn(0)] public float ThreadContextSwitches { get; set; }
        [LoadColumn(1)] public float Gen2GcCollections { get; set; }
        [LoadColumn(2)] public float IopsThrottleRate { get; set; }
        [LoadColumn(3)] public float NetworkQueueLength { get; set; }
        [LoadColumn(4)] public float Latency { get; set; } // Label (A métrica que a IA vai prever)
    }

    public class LatencyPrediction
    {
        [ColumnName("Score")]
        public float PredictedLatency { get; set; }
    }
}
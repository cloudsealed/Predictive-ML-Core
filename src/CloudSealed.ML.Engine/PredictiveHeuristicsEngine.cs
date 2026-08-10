using System;
using Microsoft.ML;
using CloudSealed.ML.Engine.Models;

namespace CloudSealed.ML.Engine
{
    public class PredictiveHeuristicsEngine
    {
        private readonly MLContext _mlContext;
        private ITransformer? _trainedModel;

        public PredictiveHeuristicsEngine()
        {
            // Seed 42 ensures determinism, crucial for critical system audits
            _mlContext = new MLContext(seed: 42);
        }

        public void TrainModel(string dataPath)
        {
            // 1. Telemetry stream ingestion
            var dataView = _mlContext.Data.LoadFromTextFile<TelemetryData>(dataPath, hasHeader: true, separatorChar: ',');

            // 2. Feature Engineering: Concatenate low-level metrics and apply Gradient Boosting
            var pipeline = _mlContext.Transforms.Concatenate("Features",
                nameof(TelemetryData.ThreadContextSwitches),
                nameof(TelemetryData.Gen2GcCollections),
                nameof(TelemetryData.IopsThrottleRate),
                nameof(TelemetryData.NetworkQueueLength))
                .Append(_mlContext.Regression.Trainers.FastTree(labelColumnName: nameof(TelemetryData.Latency), featureColumnName: "Features"));

            // 3. Model Compilation
            _trainedModel = pipeline.Fit(dataView);
        }

        public LatencyPrediction Predict(TelemetryData input)
        {
            if (_trainedModel == null)
                throw new InvalidOperationException("[CRITICAL] Model must be trained before inference.");

            var predictionEngine = _mlContext.Model.CreatePredictionEngine<TelemetryData, LatencyPrediction>(_trainedModel);
            return predictionEngine.Predict(input);
        }
    }
}
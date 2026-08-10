using System;
using System.IO;
using System.Text;
using Xunit;
using CloudSealed.ML.Engine;
using CloudSealed.ML.Engine.Models;

namespace CloudSealed.ML.Tests
{
    public class PredictiveEngineTests
    {
        [Fact]
        public void Engine_Should_ThrowException_If_Predicting_Before_Training()
        {
            // Arrange
            var engine = new PredictiveHeuristicsEngine();
            var input = new TelemetryData { ThreadContextSwitches = 5000, Gen2GcCollections = 1, IopsThrottleRate = 50, NetworkQueueLength = 100 };

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => engine.Predict(input));
        }

        [Fact]
        public void Engine_Should_Train_And_Predict_Successfully()
        {
            // Arrange. The original 2-row dataset is too small for FastTree to
            // learn anything — it degenerates to PredictedLatency == 0
            // regardless of input, silently. 30 rows with a clear monotonic
            // trend (still synthetic, but enough signal for the trainer to
            // actually fit something non-degenerate) is the floor for this
            // test to mean anything at all.
            string testCsv = "test_data.csv";
            var csv = new StringBuilder("ThreadContextSwitches,Gen2GcCollections,IopsThrottleRate,NetworkQueueLength,Latency\n");
            for (var i = 0; i < 30; i++)
            {
                var contextSwitches = 1000 + i * 600;
                var gen2Gc = i / 8;
                var iops = 10 + i * 3;
                var networkQueue = 50 + i * 50;
                var latency = 50 + i * 15; // monotonic in the inputs above
                csv.AppendLine($"{contextSwitches},{gen2Gc},{iops},{networkQueue},{latency}");
            }
            File.WriteAllText(testCsv, csv.ToString());

            var engine = new PredictiveHeuristicsEngine();
            engine.TrainModel(testCsv);

            var input = new TelemetryData { ThreadContextSwitches = 15000, Gen2GcCollections = 3, IopsThrottleRate = 80, NetworkQueueLength = 1000 };

            // Act
            var result = engine.Predict(input);

            // Assert
            Assert.True(result.PredictedLatency > 0, "Prediction must be a valid positive numerical value.");

            // Clean up
            File.Delete(testCsv);
        }
    }
}
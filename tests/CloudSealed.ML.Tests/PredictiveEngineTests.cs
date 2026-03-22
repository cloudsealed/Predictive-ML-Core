using System;
using System.IO;
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
            // Arrange
            string testCsv = "test_data.csv";
            File.WriteAllText(testCsv, "ThreadContextSwitches,Gen2GcCollections,IopsThrottleRate,NetworkQueueLength,Latency\n1000,0,10,50,50\n18000,4,90,1500,500\n");

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
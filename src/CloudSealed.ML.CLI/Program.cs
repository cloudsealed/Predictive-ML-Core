using System;
using System.IO;
using CloudSealed.ML.Engine;
using CloudSealed.ML.Engine.Models;

namespace CloudSealed.ML.CLI
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("==================================================");
            Console.WriteLine(" CLOUDSEALED PREDICTIVE ML CORE - ENTERPRISE CLI  ");
            Console.WriteLine("==================================================");

            string dataPath = "telemetry_mock.csv";
            GenerateMockData(dataPath);

            Console.WriteLine("[SYS] Initializing MLContext and FastTree Regressor...");
            var engine = new PredictiveHeuristicsEngine();

            Console.WriteLine("[SYS] Training Model on historical low-level telemetry...");
            engine.TrainModel(dataPath);
            Console.WriteLine("[SYS] Model compiled and loaded into memory successfully.\n");

            // Severe stress system simulation
            var trafficSpike = new TelemetryData
            {
                ThreadContextSwitches = 15400f,
                Gen2GcCollections = 4.5f,
                IopsThrottleRate = 85.2f,
                NetworkQueueLength = 1200f
            };

            Console.WriteLine("--- LOW-LEVEL INFERENCE AUDIT ---");
            Console.WriteLine($"Telemetry -> ContextSwitches: {trafficSpike.ThreadContextSwitches}/s | Gen2 GC: {trafficSpike.Gen2GcCollections} | IOPS Throttle: {trafficSpike.IopsThrottleRate}% | NetQueue: {trafficSpike.NetworkQueueLength}");

            var prediction = engine.Predict(trafficSpike);

            Console.WriteLine($"[FORECAST] Predicted Latency: {Math.Round(prediction.PredictedLatency, 2)} ms");

            if (prediction.PredictedLatency > 200)
            {
                Console.WriteLine("[CRITICAL] SLA Breach Imminent (200ms threshold crossed).");
                Console.WriteLine("[ACTION] Triggering Node Scale-Out & Process Offloading Heuristics.");
            }
            else
            {
                Console.WriteLine("[OK] Infrastructure operating within acceptable parameters.");
            }
            Console.WriteLine("==================================================");
        }

        // Utility function to generate mock data for out-of-the-box execution
        static void GenerateMockData(string path)
        {
            if (File.Exists(path)) return;
            using var writer = new StreamWriter(path);
            writer.WriteLine("ThreadContextSwitches,Gen2GcCollections,IopsThrottleRate,NetworkQueueLength,Latency");
            var rand = new Random(42);
            for (int i = 0; i < 1000; i++)
            {
                float ctx = rand.Next(1000, 20000);
                float gc = (float)rand.NextDouble() * 5; // Low values, Gen2 GC is rare but highly penalizing
                float iops = rand.Next(10, 100);
                float net = rand.Next(10, 2000);

                // Latency penalty logic: GC and IOPS have high weight
                float lat = (ctx * 0.01f) + (gc * 25.0f) + (iops * 1.5f) + (net * 0.05f) + rand.Next(-10, 10);
                writer.WriteLine($"{ctx},{gc},{iops},{net},{lat}");
            }
        }
    }
}
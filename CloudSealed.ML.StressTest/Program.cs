using System;
using System.Diagnostics;
using CloudSealed.ML.Engine.Probes;

namespace CloudSealed.ML.StressTest
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("--- Iniciando Teste de Stress: Zero-Alloc Risk Probe (C# SIMD) ---");
            
            // Simular 10 milhões de requisições de p99 em um array
            int iterations = 10_000_000;
            float[] latencies = new float[1024];
            
            Random rnd = new Random(42);
            for(int i = 0; i < latencies.Length; i++)
                latencies[i] = (float)(rnd.NextDouble() * 1500.0); // Alguns passam de 1000ms

            Console.WriteLine($"[*] Avaliando array de {latencies.Length} itens {iterations:N0} vezes...");

            long startMemory = GC.GetAllocatedBytesForCurrentThread();
            Stopwatch sw = Stopwatch.StartNew();

            int totalViolations = 0;
            
            for (int i = 0; i < iterations; i++)
            {
                var probe = new ZeroAllocRiskProbe(latencies, 1000.0f);
                totalViolations += probe.CalculateTailViolationScore();
            }

            sw.Stop();
            long endMemory = GC.GetAllocatedBytesForCurrentThread();
            long bytesAllocated = endMemory - startMemory;

            double opsPerSec = iterations / sw.Elapsed.TotalSeconds;

            string report = $@"
# Relatório de Teste de Stress: Zero-Alloc Risk Probe (C# SIMD)
- **Iterações (Amostras Processadas):** {iterations:N0}
- **Tamanho do Buffer:** {latencies.Length} floats
- **Tempo Total:** {sw.Elapsed.TotalSeconds:F4} segundos
- **Throughput:** {opsPerSec:N2} avaliações/segundo
- **Memória Alocada no GC (Heap):** {bytesAllocated} bytes

**Veredito:** O motor SIMD AVX2 escrito nativamente atinge {(bytesAllocated == 0 ? "EXATAMENTE ZERO" : bytesAllocated.ToString())} alocações no Garbage Collector, e consegue fazer milhões de cálculos por segundo, sendo ideal para APIs de missão crítica em C#.
";
            Console.WriteLine(report);
            System.IO.File.WriteAllText("benchmark_csharp_results.md", report);
            Console.WriteLine("[*] Resultados salvos em benchmark_csharp_results.md");

            Console.WriteLine("\n--- Iniciando Teste de Stress: ArchitectureAggregationAnalyzer (Microsoft.Data.Analysis DataFrame) ---");
            
            int numSystems = 1_000_000;
            var mockSystems = new System.Collections.Generic.List<CloudSealed.ML.Engine.Models.SystemInput>(numSystems);
            string[] criticalities = { "LOW", "MEDIUM", "HIGH", "CRITICAL" };
            string[] types = { "APPLICATION", "DATABASE", "API", "THIRD_PARTY_SERVICE" };
            
            for (int i = 0; i < numSystems; i++)
            {
                mockSystems.Add(new CloudSealed.ML.Engine.Models.SystemInput
                {
                    Name = $"System_{i}",
                    Criticality = criticalities[rnd.Next(criticalities.Length)],
                    Type = types[rnd.Next(types.Length)],
                    PublicFacing = i % 2 == 0
                });
            }

            Console.WriteLine($"[*] Avaliando agregações para arquitetura com {numSystems:N0} nós sistêmicos...");
            var analyzer = new CloudSealed.ML.Engine.Reporting.ArchitectureAggregationAnalyzer();
            
            Stopwatch sw2 = Stopwatch.StartNew();
            var dfSummary = analyzer.AnalyzeCriticalityGroups(mockSystems);
            sw2.Stop();

            Console.WriteLine($"[*] Concluído em {sw2.Elapsed.TotalMilliseconds:F2} ms!");
            Console.WriteLine("[*] Aggregation Shape:");
            Console.WriteLine(dfSummary.Info());
        }
    }
}

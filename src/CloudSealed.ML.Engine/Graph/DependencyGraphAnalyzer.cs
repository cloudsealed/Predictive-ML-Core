namespace CloudSealed.ML.Engine.Graph;

using System;
using System.Collections.Generic;

public readonly record struct Edge(int SourceIndex, int TargetIndex, float CouplingWeight);

public sealed class DependencyGraphAnalyzer
{
    private readonly int _nodeCount;
    private readonly List<Edge> _edges;

    public DependencyGraphAnalyzer(int nodeCount)
    {
        _nodeCount = nodeCount;
        _edges = new List<Edge>();
    }

    public void AddDependency(int sourceIndex, int targetIndex, float couplingWeight = 1.0f)
    {
        if (sourceIndex >= _nodeCount || targetIndex >= _nodeCount)
            throw new ArgumentOutOfRangeException("Índice de nó inválido no grafo de dependência.");

        _edges.Add(new Edge(sourceIndex, targetIndex, couplingWeight));
    }

    /// <summary>
    /// Calcula a Centralidade e o Risco de Queda em Cascata (Blast Radius)
    /// Sem alocações dinâmicas repetitivas, operando sobre spans pré-alocados.
    /// </summary>
    public void ComputeBlastRadius(Span<float> outBlastRadiusScores, Span<int> outInDegrees)
    {
        outBlastRadiusScores.Clear();
        outInDegrees.Clear();

        ReadOnlySpan<Edge> edgeSpan = _edges.ToArray();

        // 1. Acumulação de dependências diretas (in-degree e fan-in)
        for (int i = 0; i < edgeSpan.Length; i++)
        {
            ref readonly var edge = ref edgeSpan[i];
            outInDegrees[edge.TargetIndex]++;
            outBlastRadiusScores[edge.TargetIndex] += (15.0f * edge.CouplingWeight);
        }

        // 2. Normalização e teto determinístico
        for (int i = 0; i < _nodeCount; i++)
        {
            if (outInDegrees[i] >= 3)
            {
                outBlastRadiusScores[i] += 25.0f; // Penalidade por gargalo de convergência crítica
            }
            outBlastRadiusScores[i] = MathF.Min(outBlastRadiusScores[i], 100.0f);
        }
    }
}

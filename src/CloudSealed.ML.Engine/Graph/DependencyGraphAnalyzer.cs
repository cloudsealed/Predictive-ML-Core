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
            throw new ArgumentOutOfRangeException("Invalid node index in the dependency graph.");

        _edges.Add(new Edge(sourceIndex, targetIndex, couplingWeight));
    }

    /// <summary>
    /// Computes Centrality and Cascade Failure Risk (Blast Radius)
    /// without repetitive dynamic allocations, operating on pre-allocated spans.
    /// </summary>
    public void ComputeBlastRadius(Span<float> outBlastRadiusScores, Span<int> outInDegrees)
    {
        outBlastRadiusScores.Clear();
        outInDegrees.Clear();

        ReadOnlySpan<Edge> edgeSpan = _edges.ToArray();

        // 1. Accumulation of direct dependencies (in-degree and fan-in)
        for (int i = 0; i < edgeSpan.Length; i++)
        {
            ref readonly var edge = ref edgeSpan[i];
            outInDegrees[edge.TargetIndex]++;
            outBlastRadiusScores[edge.TargetIndex] += (15.0f * edge.CouplingWeight);
        }

        // 2. Normalisation and deterministic cap
        for (int i = 0; i < _nodeCount; i++)
        {
            if (outInDegrees[i] >= 3)
            {
                outBlastRadiusScores[i] += 25.0f; // Penalty for critical convergence bottleneck
            }
            outBlastRadiusScores[i] = MathF.Min(outBlastRadiusScores[i], 100.0f);
        }
    }
}

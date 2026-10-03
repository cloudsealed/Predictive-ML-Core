namespace CloudSealed.ML.Engine.Probes;

using System;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

public ref struct ZeroAllocRiskProbe
{
    private readonly ReadOnlySpan<float> _recentLatenciesMs;
    private readonly float _p99ThresholdMs;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ZeroAllocRiskProbe(ReadOnlySpan<float> recentLatenciesMs, float p99ThresholdMs = 1000.0f)
    {
        _recentLatenciesMs = recentLatenciesMs;
        _p99ThresholdMs = p99ThresholdMs;
    }

    /// <summary>
    /// Evaluates p99 violation via SIMD with zero heap allocation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public readonly int CalculateTailViolationScore()
    {
        if (_recentLatenciesMs.IsEmpty)
            return 0;

        int violations = 0;
        int i = 0;

        if (Avx2.IsSupported && _recentLatenciesMs.Length >= 8)
        {
            var threshVector = Vector256.Create(_p99ThresholdMs);
            for (; i <= _recentLatenciesMs.Length - 8; i += 8)
            {
                var latencyVector = Vector256.Create(_recentLatenciesMs.Slice(i, 8));
                var cmp = Avx2.Compare(latencyVector, threshVector, FloatComparisonMode.OrderedGreaterThanOrEqualNonSignaling);
                int mask = Avx2.MoveMask(cmp);
                violations += System.Numerics.BitOperations.PopCount((uint)mask);
            }
        }

        // Process scalar remainders
        for (; i < _recentLatenciesMs.Length; i++)
        {
            if (_recentLatenciesMs[i] >= _p99ThresholdMs)
                violations++;
        }

        float violationRatio = (float)violations / _recentLatenciesMs.Length;
        return (int)MathF.Min(100.0f, violationRatio * 150.0f);
    }
}

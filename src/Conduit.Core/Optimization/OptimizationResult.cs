using Conduit.Core.Heuristics;
using Conduit.Core.Stress;

namespace Conduit.Core.Optimization;

/// <summary>The outcome of <see cref="OptimizationLoop.Run"/>: whether the model passed, and a trail of what was done.</summary>
public sealed record OptimizationResult(
    bool Passed,
    int Iterations,
    StressResult FinalStressResult,
    IReadOnlyList<PlacedSupport> InitialPlacements,
    IReadOnlyList<string> Notes);

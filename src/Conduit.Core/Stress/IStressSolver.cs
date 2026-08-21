using Conduit.Core.NeutralFiles;

namespace Conduit.Core.Stress;

/// <summary>
/// Checks whether a neutral file's current support configuration is acceptable. v1's only
/// functional implementation, <see cref="MockStressSolver"/>, is a deterministic span/utilisation
/// proxy — not a code-compliance check. A real implementation, <see cref="CaesarComStressSolver"/>,
/// is a skeleton only; see its XML docs for the plan (drive CAESAR II via COM, read results back
/// from an exported ASCII report) and SPEC.md's "Caesar II abstraction" section for the full
/// reasoning.
/// </summary>
public interface IStressSolver
{
    StressResult Evaluate(NeutralFile file);
}

using Conduit.Core.NeutralFiles;

namespace Conduit.Core.Stress;

/// <summary>
/// Unimplemented skeleton for a real CAESAR II-backed <see cref="IStressSolver"/>. Not wired up
/// or tested in this repo — CAESAR II's COM automation and GUI are Windows-only and unavailable
/// in this project's headless Linux build/test environment. Intended to be completed and
/// validated later on a Windows machine with a licensed CAESAR II install.
///
/// <para><b>Planned implementation</b> (see SPEC.md's "Caesar II abstraction" for the full
/// reasoning behind this plan):</para>
/// <list type="number">
/// <item>Drive CAESAR II via COM: load the neutral file (after converting through
/// <c>INeutralFileConverter</c> if the source was <c>.C2</c>/<c>._A</c>), error-check, then run
/// static analysis — the "Batch Run" action (error check + analyze + generate results in one
/// step).</item>
/// <item>Have it emit a Code Compliance Report (stress ratios) and a Restraints/Restraint
/// Summary Report (support loads) to plain ASCII text files — ideally via a custom Report
/// Template authored once for a stable, known column layout, rather than pulling values through
/// interactive COM calls one field at a time.</item>
/// <item>Parse those text files into a <see cref="StressResult"/>. The real Code Compliance
/// Report shape is richer than v1's simplified pass/fail — per load case, per element, it
/// reports Code Stress, Allowable Stress, and Ratio % — so a real implementation should target
/// that ratio-based shape, not just a boolean.</item>
/// </list>
/// </summary>
public sealed class CaesarComStressSolver : IStressSolver
{
    public StressResult Evaluate(NeutralFile file) =>
        throw new NotImplementedException(
            "CaesarComStressSolver requires a licensed CAESAR II install and Windows COM automation, " +
            "neither available in this build environment. See this class's XML docs and SPEC.md's " +
            "\"Caesar II abstraction\" section for the planned implementation.");
}

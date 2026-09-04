using Conduit.Core.NeutralFiles;

namespace Conduit.Core.Heuristics;

/// <summary>
/// Maps a semantic <see cref="SupportType"/> to the neutral-file <see cref="RestraintType"/>
/// code that expresses it. A hold-down is never placed standalone — per direct instruction
/// (2026-09-04): "There should not be standalone hold-downs." <see cref="SupportType.Rest"/> maps
/// to the bidirectional <c>Y</c>/<c>Z</c> directly, per direct instruction (2026-09-03): "we will
/// start by placing hold-downs together with rest supports on the initial pass. Unless we
/// determine that the stresses are not passing for the placement... this can be done by setting Y
/// instead of +Y." So every rest <see cref="Heuristics.SupportPlacer"/>/
/// <see cref="Optimization.OptimizationLoop"/> place is a combined rest+hold-down by default (a
/// real accidental-blast-scenario need, per the same instruction) — a real stress check narrows a
/// specific one back down to a plain one-directional rest (<c>+Y</c>/<c>+Z</c>) when the
/// hold-down would over-restrain the pipe's own expansion, never to a hold-down on its own. That
/// stress check ("we need to derive the logic for the forces and stresses together," per the same
/// 2026-09-04 instruction — a single coupled sustained/expansion-stress model, not independent
/// heuristics) is scoped but not yet built; see QUESTIONS.md's "Scoping proposal... beam/
/// expansion-stress model" entry.
/// </summary>
public static class RestraintTypeMapper
{
    /// <param name="izup">The model's vertical-axis flag from <c>#$ CONTROL</c> (0 = -Y vertical, 1 = -Z vertical).</param>
    public static RestraintType Map(SupportType type, int izup) => type switch
    {
        SupportType.Rest => izup == 0 ? RestraintType.Y : RestraintType.Z,
        SupportType.Guide => RestraintType.Gui,
        SupportType.LineStop => RestraintType.Lim,
        SupportType.Anchor => RestraintType.Anc,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown support type."),
    };
}

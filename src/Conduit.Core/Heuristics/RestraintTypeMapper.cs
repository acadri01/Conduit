using Conduit.Core.NeutralFiles;

namespace Conduit.Core.Heuristics;

/// <summary>
/// Maps a semantic <see cref="SupportType"/> to the neutral-file <see cref="RestraintType"/>
/// code that expresses it. The taxonomy itself (per review) is that a rest is properly
/// one-directional (<c>+Y</c>, allowing lift-off) and a hold-down is the opposite one-directional
/// restraint (<c>-Y</c>) — bidirectional <c>Y</c> is what a rest and a hold-down together amount
/// to. <see cref="SupportType.Rest"/> maps to the bidirectional <c>Y</c>/<c>Z</c> directly,
/// though, per direct instruction (2026-09-03): "we will start by placing hold-downs together
/// with rest supports on the initial pass. Unless we determine that the stresses are not passing
/// for the placement... this can be done by setting Y instead of +Y." So every rest
/// <see cref="Heuristics.SupportPlacer"/>/<see cref="Optimization.OptimizationLoop"/> place is a
/// combined rest+hold-down by default (a real accidental-blast-scenario need, per the same
/// instruction) until a real stress check justifies narrowing a specific one back to a plain
/// one-directional rest — <see cref="SupportType.HoldDown"/> itself stays mapped to the
/// one-directional <c>-Y</c>/<c>-Z</c>, for whenever that narrower case is actually needed.
/// </summary>
public static class RestraintTypeMapper
{
    /// <param name="izup">The model's vertical-axis flag from <c>#$ CONTROL</c> (0 = -Y vertical, 1 = -Z vertical).</param>
    public static RestraintType Map(SupportType type, int izup) => type switch
    {
        SupportType.Rest => izup == 0 ? RestraintType.Y : RestraintType.Z,
        SupportType.HoldDown => izup == 0 ? RestraintType.MinusY : RestraintType.MinusZ,
        SupportType.Guide => RestraintType.Gui,
        SupportType.LineStop => RestraintType.Lim,
        SupportType.Anchor => RestraintType.Anc,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown support type."),
    };
}

using Conduit.Core.NeutralFiles;

namespace Conduit.Core.Heuristics;

/// <summary>
/// Maps a semantic <see cref="SupportType"/> to the neutral-file <see cref="RestraintType"/>
/// code that expresses it, per the corrected taxonomy from review: a rest is one-directional
/// (<c>+Y</c>, allowing lift-off), not the bidirectional <c>Y</c> — bidirectional <c>Y</c> is
/// what you get from a rest *and* a hold-down together.
/// </summary>
public static class RestraintTypeMapper
{
    /// <param name="izup">The model's vertical-axis flag from <c>#$ CONTROL</c> (0 = -Y vertical, 1 = -Z vertical).</param>
    public static RestraintType Map(SupportType type, int izup) => type switch
    {
        SupportType.Rest => izup == 0 ? RestraintType.PlusY : RestraintType.PlusZ,
        SupportType.HoldDown => izup == 0 ? RestraintType.MinusY : RestraintType.MinusZ,
        SupportType.Guide => RestraintType.Gui,
        SupportType.LineStop => RestraintType.Lim,
        SupportType.Anchor => RestraintType.Anc,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown support type."),
    };
}

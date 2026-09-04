namespace Conduit.Core.Heuristics;

/// <summary>
/// The semantic kind of support v1's heuristic assigns to a candidate location. Mapped to an
/// actual <see cref="Conduit.Core.NeutralFiles.RestraintType"/> by <see cref="RestraintTypeMapper"/>
/// (which also knows the model's vertical axis), so this type stays about engineering intent
/// rather than neutral-file mechanics.
///
/// <para>The real taxonomy (per review) is rest, hold-down, guide, and line stop, with an anchor
/// being their combination (equivalently: the single <c>ANC</c> restraint code, or <c>Y</c> +
/// <c>GUIDE</c> + <c>LIM</c> together). <see cref="HoldDown"/> and <see cref="LineStop"/> are
/// included here for a complete vocabulary, but v1's <c>SupportTypeClassifier</c> doesn't
/// currently produce them — it only ever assigns <see cref="Rest"/>, <see cref="Guide"/>, or
/// <see cref="Anchor"/> — since it has no signal yet for when a hold-down or line stop is needed
/// specifically (that needs the loads/travel data a real stress check would provide).</para>
/// </summary>
public enum SupportType
{
    /// <summary>
    /// A vertical rest — resists sagging under gravity. Properly a one-directional restraint
    /// (CAESAR II's <c>+Y</c>, allowing the pipe to lift off under e.g. thermal growth), but
    /// <see cref="RestraintTypeMapper"/> maps it to the bidirectional <c>Y</c> by default as of
    /// direct instruction (2026-09-03) — Conduit's own rests are placed with a hold-down bundled
    /// in from the start, not as a plain one-directional rest, until a real stress check justifies
    /// narrowing a specific one back down.
    /// </summary>
    Rest,

    /// <summary>A one-directional restraint against lifting off (the opposite of <see cref="Rest"/>) — combined with a rest, this is CAESAR II's bidirectional <c>Y</c>.</summary>
    HoldDown,

    /// <summary>A guide — restrains lateral movement, allows axial travel. Used on vertical runs.</summary>
    Guide,

    /// <summary>A limit/line stop — restrains travel beyond a defined gap, rather than fully rigid restraint. CAESAR II's <c>LIM</c>.</summary>
    LineStop,

    /// <summary>An anchor — the combination of rest, hold-down, guide, and line stop; used near equipment/nozzle connections and major direction changes.</summary>
    Anchor,
}

namespace Conduit.Core.Heuristics;

/// <summary>
/// The semantic kind of support v1's heuristic assigns to a candidate location. Mapped to an
/// actual <see cref="Conduit.Core.NeutralFiles.RestraintType"/> by <c>SupportPlacer</c> (which
/// also knows the model's vertical axis), so this type stays about engineering intent rather
/// than neutral-file mechanics.
/// </summary>
public enum SupportType
{
    /// <summary>A simple vertical (gravity) rest support.</summary>
    Rest,

    /// <summary>A guide — restrains lateral movement, allows axial travel. Used on vertical runs.</summary>
    Guide,

    /// <summary>An anchor — used near equipment/nozzle connections and major direction changes.</summary>
    Anchor,

    /// <summary>
    /// Flags the location as a candidate for a spring (variable-support) hanger, because the
    /// span-to-limit ratio suggests thermal growth may lift the pipe off a rigid rest support.
    /// v1 only flags this — actual spring sizing (load, travel, catalog selection) is out of scope.
    /// </summary>
    SpringCandidate,
}

namespace Conduit.Core.NeutralFiles;

/// <summary>
/// A parsed CAESAR II neutral file (<c>.cii</c>). <see cref="Blocks"/> is the source of truth for
/// writing — every block is written back verbatim except <c>CONTROL</c> and <c>RESTRANT</c>,
/// which are regenerated from <see cref="Control"/> and <see cref="Restraints"/> so a file
/// round-tripped without any support changes is byte-identical, and one with only new supports
/// preserves everything else CAESAR II needs to re-import it.
/// </summary>
public sealed class NeutralFile
{
    public required List<NeutralFileBlock> Blocks { get; init; }
    public required ControlSection Control { get; init; }
    public required IReadOnlyList<Element> Elements { get; init; }
    public required IReadOnlyList<NodeName> NodeNames { get; init; }
    public required List<Restraint> Restraints { get; init; }

    /// <summary>
    /// Appends a new support and keeps <see cref="Control"/>'s restraint count in sync so the
    /// regenerated <c>#$ CONTROL</c> section matches the regenerated <c>#$ RESTRANT</c> section.
    /// </summary>
    public void AddRestraint(Restraint restraint)
    {
        Restraints.Add(restraint);
        Control.NumRestraints = Restraints.Count;
    }

    /// <summary>
    /// Computes each node's position by walking <see cref="Elements"/> in order and accumulating
    /// delta coordinates. Conduit doesn't parse the optional <c>#$ COORDS</c> section, so each
    /// disconnected chain's first FROM node is treated as the origin — fine for the span/length
    /// calculations v1's heuristics need, which only depend on relative geometry along a run.
    /// </summary>
    public Dictionary<int, (double X, double Y, double Z)> ComputeNodePositions()
    {
        var positions = new Dictionary<int, (double X, double Y, double Z)>();
        foreach (var element in Elements)
        {
            if (!positions.ContainsKey(element.FromNode))
            {
                positions[element.FromNode] = (0, 0, 0);
            }
            var from = positions[element.FromNode];
            positions[element.ToNode] = (from.X + element.DeltaX, from.Y + element.DeltaY, from.Z + element.DeltaZ);
        }
        return positions;
    }
}

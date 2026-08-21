namespace Conduit.Core.NeutralFiles;

/// <summary>
/// One record from <c>#$ RESTRANT</c> — a support at a node. Always exactly
/// <see cref="DofsPerRestraint"/> DOF slots (see <see cref="RestraintDof"/>), whether or not
/// all of them are used. This is the section Conduit actively edits: new supports are appended
/// as new <see cref="Restraint"/> records, and the whole section is regenerated on write.
/// </summary>
public sealed class Restraint
{
    public const int DofsPerRestraint = 6;
    public const int LinesPerDof = 4; // 2 data lines (9 values) + tag line + GUID line
    public const int LinesPerRestraint = DofsPerRestraint * LinesPerDof;

    public required List<RestraintDof> Dofs { get; init; }

    /// <summary>The node this restraint is defined at (from its first used DOF slot), or 0 if none are used.</summary>
    public int Node => Dofs.FirstOrDefault(d => d.IsUsed)?.Node ?? 0;

    public static Restraint CreateEmpty() =>
        new() { Dofs = Enumerable.Range(0, DofsPerRestraint).Select(_ => new RestraintDof()).ToList() };

    /// <summary>Creates a single-DOF restraint (the shape v1's placement heuristic emits).</summary>
    public static Restraint CreateSingleDof(int node, RestraintType type)
    {
        var restraint = CreateEmpty();
        restraint.Dofs[0].Node = node;
        restraint.Dofs[0].RawTypeCode = (int)type;
        return restraint;
    }

    public static List<Restraint> ParseMany(IReadOnlyList<string> lines, int count)
    {
        var restraints = new List<Restraint>(count);
        var lineIndex = 0;
        for (var i = 0; i < count; i++)
        {
            var dofs = new List<RestraintDof>(DofsPerRestraint);
            for (var d = 0; d < DofsPerRestraint; d++)
            {
                dofs.Add(RestraintDof.Parse(lines, ref lineIndex));
            }
            restraints.Add(new Restraint { Dofs = dofs });
        }
        return restraints;
    }

    public IEnumerable<string> ToRawLines() => Dofs.SelectMany(dof => dof.ToRawLines());
}

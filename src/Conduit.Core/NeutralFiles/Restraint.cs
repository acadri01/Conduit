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

    /// <summary>
    /// Creates a single-DOF restraint (the shape v1's placement heuristic emits), rigid rather
    /// than a zero-stiffness no-op — see <paramref name="rigidStiffness"/>. Also sets the
    /// direction cosine for restraint types whose axis is unambiguous from the type code itself
    /// (X/Y/Z and their signed/rod/snubber variants) — confirmed against every such restraint in
    /// <c>fixtures/real-samples/44002.cii</c>, all of which carry the matching axis's direction
    /// cosine even though the type code alone already implies it. <see cref="RestraintType.Anc"/>
    /// and <see cref="RestraintType.Gui"/> are left at (0,0,0): the one real GUI example available
    /// has a non-zero, seemingly direction-specific cosine, but a single sample isn't enough to
    /// confirm the general rule for a plain (non-directional) guide — logged in QUESTIONS.md as an
    /// open question rather than guessed at further, since it's a placement-logic question CLAUDE.md
    /// reserves for direct consultation. ANC's own real example is (0,0,0), so this default is
    /// confirmed correct for anchors at least.
    /// </summary>
    public static Restraint CreateSingleDof(int node, RestraintType type, double rigidStiffness)
    {
        var restraint = CreateEmpty();
        var dof = restraint.Dofs[0];
        dof.Node = node;
        dof.RawTypeCode = (int)type;
        dof.Stiffness = rigidStiffness;
        (dof.DirectionCosineX, dof.DirectionCosineY, dof.DirectionCosineZ) = DirectionCosineFor(type);
        return restraint;
    }

    /// <summary>
    /// Creates one restraint record carrying several DOF types at the same node — e.g. a rest and
    /// a guide together, matching how real files pack multiple restraint types into a single
    /// record at one node rather than several separate records (confirmed against
    /// <c>fixtures/real-samples/44002.cii</c>, e.g. node 175's <c>Y, Lim, Gui</c> all in one
    /// record). <paramref name="types"/> must have at most <see cref="DofsPerRestraint"/> entries.
    /// </summary>
    public static Restraint CreateMultiDof(int node, IReadOnlyList<RestraintType> types, double rigidStiffness)
    {
        if (types.Count == 1)
        {
            return CreateSingleDof(node, types[0], rigidStiffness);
        }

        var restraint = CreateEmpty();
        for (var i = 0; i < types.Count; i++)
        {
            var dof = restraint.Dofs[i];
            dof.Node = node;
            dof.RawTypeCode = (int)types[i];
            dof.Stiffness = rigidStiffness;
            (dof.DirectionCosineX, dof.DirectionCosineY, dof.DirectionCosineZ) = DirectionCosineFor(types[i]);
        }
        return restraint;
    }

    private static (double X, double Y, double Z) DirectionCosineFor(RestraintType type) => type switch
    {
        RestraintType.X or RestraintType.PlusX or RestraintType.MinusX
            or RestraintType.Xsnb or RestraintType.PlusXsnb or RestraintType.MinusXsnb
            or RestraintType.Xrod or RestraintType.PlusXrod or RestraintType.MinusXrod
            or RestraintType.X2 or RestraintType.PlusX2 or RestraintType.MinusX2 => (1, 0, 0),
        RestraintType.Y or RestraintType.PlusY or RestraintType.MinusY
            or RestraintType.Ysnb or RestraintType.PlusYsnb or RestraintType.MinusYsnb
            or RestraintType.Yrod or RestraintType.PlusYrod or RestraintType.MinusYrod
            or RestraintType.Y2 or RestraintType.PlusY2 or RestraintType.MinusY2 => (0, 1, 0),
        RestraintType.Z or RestraintType.PlusZ or RestraintType.MinusZ
            or RestraintType.Zsnb or RestraintType.PlusZsnb or RestraintType.MinusZsnb
            or RestraintType.Zrod or RestraintType.PlusZrod or RestraintType.MinusZrod
            or RestraintType.Z2 or RestraintType.PlusZ2 or RestraintType.MinusZ2 => (0, 0, 1),
        _ => (0, 0, 0),
    };

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

namespace Conduit.Core.NeutralFiles;

/// <summary>
/// The <c>#$ CONTROL</c> section: element/aux-data-type counts. Conduit needs
/// <see cref="NumElements"/> to chop the <c>#$ ELEMENTS</c> block into per-element records,
/// and <see cref="NumRestraints"/> to chop <c>#$ RESTRANT</c> into per-restraint records
/// (and to bump when new supports are added) — every other count is informational only, kept
/// so the section round-trips exactly when unmodified.
/// </summary>
public sealed class ControlSection
{
    public required int NumElements { get; set; }
    public required int NumNozzles { get; set; }
    public required int NumHangers { get; set; }
    public required int NumNodeNames { get; set; }
    public required int NumReducers { get; set; }
    public required int NumFlanges { get; set; }

    public required int NumBends { get; set; }
    public required int NumRigids { get; set; }
    public required int NumExpansionJoints { get; set; }
    public required int NumRestraints { get; set; }
    public required int NumDisplacements { get; set; }
    public required int NumForceMoments { get; set; }
    public required int NumUniformLoads { get; set; }
    public required int NumWindLoads { get; set; }
    public required int NumOffsets { get; set; }
    public required int NumAllowableStress { get; set; }
    public required int NumIntersections { get; set; }

    /// <summary>0 = global -Y axis vertical; 1 = global -Z axis vertical.</summary>
    public required int Izup { get; set; }

    public required int NumEquipmentChecks { get; set; }

    public static ControlSection Parse(NeutralFileBlock block)
    {
        var lines = block.RawLines;
        var lineIndex = 0;
        var head = FixedWidth.ParseInts(lines, ref lineIndex, 6);
        var aux = FixedWidth.ParseInts(lines, ref lineIndex, 13);

        return new ControlSection
        {
            NumElements = (int)head[0],
            NumNozzles = (int)head[1],
            NumHangers = (int)head[2],
            NumNodeNames = (int)head[3],
            NumReducers = (int)head[4],
            NumFlanges = (int)head[5],

            NumBends = (int)aux[0],
            NumRigids = (int)aux[1],
            NumExpansionJoints = (int)aux[2],
            NumRestraints = (int)aux[3],
            NumDisplacements = (int)aux[4],
            NumForceMoments = (int)aux[5],
            NumUniformLoads = (int)aux[6],
            NumWindLoads = (int)aux[7],
            NumOffsets = (int)aux[8],
            NumAllowableStress = (int)aux[9],
            NumIntersections = (int)aux[10],
            Izup = (int)aux[11],
            NumEquipmentChecks = (int)aux[12],
        };
    }

    /// <summary>Regenerates <paramref name="block"/>'s raw lines from this section's current values.</summary>
    public void WriteBackTo(NeutralFileBlock block)
    {
        var head = new List<long> { NumElements, NumNozzles, NumHangers, NumNodeNames, NumReducers, NumFlanges };
        var aux = new List<long>
        {
            NumBends, NumRigids, NumExpansionJoints, NumRestraints, NumDisplacements, NumForceMoments,
            NumUniformLoads, NumWindLoads, NumOffsets, NumAllowableStress, NumIntersections, Izup, NumEquipmentChecks,
        };

        block.RawLines.Clear();
        block.RawLines.AddRange(FixedWidth.FormatIntLines(head));
        block.RawLines.AddRange(FixedWidth.FormatIntLines(aux));
    }
}

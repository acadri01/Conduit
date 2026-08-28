using Conduit.Core.Heuristics;
using Conduit.Core.NeutralFiles;

namespace Conduit.Tests.TestHelpers;

/// <summary>
/// Builds small, synthetic, structurally-valid <see cref="NeutralFile"/> models for tests and
/// for the committed fixtures under <c>fixtures/</c> — fictitious node numbers and geometry,
/// not derived from any real project (see SPEC.md's clean-room constraint).
/// </summary>
public static class NeutralFileFixtureBuilder
{
    public sealed record PipeSegmentSpec(
        int FromNode,
        int ToNode,
        double DeltaX,
        double DeltaY,
        double DeltaZ,
        double OutsideDiameter,
        double WallThickness,
        double PipeDensity);

    /// <summary>A 6" Sch 40 carbon-steel segment of the given length (mm) along the X axis.</summary>
    public static PipeSegmentSpec Schedule40Run(int fromNode, int toNode, double length) =>
        new(fromNode, toNode, DeltaX: length, DeltaY: 0, DeltaZ: 0, OutsideDiameter: 168.3, WallThickness: 7.11, PipeDensity: SpanLimitCalculator.DefaultSteelDensityKgPerM3);

    /// <summary>A 6" Sch 40 carbon-steel vertical riser segment of the given length (mm, Y-up).</summary>
    public static PipeSegmentSpec Schedule40Riser(int fromNode, int toNode, double length) =>
        new(fromNode, toNode, DeltaX: 0, DeltaY: length, DeltaZ: 0, OutsideDiameter: 168.3, WallThickness: 7.11, PipeDensity: SpanLimitCalculator.DefaultSteelDensityKgPerM3);

    /// <summary>Builds a standalone <see cref="Element"/> from a spec, for tests that need one without a whole file.</summary>
    public static Element ToElement(this PipeSegmentSpec segment) =>
        new() { RealValues = BuildRealValues(segment), AuxiliaryPointers = new int[15] };

    private static double[] BuildRealValues(PipeSegmentSpec segment)
    {
        var real = new double[53];
        real[0] = segment.FromNode;
        real[1] = segment.ToNode;
        real[2] = segment.DeltaX;
        real[3] = segment.DeltaY;
        real[4] = segment.DeltaZ;
        real[5] = segment.OutsideDiameter;
        real[6] = segment.WallThickness;
        real[29] = segment.PipeDensity;
        return real;
    }

    public static NeutralFile Build(
        IReadOnlyList<PipeSegmentSpec> segments,
        IReadOnlyList<int> anchorNodes,
        int izup = 0,
        IReadOnlyList<int>? bendNodes = null)
    {
        bendNodes ??= [];
        var blocks = new List<NeutralFileBlock>();

        NeutralFileBlock AddBlock(string name, IEnumerable<string>? lines = null)
        {
            var block = new NeutralFileBlock { Name = name, HeaderLine = $"#$ {name}", RawLines = (lines ?? []).ToList() };
            blocks.Add(block);
            return block;
        }

        AddBlock("VERSION", BuildVersionLines());
        AddBlock("CONTROL");
        var elementsBlock = AddBlock("ELEMENTS", BuildElementLines(segments, bendNodes, anchorNodes));
        AddBlock("AUX_DATA");
        AddBlock("BEND", BuildBendLines(segments, bendNodes));
        AddBlock("RIGID");
        AddBlock("EXPJT");
        AddBlock("RESTRANT");
        AddBlock("DISPLMNT");
        AddBlock("FORCMNT");
        AddBlock("UNIFORM");
        AddBlock("WIND"); // empty: NumWindLoads=0 (no wind load modeled) — see BuildWindLines' doc comment
        AddBlock("OFFSETS");
        var allowblsBlock = AddBlock("ALLOWBLS", BuildAllowblsLines());
        AddBlock("SIF&TEES");
        AddBlock("REDUCERS");
        AddBlock("FLANGES");
        AddBlock("EQUIPMNT");
        AddBlock("MISCEL_1", BuildMiscel1Lines(segments.Count));
        var unitsBlock = AddBlock("UNITS", BuildUnitsLines());
        AddBlock("COORDS", BuildCoordsLines());

        var control = new ControlSection
        {
            NumElements = segments.Count,
            NumNozzles = 0,
            NumHangers = 0,
            NumNodeNames = 0,
            NumReducers = 0,
            NumFlanges = 0,
            NumBends = bendNodes.Count,
            NumRigids = 0,
            NumExpansionJoints = 0,
            NumRestraints = anchorNodes.Count,
            NumDisplacements = 0,
            NumForceMoments = 0,
            NumUniformLoads = 0,
            NumWindLoads = 0,
            NumOffsets = 0,
            NumAllowableStress = 1,
            NumIntersections = 0,
            Izup = izup,
            NumEquipmentChecks = 0,
        };

        var elements = Element.ParseMany(elementsBlock.RawLines, 0, segments.Count);
        var units = UnitsSection.Parse(unitsBlock);

        var restraints = anchorNodes
            .Select(node => Restraint.CreateSingleDof(node, RestraintType.Anc, units.RigidRestraintStiffness))
            .ToList();

        var file = new NeutralFile
        {
            Blocks = blocks,
            Control = control,
            Elements = elements,
            NodeNames = [],
            Restraints = restraints,
            MaterialIds = segments.Select(_ => 1).ToList(),
            AllowableStresses = AllowableStress.ParseMany(allowblsBlock.RawLines, 1),
            NozzleLimits = [],
            Units = units,
        };

        // Regenerate CONTROL/RESTRANT raw lines from the model right away, so the returned
        // file's Blocks are already consistent (NeutralFileWriter does this again on write,
        // harmlessly, since it's idempotent).
        NeutralFileWriter.ToLines(file);

        return file;
    }

    /// <summary>
    /// <c>#$ VERSION</c>'s info line (already correct) plus the 60 fixed 75-char title-page lines
    /// <c>NeutralFile-v15.pdf</c> requires (FORTRAN <c>(2X, A75)</c>) — confirmed against a real
    /// sample's byte length (61 total lines before <c>#$ CONTROL</c>). Previously this block was
    /// just the info line with zero title lines, which shifts every section after it by 60 lines;
    /// a strong candidate for a "line # NN" parse error on any file built from this fixture.
    /// </summary>
    private static List<string> BuildVersionLines()
    {
        var lines = new List<string> { "    5.00000      15.0000        1252" };
        for (var i = 0; i < 59; i++)
        {
            lines.Add(FixedWidth.FormatFixedWidthText(string.Empty, 75));
        }
        lines.Add(FixedWidth.FormatFixedWidthText("Data generated by Conduit (synthetic test fixture)", 75));
        return lines;
    }

    /// <summary>
    /// <c>#$ COORDS</c> lists the start coordinate of each *discontinuous* piping segment
    /// (<c>NeutralFile-v15.pdf</c>) — always at least an <c>NXYZ</c> count line, per a real
    /// sample. This builder's elements always form one contiguous chain per run, so there are
    /// never any discontinuities to list: just the zero count.
    /// </summary>
    private static List<string> BuildCoordsLines() =>
        FixedWidth.FormatIntLines([0]).ToList();

    /// <summary>
    /// <c>#$ UNITS</c>'s 22 conversion constants (FORTRAN <c>(2X, 6G13.6)</c>, 4 lines) and 24
    /// unit labels (FORTRAN <c>(2X, A&lt;n&gt;)</c> per <c>NeutralFile-v15.pdf</c>'s field list),
    /// never empty in a real file. The constants and unit labels below (mm/N/kg-based, MPa
    /// stress, etc.) are confirmed byte-identical across 4 unrelated real samples — ordinary
    /// physical conversion factors (25.4 mm/in, 4.448 N/lbf, 0.4536 kg/lbm, ...), not project- or
    /// company-specific data — except <c>CCVNAME</c> itself, which those samples set to a
    /// company-specific unit-system name; this builder uses the generic "Metric (mm)" instead
    /// (per direct instruction — pending the user confirming CAESAR II's own preset name for it).
    /// </summary>
    private static List<string> BuildUnitsLines()
    {
        double[] constants =
        [
            25.4, 4.448, 0.4536, 0.11298, 0.11298, 0.006895,
            0.5556, -17.77778, 0.068946, 0.006895, 27680.0, 27680.0,
            27680.0, 0.17512, 0.11298, 175.12, 1.0, 0.068946,
            0.0254, 25.4, 25.4, 25.4,
        ];
        (string Label, int Width)[] labels =
        [
            ("Metric (mm)", 15), ("ON", 3), ("mm", 3), ("N", 3), ("kg", 3), ("Nm", 6),
            ("Nm", 6), ("MPa", 10), ("C", 1), ("C", 1), ("bar", 10), ("MPa", 10),
            ("kg/m3", 10), ("kg/m3", 10), ("kg/m3", 10), ("N/mm", 7), ("Nm/deg", 10), ("N/m", 7),
            ("g's", 3), ("bar", 10), ("m", 3), ("mm", 3), ("mm", 3), ("mm", 3),
        ];

        var lines = FixedWidth.FormatRealLines(constants).ToList();
        lines.AddRange(labels.Select(l => FixedWidth.FormatFixedWidthText(l.Label, l.Width)));
        return lines;
    }

    /// <summary>
    /// <c>#$ ALLOWBLS</c>: one real, sourced allowable-stress record — ASTM A106 Grade B's cold/
    /// ambient allowable stress (118 MPa), read directly from the user's own CAESAR II material
    /// database (<c>UMAT1.umd</c>, material #107) — per direct instruction to reference a real,
    /// complete material rather than CAESAR's generic "LOW CARBON" entry (material #1), which
    /// turns out to carry no allowable/yield/UTS data at all. Only item 1 (cold allowable stress,
    /// <see cref="AllowableStress.ColdAllowableStress"/>) is populated — the only field Conduit's
    /// own model actually reads; the other 167 fields (hot allowables, fatigue curves, code-
    /// specific items) are left at 0.0 pending future need, same minimalism as every other
    /// section this builder only partially populates.
    /// </summary>
    private static List<string> BuildAllowblsLines()
    {
        var values = new double[168];
        values[0] = SpanLimitCalculator.DefaultAllowableBendingStressMpa;
        return FixedWidth.FormatRealLines(values).ToList();
    }

    /// <summary>
    /// <c>#$ MISCEL_1</c>'s RRMAT array (one material ID per element, packed 6-per-line, FORTRAN
    /// <c>(2X, 6G13.6)</c>) plus a fixed 4-line trailing block — hanger-table defaults and
    /// execution options (<c>NeutralFile-v15.pdf</c>'s "Hangers"/"Execution Options"
    /// subsections) — that's present even with zero hangers/nozzles/execution overrides, unlike
    /// this builder's earlier RRMAT-only version. Omitting it is a confirmed cause of
    /// <c>iecho.exe</c>'s "Error processing MISCEL_1 section, line # NN": the reader expects this
    /// trailing data unconditionally (it isn't gated by any <c>#$ CONTROL</c> count), so leaving
    /// it out desyncs every read after it, surfacing as a parse error in whatever section comes
    /// next (<c>#$ UNITS</c>). The trailing values themselves are confirmed byte-identical
    /// between <c>TESTv15.cii</c> and <c>TESTv15_slugged.cii</c> (both zero hangers/nozzles); a
    /// third real sample (<c>44002.cii</c>, also zero hangers/nozzles) has slightly different
    /// values for a few fields — logged as an open, low-priority question in QUESTIONS.md, same
    /// treatment as <c>#$ UNITS</c>'s <c>CCVNAME</c> placeholder — but the two agreeing samples
    /// are reused here rather than guessing at which fields are safe to vary.
    /// </summary>
    private static List<string> BuildMiscel1Lines(int numElements)
    {
        var lines = FixedWidth.FormatRealLines(Enumerable.Repeat(1.0, numElements).ToList()).ToList();
        lines.AddRange(
        [
            "              1            0            2            2 0.000000E+00            0",
            "              0            0 4.001740E+00 2.159830E+01            0            0",
            "              0            0            0            0 2.500000E-01            3",
            "              3            1",
        ]);
        return lines;
    }

    /// <summary>
    /// Delegates to <see cref="Element.ToRawLines"/> — the same formatting logic
    /// <see cref="NeutralFile.ReplaceElement"/> uses in production — so the two can never
    /// format-drift apart the way the color/visibility line once did.
    /// </summary>
    private static List<string> BuildElementLines(
        IReadOnlyList<PipeSegmentSpec> segments, IReadOnlyList<int> bendNodes, IReadOnlyList<int> anchorNodes)
    {
        var bendNodeList = bendNodes.ToList();

        // Per NeutralFile.AddRestraint's doc comment: the owning element for a restraint at some
        // node is the one whose ToNode is that node — except a run's very first node, which is
        // nobody's ToNode, so it falls back to the one segment whose FromNode is that node
        // (there's exactly one, since these fixtures are always a single contiguous chain).
        var segmentList = segments.ToList();
        var restraintOwnerSegmentIndex = new Dictionary<int, int>();
        for (var i = 0; i < anchorNodes.Count; i++)
        {
            var node = anchorNodes[i];
            var ownerIndex = segmentList.FindLastIndex(s => s.ToNode == node);
            if (ownerIndex < 0)
            {
                ownerIndex = segmentList.FindIndex(s => s.FromNode == node);
            }
            if (ownerIndex >= 0)
            {
                restraintOwnerSegmentIndex[ownerIndex] = i + 1; // 1-based pointer into #$ RESTRANT
            }
        }

        var lines = new List<string>();
        for (var segmentIndex = 0; segmentIndex < segments.Count; segmentIndex++)
        {
            var segment = segments[segmentIndex];
            var pointers = new long[15];
            var bendIndex = bendNodeList.IndexOf(segment.ToNode);
            if (bendIndex >= 0)
            {
                pointers[0] = bendIndex + 1; // 1-based pointer into #$ BEND
            }
            pointers[9] = 1; // every element points at the one #$ ALLOWBLS record (see BuildAllowblsLines)

            if (restraintOwnerSegmentIndex.TryGetValue(segmentIndex, out var restraintIndex))
            {
                pointers[3] = restraintIndex;
            }

            var element = new Element
            {
                RealValues = BuildRealValues(segment),
                AuxiliaryPointers = pointers.Select(p => (int)p).ToArray(),
            };
            lines.AddRange(element.ToRawLines());
        }
        return lines;
    }

    /// <summary>
    /// <c>#$ BEND</c>: 14 values per bend (13 documented items plus an always-zero 14th, "Overlay
    /// Thickness" — confirmed against <c>44002.cii</c>'s 13 real bends, 3 lines each), referenced
    /// by the 1-based <see cref="Element.AuxiliaryPointers"/>[0] of the element whose <c>ToNode</c>
    /// is the bend's corner node. "Node position #1/#2" reference CAESAR II's own
    /// auto-generated near/far tangent-point node numbers, which never appear as real
    /// <c>FromNode</c>/<c>ToNode</c> values anywhere else in the file — confirmed against
    /// <c>44002.cii</c>, where every bend's tangent nodes are exactly (corner - 1, corner - 2)
    /// and don't appear anywhere in <c>#$ ELEMENTS</c>; reused here for the same convention.
    /// Radius defaults to "Long" (<see cref="ElementSplitter.LongRadiusToOutsideDiameterFactor"/>
    /// x the bend's own element's outside diameter), per direct instruction — CAESAR II's own
    /// bend-radius preset dropdown offers Short/Long/3D/5D, all of which just resolve to a plain
    /// radius number in the neutral file (no separate "type" field), so this is the only field
    /// that needs setting. "Angle to node position #1" (-2.0202) and fitting thickness (4.191 mm)
    /// are still reused verbatim from `44002.cii` (confirmed constant across all 13 of that
    /// file's bends, which all shared one radius, 381 mm) — since our bends now use a different,
    /// per-pipe-size radius, whether these two values still hold is unconfirmed; logged in
    /// QUESTIONS.md.
    /// </summary>
    private static List<string> BuildBendLines(IReadOnlyList<PipeSegmentSpec> segments, IReadOnlyList<int> bendNodes) =>
        bendNodes
            .SelectMany(node =>
            {
                var outsideDiameter = segments.First(s => s.ToNode == node).OutsideDiameter;
                var radius = ElementSplitter.LongRadiusToOutsideDiameterFactor * outsideDiameter;
                return FixedWidth.FormatRealLines(
                [
                    radius, 0, -2.0202, node - 1, 0, node - 2, 0, 0, 0, 4.191, 0, 0, 0, 0,
                ]);
            })
            .ToList();
}

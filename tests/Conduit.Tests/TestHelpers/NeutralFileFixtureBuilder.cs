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

    public static NeutralFile Build(IReadOnlyList<PipeSegmentSpec> segments, IReadOnlyList<int> anchorNodes, int izup = 0)
    {
        var blocks = new List<NeutralFileBlock>();

        NeutralFileBlock AddBlock(string name, IEnumerable<string>? lines = null)
        {
            var block = new NeutralFileBlock { Name = name, HeaderLine = $"#$ {name}", RawLines = (lines ?? []).ToList() };
            blocks.Add(block);
            return block;
        }

        AddBlock("VERSION", BuildVersionLines());
        AddBlock("CONTROL");
        var elementsBlock = AddBlock("ELEMENTS", BuildElementLines(segments));
        AddBlock("AUX_DATA");
        AddBlock("BEND");
        AddBlock("RIGID");
        AddBlock("EXPJT");
        AddBlock("RESTRANT");
        AddBlock("DISPLMNT");
        AddBlock("FORCMNT");
        AddBlock("UNIFORM");
        AddBlock("WIND", BuildWindLines());
        AddBlock("OFFSETS");
        AddBlock("ALLOWBLS");
        AddBlock("SIF&TEES");
        AddBlock("REDUCERS");
        AddBlock("FLANGES");
        AddBlock("EQUIPMNT");
        AddBlock("MISCEL_1", FixedWidth.FormatRealLines(segments.Select(_ => 1.0).ToList()));
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
            NumBends = 0,
            NumRigids = 0,
            NumExpansionJoints = 0,
            NumRestraints = anchorNodes.Count,
            NumDisplacements = 0,
            NumForceMoments = 0,
            NumUniformLoads = 0,
            NumWindLoads = 0,
            NumOffsets = 0,
            NumAllowableStress = 0,
            NumIntersections = 0,
            Izup = izup,
            NumEquipmentChecks = 0,
        };

        var elements = Element.ParseMany(elementsBlock.RawLines, 0, segments.Count);

        var restraints = anchorNodes
            .Select(node => Restraint.CreateSingleDof(node, RestraintType.Anc))
            .ToList();

        var file = new NeutralFile
        {
            Blocks = blocks,
            Control = control,
            Elements = elements,
            NodeNames = [],
            Restraints = restraints,
            MaterialIds = segments.Select(_ => 1).ToList(),
            AllowableStresses = [],
            NozzleLimits = [],
            Units = UnitsSection.Parse(unitsBlock),
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
    /// <c>#$ WIND</c> is never truly empty — confirmed byte-identical across 4 unrelated real
    /// samples with no wind load applied, so this default row looks like a CAESAR II structural
    /// default rather than per-project data (logged as an assumption in QUESTIONS.md).
    /// </summary>
    private static List<string> BuildWindLines() =>
        FixedWidth.FormatRealLines([0.0, 0.7, 0.0, 0.0, 0.0, 0.0]).ToList();

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

    private static List<string> BuildElementLines(IReadOnlyList<PipeSegmentSpec> segments)
    {
        var lines = new List<string>();
        foreach (var segment in segments)
        {
            var real = BuildRealValues(segment);

            lines.AddRange(FixedWidth.FormatRealLines(real));
            lines.Add(FixedWidth.FormatLengthPrefixedString(string.Empty));
            lines.Add(FixedWidth.FormatLengthPrefixedString(string.Empty));
            // Line color/line visibility: NeutralFile-v15.pdf labels this (2X, 6G13.6) — real-value
            // format — but all 3 real samples (fixtures/real-samples/*.cii) write it as plain
            // integers ("-1 -1", no decimal/E-notation) instead. Writing it as a real (the bug this
            // replaced) is a confirmed cause of iecho.exe's "Error processing ELEMENT section, line
            // # NN" — see QUESTIONS.md's "ELEMENTS color/visibility line" entry.
            lines.AddRange(FixedWidth.FormatIntLines([-1, -1]));
            lines.AddRange(FixedWidth.FormatIntLines(new long[15]));
        }
        return lines;
    }
}

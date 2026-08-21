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

    /// <summary>A 6" Sch 40 carbon-steel segment of the given length along the X axis.</summary>
    public static PipeSegmentSpec Schedule40Run(int fromNode, int toNode, double length) =>
        new(fromNode, toNode, DeltaX: length, DeltaY: 0, DeltaZ: 0, OutsideDiameter: 6.625, WallThickness: 0.280, PipeDensity: 0.2836);

    /// <summary>A 6" Sch 40 carbon-steel vertical riser segment of the given length (Y-up).</summary>
    public static PipeSegmentSpec Schedule40Riser(int fromNode, int toNode, double length) =>
        new(fromNode, toNode, DeltaX: 0, DeltaY: length, DeltaZ: 0, OutsideDiameter: 6.625, WallThickness: 0.280, PipeDensity: 0.2836);

    /// <summary>Builds a standalone <see cref="Element"/> from a spec, for tests that need one without a whole file.</summary>
    public static Element ToElement(this PipeSegmentSpec segment) =>
        new() { RealValues = BuildRealValues(segment) };

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

        AddBlock("VERSION", ["    5.00000      15.0000        1252"]);
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
        AddBlock("WIND");
        AddBlock("OFFSETS");
        AddBlock("ALLOWBLS");
        AddBlock("SIF&TEES");
        AddBlock("REDUCERS");
        AddBlock("FLANGES");
        AddBlock("EQUIPMNT");
        AddBlock("MISCEL_1");
        AddBlock("UNITS");
        AddBlock("COORDS");

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
        };

        // Regenerate CONTROL/RESTRANT raw lines from the model right away, so the returned
        // file's Blocks are already consistent (NeutralFileWriter does this again on write,
        // harmlessly, since it's idempotent).
        NeutralFileWriter.ToLines(file);

        return file;
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
            lines.AddRange(FixedWidth.FormatRealLines([0, -1]));
            lines.AddRange(FixedWidth.FormatIntLines(new long[15]));
        }
        return lines;
    }
}

namespace Conduit.Core.NeutralFiles;

/// <summary>Parses a CAESAR II neutral file (<c>.cii</c>) into a <see cref="NeutralFile"/>.</summary>
public static class NeutralFileReader
{
    public static NeutralFile Read(string path)
    {
        if (!File.Exists(path))
        {
            throw new NeutralFileParseException($"Neutral file not found: {path}");
        }
        return Parse(File.ReadAllLines(path));
    }

    public static NeutralFile Parse(IReadOnlyList<string> lines)
    {
        var blocks = SplitIntoBlocks(lines);

        var controlBlock = RequireBlock(blocks, "CONTROL");
        var control = ControlSection.Parse(controlBlock);

        var elementsBlock = RequireBlock(blocks, "ELEMENTS");
        var elements = Element.ParseMany(elementsBlock.RawLines, 0, control.NumElements);

        var nodeNameBlock = FindBlock(blocks, "NODENAME");
        var nodeNames = nodeNameBlock is null
            ? new List<NodeName>()
            : NodeName.ParseMany(nodeNameBlock.RawLines);

        var restraintBlock = RequireBlock(blocks, "RESTRANT");
        var restraints = Restraint.ParseMany(restraintBlock.RawLines, control.NumRestraints);

        return new NeutralFile
        {
            Blocks = blocks,
            Control = control,
            Elements = elements,
            NodeNames = nodeNames,
            Restraints = restraints,
        };
    }

    private static List<NeutralFileBlock> SplitIntoBlocks(IReadOnlyList<string> lines)
    {
        var blocks = new List<NeutralFileBlock>();
        NeutralFileBlock? current = null;

        foreach (var line in lines)
        {
            if (line.StartsWith("#$", StringComparison.Ordinal))
            {
                var name = line.Length > 2 ? line[2..].Trim() : string.Empty;
                current = new NeutralFileBlock { Name = name, HeaderLine = line, RawLines = new List<string>() };
                blocks.Add(current);
            }
            else if (current is not null)
            {
                current.RawLines.Add(line);
            }
            else if (!string.IsNullOrWhiteSpace(line))
            {
                throw new NeutralFileParseException(
                    $"Expected the file to start with a '#$ ' section header, found: \"{line}\"");
            }
        }

        return blocks;
    }

    private static NeutralFileBlock? FindBlock(IReadOnlyList<NeutralFileBlock> blocks, string name) =>
        blocks.FirstOrDefault(b => string.Equals(b.Name, name, StringComparison.OrdinalIgnoreCase));

    private static NeutralFileBlock RequireBlock(IReadOnlyList<NeutralFileBlock> blocks, string name) =>
        FindBlock(blocks, name)
        ?? throw new NeutralFileParseException($"Required section '#$ {name}' was not found in the neutral file.");
}

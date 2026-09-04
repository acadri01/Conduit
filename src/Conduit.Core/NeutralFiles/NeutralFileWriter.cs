namespace Conduit.Core.NeutralFiles;

/// <summary>Writes a <see cref="NeutralFile"/> back out as a CAESAR II neutral file (<c>.cii</c>).</summary>
public static class NeutralFileWriter
{
    /// <summary>
    /// CAESAR II's own neutral files always use CRLF line endings (confirmed against real
    /// exported <c>.cii</c> files) — <c>iecho.exe</c> and CAESAR II itself are Windows/Fortran
    /// heritage tools that expect this convention. Writing LF-only, even on Linux, produces a
    /// file those tools reject; <see cref="NeutralFileReader.Read"/> tolerates either (via
    /// <see cref="File.ReadAllLines(string)"/>), so this asymmetry was silently downgrading
    /// CRLF input to LF output on every round-trip.
    /// </summary>
    public static void Write(NeutralFile file, string path)
    {
        var lines = ToLines(file);
        File.WriteAllText(path, string.Join("\r\n", lines) + "\r\n");
    }

    public static List<string> ToLines(NeutralFile file)
    {
        SyncEditedBlocks(file);

        var lines = new List<string>();
        foreach (var block in file.Blocks)
        {
            lines.Add(block.HeaderLine);
            lines.AddRange(block.RawLines);
        }
        return lines;
    }

    /// <summary>
    /// Regenerates the raw lines of the two blocks Conduit actively edits (<c>CONTROL</c>,
    /// for its restraint count, and <c>RESTRANT</c> itself) from the current
    /// <see cref="NeutralFile.Control"/> and <see cref="NeutralFile.Restraints"/>. Every other
    /// block's raw lines are left untouched, so an unmodified file round-trips byte-identical.
    /// </summary>
    private static void SyncEditedBlocks(NeutralFile file)
    {
        var controlBlock = file.Blocks.First(b => string.Equals(b.Name, "CONTROL", StringComparison.OrdinalIgnoreCase));
        file.Control.WriteBackTo(controlBlock);

        var restraintBlock = file.Blocks.First(b => string.Equals(b.Name, "RESTRANT", StringComparison.OrdinalIgnoreCase));
        restraintBlock.RawLines.Clear();
        restraintBlock.RawLines.AddRange(file.Restraints.SelectMany(r => r.ToRawLines()));
    }
}

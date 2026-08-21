namespace Conduit.Core.NeutralFiles;

/// <summary>
/// One nozzle/equipment load-limit entry from <c>#$ EQUIPMNT</c>. Each file record holds two
/// 17-value slots (per the vendor doc); v1 flattens both into a single list, dropping unused
/// slots (<see cref="Node"/> == 0), since nothing currently needs to distinguish which slot a
/// limit came from. Read-only: Conduit never writes this section.
/// </summary>
public sealed class NozzleLimit
{
    /// <summary>All 17 values for this slot, in vendor-doc order (0-based).</summary>
    public required IReadOnlyList<double> Values { get; init; }

    public int Node => (int)Values[0];
    public double LimitFx => Values[1];
    public double LimitFy => Values[2];
    public double LimitFz => Values[3];
    public double LimitMx => Values[4];
    public double LimitMy => Values[5];
    public double LimitMz => Values[6];

    /// <summary>Parses <paramref name="count"/> two-slot EQUIPMNT records, returning the used (non-zero-node) slots flattened.</summary>
    public static List<NozzleLimit> ParseMany(IReadOnlyList<string> lines, int count)
    {
        var limits = new List<NozzleLimit>();
        var lineIndex = 0;
        for (var i = 0; i < count; i++)
        {
            for (var slot = 0; slot < 2; slot++)
            {
                var values = FixedWidth.ParseReals(lines, ref lineIndex, 17);
                if ((int)values[0] != 0)
                {
                    limits.Add(new NozzleLimit { Values = values });
                }
            }
        }
        return limits;
    }
}

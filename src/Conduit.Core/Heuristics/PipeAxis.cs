using Conduit.Core.NeutralFiles;

namespace Conduit.Core.Heuristics;

/// <summary>
/// Which of the model's three principal axes a (90°-only, MVP-scope) element runs along.
///
/// <para>Per direct instruction: span accumulation for the two horizontal axes must be tracked
/// <b>separately</b>, not summed into one combined running total — a rest support on one leg of a
/// jog also supports the *other* horizontal axis at that point, since gravity support doesn't
/// depend on which horizontal direction the local pipe segment happens to run in. Diagonal/45°
/// segments are explicitly out of MVP scope (per direct instruction, "let's take one thing at a
/// time") — <see cref="PipeAxisClassifier.Determine"/> always resolves to the single best-fit
/// axis even for a non-axis-aligned element, rather than modeling a true local coordinate system.</para>
/// </summary>
public enum PipeAxis
{
    /// <summary>One of the model's two horizontal axes (X, or Z when <c>Izup</c>=0 / Y when <c>Izup</c>=1).</summary>
    HorizontalA,

    /// <summary>The other horizontal axis.</summary>
    HorizontalB,

    /// <summary>The model's vertical axis, per <c>#$ CONTROL</c>'s <c>Izup</c>.</summary>
    Vertical,
}

public static class PipeAxisClassifier
{
    /// <summary>Fraction of an element's length its dominant-axis delta must reach to count as running along that axis.</summary>
    public const double DominanceFraction = 0.9;

    /// <param name="izup">The model's vertical-axis flag from <c>#$ CONTROL</c> (0 = -Y vertical, 1 = -Z vertical).</param>
    public static PipeAxis Determine(Element element, int izup)
    {
        if (element.Length <= 0)
        {
            return PipeAxis.HorizontalA;
        }

        var verticalDelta = izup == 0 ? element.DeltaY : element.DeltaZ;
        if (Math.Abs(verticalDelta) / element.Length >= DominanceFraction)
        {
            return PipeAxis.Vertical;
        }

        var (a, b) = izup == 0 ? (element.DeltaX, element.DeltaZ) : (element.DeltaX, element.DeltaY);
        return Math.Abs(a) >= Math.Abs(b) ? PipeAxis.HorizontalA : PipeAxis.HorizontalB;
    }
}

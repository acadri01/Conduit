namespace Conduit.Core.Configuration;

/// <summary>
/// Parses <c>caesar.cfg</c>, CAESAR II's per-directory global settings file. There's no vendor
/// documentation for this format (unlike the neutral file) — only a single real example the user
/// shared, confirmed as a non-proprietary demonstration case safe to use directly. The parser is
/// deliberately lenient rather than a strict grammar: each recognized line is <c>KEY = VALUE</c>
/// followed by loosely-aligned numeric column metadata this parser ignores, e.g.
/// <c>DEFAULT_CODE =                    B31.3_2020        43      43.</c> parses to key
/// <c>DEFAULT_CODE</c>, value <c>B31.3_2020</c>. Lines without an <c>=</c> (e.g. the leading
/// <c>Ver. 15.010</c> version line) are skipped rather than throwing, since this file's exact
/// grammar is unconfirmed beyond the one example.
/// </summary>
public static class CaesarConfigReader
{
    public static CaesarConfig Read(string path) => Parse(File.ReadAllLines(path));

    public static CaesarConfig Parse(IEnumerable<string> lines)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in lines)
        {
            var equalsIndex = line.IndexOf('=');
            if (equalsIndex <= 0)
            {
                continue;
            }

            var key = line[..equalsIndex].Trim();
            var remainder = line[(equalsIndex + 1)..].Trim();
            var value = remainder.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();

            if (key.Length > 0 && value is not null)
            {
                values[key] = value;
            }
        }

        return new CaesarConfig { Values = values };
    }
}

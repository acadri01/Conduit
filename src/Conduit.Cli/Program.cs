using Conduit.Core.Configuration;
using Conduit.Core.NeutralFiles;
using Conduit.Core.Optimization;
using Conduit.Core.Stress;

return Run(args);

static int Run(string[] args)
{
    if (args.Length != 3 || args[0] != "optimize")
    {
        Console.Error.WriteLine("Usage: conduit optimize <input.cii> <output.cii>");
        return 1;
    }

    var inputPath = args[1];
    var outputPath = args[2];

    NeutralFile file;
    try
    {
        file = NeutralFileReader.Read(inputPath);
    }
    catch (NeutralFileParseException ex)
    {
        Console.Error.WriteLine($"Failed to parse '{inputPath}': {ex.Message}");
        return 1;
    }

    var config = TryReadCaesarConfig(inputPath);

    var result = OptimizationLoop.Run(file, new MockStressSolver());

    NeutralFileWriter.Write(file, outputPath);

    PrintSummary(inputPath, outputPath, file, config, result);

    return result.Passed ? 0 : 2;
}

/// <summary>
/// Looks for <c>caesar.cfg</c> next to the input file — every CAESAR II model directory has one.
/// Best-effort/supplementary: a missing or unparseable config doesn't fail the run, since v1 only
/// uses it to cross-check the per-file axis setting and surface context (default code, material
/// database locations), never as a substitute for the neutral file's own data.
/// </summary>
static CaesarConfig? TryReadCaesarConfig(string inputPath)
{
    var directory = Path.GetDirectoryName(Path.GetFullPath(inputPath));
    var configPath = Path.Combine(directory ?? ".", "caesar.cfg");
    if (!File.Exists(configPath))
    {
        return null;
    }

    try
    {
        return CaesarConfigReader.Read(configPath);
    }
    catch (IOException)
    {
        return null;
    }
}

static void PrintSummary(string inputPath, string outputPath, NeutralFile file, CaesarConfig? config, OptimizationResult result)
{
    Console.WriteLine($"Conduit optimize: {inputPath} -> {outputPath}");
    Console.WriteLine();

    var effectiveCode = CaesarConfig.EffectiveCode(config);
    Console.WriteLine($"  Piping code assumed: {effectiveCode}" +
        (config?.DefaultCode is not null ? " (from caesar.cfg)" : " (default — no caesar.cfg DEFAULT_CODE found)"));

    if (config is not null)
    {
        if (config.ZAxisUp is { } zAxisUp && zAxisUp != (file.Control.Izup == 1))
        {
            Console.WriteLine(
                $"  Warning: caesar.cfg's Z_AXIS_UP ({(zAxisUp ? "YES" : "NO")}) disagrees with this " +
                $"file's own IZUP ({file.Control.Izup}) — using the neutral file's IZUP as authoritative.");
        }

        if (config.SystemDirectoryName is not null || config.UserMaterialFileName is not null)
        {
            Console.WriteLine(
                $"  Material database (caesar.cfg): system directory '{config.SystemDirectoryName}'" +
                (config.UserMaterialFileName is not null ? $", user material file '{config.UserMaterialFileName}'" : string.Empty));
        }
    }

    Console.WriteLine();

    foreach (var note in result.Notes)
    {
        Console.WriteLine($"  - {note}");
    }

    Console.WriteLine();
    Console.WriteLine($"Iterations: {result.Iterations}");

    var failing = result.FinalStressResult.Findings.Where(f => !f.Passed).ToList();
    if (failing.Count > 0)
    {
        Console.WriteLine("Remaining failing spans:");
        foreach (var finding in failing)
        {
            Console.WriteLine($"  - {finding.Message}");
        }
    }

    Console.WriteLine();
    Console.WriteLine(result.Passed ? "PASS" : "FAIL");
}

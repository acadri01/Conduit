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

    var result = OptimizationLoop.Run(file, new MockStressSolver());

    NeutralFileWriter.Write(file, outputPath);

    PrintSummary(inputPath, outputPath, result);

    return result.Passed ? 0 : 2;
}

static void PrintSummary(string inputPath, string outputPath, OptimizationResult result)
{
    Console.WriteLine($"Conduit optimize: {inputPath} -> {outputPath}");
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

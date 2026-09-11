namespace Source2VpkDump;

internal static class ConsoleReporter
{
    public static void WriteHelp()
    {
        Console.WriteLine("Source2VpkDump - extract Source 2 VPK entries without decompiling them");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  vpkdump <input.vpk> [-o|--output <directory>]");
        Console.WriteLine("  vpkdump --help");
        Console.WriteLine("  vpkdump --version");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  -o, --output <directory>  Output directory, defaults to .\\assets");
        Console.WriteLine("  -h, --help                Show this help");
        Console.WriteLine("      --version             Show the application version");
        Console.WriteLine();
        Console.WriteLine("Use the *_dir.vpk file when the package has split VPK files. Keep its sibling files beside it.");
    }

    public static void WriteVersion()
    {
        var version = typeof(Program).Assembly.GetName().Version?.ToString(3) ?? "0.1.0";
        Console.WriteLine($"Source2VpkDump {version}");
    }

    public static void WriteEntry(ExtractionProgress progress)
    {
        Console.WriteLine($"Extracted {progress.EntryPath} ({progress.ByteCount:N0} bytes)");
    }

    public static void WriteSummary(string inputPath, ExtractionResult result, TimeSpan elapsed)
    {
        Console.WriteLine();
        Console.WriteLine($"Input: {inputPath}");
        Console.WriteLine($"Output: {result.OutputDirectory}");
        Console.WriteLine($"Entries: {result.EntryCount:N0}");
        Console.WriteLine($"Bytes: {result.ByteCount:N0}");
        Console.WriteLine($"Elapsed: {elapsed.TotalSeconds:N2}s");
    }
}

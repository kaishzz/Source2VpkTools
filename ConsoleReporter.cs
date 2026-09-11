namespace Source2VpkTools;

internal static class ConsoleReporter
{
    public static void WriteHelp()
    {
        Console.WriteLine("Source2VpkTools - extract and upload Source 2 VPK content");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  Source2VpkTools <input.vpk> [-o|--output <directory>]");
        Console.WriteLine("  Source2VpkTools upload --content-dir <directory> --package-name <file.vpk> --title <title> --preview <file>");
        Console.WriteLine("  Source2VpkTools upload --vpk <file.vpk> --title <title> --preview <file>");
        Console.WriteLine("  Source2VpkTools --help");
        Console.WriteLine("  Source2VpkTools --version");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  -o, --output <directory>  Output directory, defaults to .\\assets");
        Console.WriteLine("  -h, --help                Show this help");
        Console.WriteLine("      --version             Show the application version");
        Console.WriteLine();
        Console.WriteLine("Upload options:");
        Console.WriteLine("      --content-dir <dir>  VPK root; recursively includes all regular files");
        Console.WriteLine("      --vpk <file.vpk>     Upload an existing VPK and its numbered chunks directly");
        Console.WriteLine("      --package-name <vpk> Output VPK file name for --content-dir");
        Console.WriteLine("      --workshop-id <id>   Existing item to update");
        Console.WriteLine("      --preview <file>     Workshop preview image");
        Console.WriteLine("      --description-file <file>");
        Console.WriteLine("      --changenote <text>  Workshop change note");
        Console.WriteLine("      --visibility <mode>  public, friends, private, or unlisted");
        Console.WriteLine("      --tag <tag>          Workshop tag, repeatable");
        Console.WriteLine("      --tags <a,b>         Comma-separated Workshop tags");
        Console.WriteLine("      --state-file <file>  Save the ID and validated manifest after success");
        Console.WriteLine("      --dry-run            Build and validate without uploading");
        Console.WriteLine("      --keep-staging       Keep generated upload files after failure");
        Console.WriteLine("Use the *_dir.vpk file when the package has split VPK files. Keep its sibling files beside it.");
    }

    public static void WriteVersion()
    {
        var version = typeof(Program).Assembly.GetName().Version?.ToString(3) ?? "0.1.0";
        Console.WriteLine($"Source2VpkTools {version}");
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

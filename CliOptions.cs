namespace Source2VpkDump;

internal sealed record CliOptions(
    string? InputPath,
    string? OutputDirectory,
    bool ShowHelp,
    bool ShowVersion)
{
    public static CliParseResult Parse(string[] args)
    {
        if (args.Length == 0)
        {
            return new CliParseResult(null, "An input VPK path is required");
        }

        string? inputPath = null;
        string? outputDirectory = null;
        var showHelp = false;
        var showVersion = false;

        for (var index = 0; index < args.Length; index++)
        {
            var argument = args[index];
            switch (argument)
            {
                case "-h":
                case "--help":
                    showHelp = true;
                    break;
                case "--version":
                    showVersion = true;
                    break;
                case "-o":
                case "--output":
                    if (++index >= args.Length || string.IsNullOrWhiteSpace(args[index]))
                    {
                        return new CliParseResult(null, $"Missing value for {argument}");
                    }

                    if (outputDirectory is not null)
                    {
                        return new CliParseResult(null, "The output directory was specified more than once");
                    }

                    outputDirectory = args[index];
                    break;
                default:
                    if (argument.StartsWith("-", StringComparison.Ordinal))
                    {
                        return new CliParseResult(null, $"Unknown option: {argument}");
                    }

                    if (inputPath is not null)
                    {
                        return new CliParseResult(null, "Only one input VPK path may be specified");
                    }

                    inputPath = argument;
                    break;
            }
        }

        if (showHelp || showVersion)
        {
            return new CliParseResult(new CliOptions(inputPath, outputDirectory, showHelp, showVersion), null);
        }

        if (inputPath is null)
        {
            return new CliParseResult(null, "An input VPK path is required");
        }

        var fullInputPath = Path.GetFullPath(inputPath);
        var fullOutputPath = Path.GetFullPath(outputDirectory ?? Path.Combine(Environment.CurrentDirectory, "assets"));
        return new CliParseResult(new CliOptions(fullInputPath, fullOutputPath, false, false), null);
    }
}

internal sealed record CliParseResult(CliOptions? Options, string? Error)
{
    public bool IsSuccess => Error is null;
}

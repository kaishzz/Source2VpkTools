namespace Source2VpkDump;

internal enum CliCommand
{
    Extract,
    Upload
}

internal sealed record CliOptions(
    string? InputPath,
    string? OutputDirectory,
    CliCommand Command,
    UploadOptions? Upload,
    bool ShowHelp,
    bool ShowVersion)
{
    public static CliParseResult Parse(string[] args)
    {
        if (args.Length == 0)
        {
            return new CliParseResult(null, "An input VPK path is required");
        }

        if (string.Equals(args[0], "upload", StringComparison.OrdinalIgnoreCase))
        {
            return ParseUpload(args[1..]);
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
            return new CliParseResult(new CliOptions(inputPath, outputDirectory, CliCommand.Extract, null, showHelp, showVersion), null);
        }

        if (inputPath is null)
        {
            return new CliParseResult(null, "An input VPK path is required");
        }

        var fullInputPath = Path.GetFullPath(inputPath);
        var fullOutputPath = Path.GetFullPath(outputDirectory ?? Path.Combine(Environment.CurrentDirectory, "assets"));
        return new CliParseResult(new CliOptions(fullInputPath, fullOutputPath, CliCommand.Extract, null, false, false), null);
    }

    private static CliParseResult ParseUpload(string[] args)
    {
        if (args.Any(static argument => argument is "-h" or "--help"))
        {
            return new CliParseResult(new CliOptions(null, null, CliCommand.Upload, null, true, false), null);
        }

        string? contentDirectory = null;
        string? vpkPath = null;
        string? packageName = null;
        ulong? workshopId = null;
        string? title = null;
        string? previewPath = null;
        string? descriptionFilePath = null;
        string changeNote = string.Empty;
        var visibility = UploadVisibility.Private;
        var tags = new List<string>();
        var dryRun = false;
        var keepStaging = false;
        string? stateFilePath = null;
        var singleOptions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < args.Length; index++)
        {
            var argument = args[index];
            switch (argument)
            {
                case "--content-dir":
                    if (!singleOptions.Add(argument))
                    {
                        return DuplicateOption(argument);
                    }

                    if (!TryReadValue(args, ref index, argument, out contentDirectory, out var error))
                    {
                        return new CliParseResult(null, error);
                    }

                    break;
                case "--package-name":
                    if (!singleOptions.Add(argument))
                    {
                        return DuplicateOption(argument);
                    }

                    if (!TryReadValue(args, ref index, argument, out packageName, out error))
                    {
                        return new CliParseResult(null, error);
                    }

                    break;
                case "--vpk":
                    if (!singleOptions.Add(argument))
                    {
                        return DuplicateOption(argument);
                    }

                    if (!TryReadValue(args, ref index, argument, out vpkPath, out var vpkError))
                    {
                        return new CliParseResult(null, vpkError);
                    }

                    break;
                case "--workshop-id":
                    if (!singleOptions.Add(argument))
                    {
                        return DuplicateOption(argument);
                    }

                    if (!TryReadValue(args, ref index, argument, out var workshopIdText, out error) ||
                        !ulong.TryParse(workshopIdText, out var parsedWorkshopId) || parsedWorkshopId == 0)
                    {
                        return new CliParseResult(null, error ?? "The workshop ID must be a positive integer");
                    }

                    workshopId = parsedWorkshopId;
                    break;
                case "--title":
                    if (!singleOptions.Add(argument))
                    {
                        return DuplicateOption(argument);
                    }

                    if (!TryReadValue(args, ref index, argument, out title, out error))
                    {
                        return new CliParseResult(null, error);
                    }

                    break;
                case "--preview":
                    if (!singleOptions.Add(argument))
                    {
                        return DuplicateOption(argument);
                    }

                    if (!TryReadValue(args, ref index, argument, out previewPath, out error))
                    {
                        return new CliParseResult(null, error);
                    }

                    break;
                case "--description-file":
                    if (!singleOptions.Add(argument))
                    {
                        return DuplicateOption(argument);
                    }

                    if (!TryReadValue(args, ref index, argument, out descriptionFilePath, out error))
                    {
                        return new CliParseResult(null, error);
                    }

                    break;
                case "--changenote":
                    if (!singleOptions.Add(argument))
                    {
                        return DuplicateOption(argument);
                    }

                    if (!TryReadValue(args, ref index, argument, out changeNote, out error))
                    {
                        return new CliParseResult(null, error);
                    }

                    break;
                case "--visibility":
                    if (!singleOptions.Add(argument))
                    {
                        return DuplicateOption(argument);
                    }

                    if (!TryReadValue(args, ref index, argument, out var visibilityText, out error) ||
                        !UploadVisibilityParser.TryParse(visibilityText, out visibility))
                    {
                        return new CliParseResult(null, error ?? "Visibility must be public, friends, private, or unlisted");
                    }

                    break;
                case "--tag":
                    if (!TryReadValue(args, ref index, argument, out var tag, out error))
                    {
                        return new CliParseResult(null, error);
                    }

                    tags.Add(tag);
                    break;
                case "--tags":
                    if (!singleOptions.Add(argument))
                    {
                        return DuplicateOption(argument);
                    }

                    if (!TryReadValue(args, ref index, argument, out var tagList, out error))
                    {
                        return new CliParseResult(null, error);
                    }

                    tags.AddRange(tagList.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
                    break;
                case "--state-file":
                    if (!singleOptions.Add(argument))
                    {
                        return DuplicateOption(argument);
                    }

                    if (!TryReadValue(args, ref index, argument, out stateFilePath, out error))
                    {
                        return new CliParseResult(null, error);
                    }

                    break;
                case "--dry-run":
                    if (!singleOptions.Add(argument))
                    {
                        return DuplicateOption(argument);
                    }

                    dryRun = true;
                    break;
                case "--keep-staging":
                    if (!singleOptions.Add(argument))
                    {
                        return DuplicateOption(argument);
                    }

                    keepStaging = true;
                    break;
                case "--version":
                    return new CliParseResult(new CliOptions(null, null, CliCommand.Upload, null, false, true), null);
                default:
                    return new CliParseResult(null, $"Unknown upload option: {argument}");
            }
        }

        if (string.IsNullOrWhiteSpace(contentDirectory) == string.IsNullOrWhiteSpace(vpkPath))
        {
            return new CliParseResult(null, "Specify exactly one of --content-dir or --vpk");
        }

        if (vpkPath is not null && packageName is not null)
        {
            return new CliParseResult(null, "The --package-name option can only be used with --content-dir");
        }

        if (contentDirectory is not null && string.IsNullOrWhiteSpace(packageName))
        {
            return new CliParseResult(null, "The upload package name is required");
        }

        if (packageName is not null &&
            (packageName.Contains(Path.DirectorySeparatorChar) || packageName.Contains(Path.AltDirectorySeparatorChar) ||
             !packageName.EndsWith(".vpk", StringComparison.OrdinalIgnoreCase)))
        {
            return new CliParseResult(null, "The package name must be a file name ending in .vpk");
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            return new CliParseResult(null, "The upload title is required");
        }

        if (string.IsNullOrWhiteSpace(previewPath))
        {
            return new CliParseResult(null, "The upload preview file is required");
        }

        var fullContentDirectory = contentDirectory is null ? null : Path.GetFullPath(contentDirectory);
        if (fullContentDirectory is not null && !Directory.Exists(fullContentDirectory))
        {
            return new CliParseResult(null, $"The upload content directory was not found: {fullContentDirectory}");
        }

        var fullVpkPath = ResolveExistingFile(vpkPath, "VPK file", out var vpkPathError);
        if (vpkPathError is not null)
        {
            return new CliParseResult(null, vpkPathError);
        }

        if (fullVpkPath is not null && !fullVpkPath.EndsWith(".vpk", StringComparison.OrdinalIgnoreCase))
        {
            return new CliParseResult(null, $"The direct upload input must be a .vpk file: {fullVpkPath}");
        }

        var fullPreviewPath = ResolveExistingFile(previewPath, "preview file", out var previewError);
        if (previewError is not null)
        {
            return new CliParseResult(null, previewError);
        }

        if (new FileInfo(fullPreviewPath!).Length >= WorkshopConstants.MaxWorkshopPreviewBytes)
        {
            return new CliParseResult(null, $"The preview file must be smaller than {WorkshopConstants.MaxWorkshopPreviewBytes:N0} bytes for Steam Workshop");
        }

        var fullDescriptionPath = ResolveExistingFile(descriptionFilePath, "description file", out var descriptionError);
        if (descriptionError is not null)
        {
            return new CliParseResult(null, descriptionError);
        }

        var fullStatePath = stateFilePath is null ? null : Path.GetFullPath(stateFilePath);
        return new CliParseResult(
            new CliOptions(
                null,
                null,
                CliCommand.Upload,
                new UploadOptions(
                    fullContentDirectory,
                    fullVpkPath,
                    packageName,
                    workshopId,
                    title,
                    fullPreviewPath!,
                    fullDescriptionPath,
                    changeNote,
                    visibility,
                    tags.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
                    dryRun,
                    keepStaging,
                    fullStatePath),
                false,
                false),
            null);
    }

    private static CliParseResult DuplicateOption(string option) =>
        new(null, $"The option was specified more than once: {option}");

    private static string? ResolveExistingFile(string? path, string description, out string? error)
    {
        error = null;
        if (path is null)
        {
            return null;
        }

        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            error = $"The {description} was not found: {fullPath}";
            return null;
        }

        return fullPath;
    }

    private static bool TryReadValue(string[] args, ref int index, string option, out string value, out string? error)
    {
        if (++index >= args.Length || string.IsNullOrWhiteSpace(args[index]))
        {
            value = string.Empty;
            error = $"Missing value for {option}";
            return false;
        }

        value = args[index];
        error = null;
        return true;
    }
}

internal sealed record CliParseResult(CliOptions? Options, string? Error)
{
    public bool IsSuccess => Error is null;
}

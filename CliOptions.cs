namespace Source2VpkTools;

internal enum CliCommand
{
    Extract,
    Compile,
    Entities,
    Upload
}

internal sealed record CliOptions(
    string? InputPath,
    string? OutputDirectory,
    CliCommand Command,
    CompileOptions? Compile,
    EntityDumpOptions? Entities,
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

        if (string.Equals(args[0], "compile", StringComparison.OrdinalIgnoreCase))
        {
            return ParseCompile(args[1..]);
        }

        if (string.Equals(args[0], "entities", StringComparison.OrdinalIgnoreCase))
        {
            return ParseEntities(args[1..]);
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
            return new CliParseResult(new CliOptions(inputPath, outputDirectory, CliCommand.Extract, null, null, null, showHelp, showVersion), null);
        }

        if (inputPath is null)
        {
            return new CliParseResult(null, "An input VPK path is required");
        }

        var fullInputPath = Path.GetFullPath(inputPath);
        var fullOutputPath = Path.GetFullPath(outputDirectory ?? Path.Combine(Environment.CurrentDirectory, "assets"));
        return new CliParseResult(new CliOptions(fullInputPath, fullOutputPath, CliCommand.Extract, null, null, null, false, false), null);
    }

    private static CliParseResult ParseEntities(string[] args)
    {
        if (args.Any(static argument => argument is "-h" or "--help"))
        {
            return new CliParseResult(new CliOptions(null, null, CliCommand.Entities, null, null, null, true, false), null);
        }

        string? inputPath = null;
        string? outputDirectory = null;
        var singleOptions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < args.Length; index++)
        {
            var argument = args[index];
            switch (argument)
            {
                case "--input":
                    if (!singleOptions.Add(argument))
                    {
                        return DuplicateOption(argument);
                    }

                    if (!TryReadValue(args, ref index, argument, out inputPath, out var inputError))
                    {
                        return new CliParseResult(null, inputError);
                    }

                    break;
                case "-o":
                case "--output":
                    if (!singleOptions.Add(argument))
                    {
                        return DuplicateOption(argument);
                    }

                    if (!TryReadValue(args, ref index, argument, out outputDirectory, out var outputError))
                    {
                        return new CliParseResult(null, outputError);
                    }

                    break;
                case "--version":
                    return new CliParseResult(new CliOptions(null, null, CliCommand.Entities, null, null, null, false, true), null);
                default:
                    return new CliParseResult(null, $"Unknown entities option: {argument}");
            }
        }

        if (string.IsNullOrWhiteSpace(inputPath))
        {
            return new CliParseResult(null, "The entities input VPK path is required");
        }

        var fullInputPath = Path.GetFullPath(inputPath);
        if (!File.Exists(fullInputPath))
        {
            return new CliParseResult(null, $"The entities input VPK was not found: {fullInputPath}");
        }

        var fullOutputPath = Path.GetFullPath(outputDirectory ?? Path.Combine(Environment.CurrentDirectory, "entities"));
        return new CliParseResult(
            new CliOptions(
                null,
                null,
                CliCommand.Entities,
                null,
                new EntityDumpOptions(fullInputPath, fullOutputPath),
                null,
                false,
                false),
            null);
    }

    private static CliParseResult ParseCompile(string[] args)
    {
        if (args.Any(static argument => argument is "-h" or "--help"))
        {
            return new CliParseResult(new CliOptions(null, null, CliCommand.Compile, null, null, null, true, false), null);
        }

        string? inputPath = null;
        string? cs2Root = null;
        string? resourceCompilerPath = null;
        string? gameInfoPath = null;
        string? outputDirectory = null;
        var recursive = false;
        var force = false;
        var noVpk = false;
        var noP4 = false;
        var verbose = false;
        var dryRun = false;
        var singleOptions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < args.Length; index++)
        {
            var argument = args[index];
            switch (argument)
            {
                case "--input":
                    if (!singleOptions.Add(argument))
                    {
                        return DuplicateOption(argument);
                    }

                    if (!TryReadValue(args, ref index, argument, out inputPath, out var inputError))
                    {
                        return new CliParseResult(null, inputError);
                    }

                    break;
                case "--cs2-root":
                    if (!singleOptions.Add(argument))
                    {
                        return DuplicateOption(argument);
                    }

                    if (!TryReadValue(args, ref index, argument, out cs2Root, out var rootError))
                    {
                        return new CliParseResult(null, rootError);
                    }

                    break;
                case "--resource-compiler":
                    if (!singleOptions.Add(argument))
                    {
                        return DuplicateOption(argument);
                    }

                    if (!TryReadValue(args, ref index, argument, out resourceCompilerPath, out var compilerError))
                    {
                        return new CliParseResult(null, compilerError);
                    }

                    break;
                case "--gameinfo":
                    if (!singleOptions.Add(argument))
                    {
                        return DuplicateOption(argument);
                    }

                    if (!TryReadValue(args, ref index, argument, out gameInfoPath, out var gameInfoError))
                    {
                        return new CliParseResult(null, gameInfoError);
                    }

                    break;
                case "--output":
                    if (!singleOptions.Add(argument))
                    {
                        return DuplicateOption(argument);
                    }

                    if (!TryReadValue(args, ref index, argument, out outputDirectory, out var outputError))
                    {
                        return new CliParseResult(null, outputError);
                    }

                    break;
                case "--recursive":
                    if (!singleOptions.Add(argument))
                    {
                        return DuplicateOption(argument);
                    }

                    recursive = true;
                    break;
                case "--force":
                    if (!singleOptions.Add(argument))
                    {
                        return DuplicateOption(argument);
                    }

                    force = true;
                    break;
                case "--novpk":
                    if (!singleOptions.Add(argument))
                    {
                        return DuplicateOption(argument);
                    }

                    noVpk = true;
                    break;
                case "--nop4":
                    if (!singleOptions.Add(argument))
                    {
                        return DuplicateOption(argument);
                    }

                    noP4 = true;
                    break;
                case "--verbose":
                    if (!singleOptions.Add(argument))
                    {
                        return DuplicateOption(argument);
                    }

                    verbose = true;
                    break;
                case "--dry-run":
                    if (!singleOptions.Add(argument))
                    {
                        return DuplicateOption(argument);
                    }

                    dryRun = true;
                    break;
                case "--version":
                    return new CliParseResult(new CliOptions(null, null, CliCommand.Compile, null, null, null, false, true), null);
                default:
                    return new CliParseResult(null, $"Unknown compile option: {argument}");
            }
        }

        if (string.IsNullOrWhiteSpace(inputPath))
        {
            return new CliParseResult(null, "The compile input path is required");
        }

        var fullInputPath = Path.GetFullPath(inputPath);
        if (!File.Exists(fullInputPath) && !Directory.Exists(fullInputPath))
        {
            return new CliParseResult(null, $"The compile input path was not found: {fullInputPath}");
        }

        var fullCs2Root = cs2Root is null ? null : Path.GetFullPath(cs2Root);
        if (fullCs2Root is not null && !Directory.Exists(fullCs2Root))
        {
            return new CliParseResult(null, $"The CS2 root directory was not found: {fullCs2Root}");
        }

        var fullCompilerPath = ResolveExistingFile(resourceCompilerPath, "resource compiler", out var compilerPathError);
        if (compilerPathError is not null)
        {
            return new CliParseResult(null, compilerPathError);
        }

        var fullGameInfoPath = ResolveGameInfoPath(gameInfoPath, out var gameInfoPathError);
        if (gameInfoPathError is not null)
        {
            return new CliParseResult(null, gameInfoPathError);
        }

        var fullOutputPath = outputDirectory is null ? null : Path.GetFullPath(outputDirectory);
        if (fullOutputPath is not null && File.Exists(fullOutputPath))
        {
            return new CliParseResult(null, $"The compile output path is a file: {fullOutputPath}");
        }

        return new CliParseResult(
            new CliOptions(
                null,
                null,
                CliCommand.Compile,
                new CompileOptions(fullInputPath, fullCs2Root, fullCompilerPath, fullGameInfoPath, fullOutputPath, recursive, force, noVpk, noP4, verbose, dryRun),
                null,
                null,
                false,
                false),
            null);
    }

    private static CliParseResult ParseUpload(string[] args)
    {
        if (args.Any(static argument => argument is "-h" or "--help"))
        {
            return new CliParseResult(new CliOptions(null, null, CliCommand.Upload, null, null, null, true, false), null);
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
                    return new CliParseResult(new CliOptions(null, null, CliCommand.Upload, null, null, null, false, true), null);
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
                null,
                null,
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

    private static string? ResolveGameInfoPath(string? path, out string? error)
    {
        error = null;
        if (path is null)
        {
            return null;
        }

        var fullPath = Path.GetFullPath(path);
        if (Directory.Exists(fullPath))
        {
            fullPath = Path.Combine(fullPath, "gameinfo.gi");
        }

        if (!File.Exists(fullPath))
        {
            error = $"The gameinfo.gi was not found: {fullPath}";
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

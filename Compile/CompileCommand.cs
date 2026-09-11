namespace Source2VpkTools;

internal static class CompileCommand
{
    public static int Run(CompileOptions options)
    {
        try
        {
            var resolution = ResourceCompilerLocator.Resolve(options);
            if (options.OutputDirectory is not null && resolution.Cs2Root is null)
            {
                throw new InvalidOperationException("A compile output directory requires a resolvable CS2 root");
            }

            Console.WriteLine($"Resource compiler: {resolution.CompilerPath}");
            Console.WriteLine($"Input: {options.InputPath}");
            Console.WriteLine($"Gameinfo: {resolution.GameInfoPath ?? "native auto-detection"}");
            Console.WriteLine($"Output: {options.OutputDirectory ?? "input directory"}");
            if (options.DryRun)
            {
                var dryRunStaging = RequiresStaging(options, resolution);
                var dryRunArguments = BuildArguments(
                    options,
                    resolution,
                    GetDryRunInputPath(options, resolution),
                    dryRunStaging);
                Console.WriteLine($"Command: {FormatCommand(resolution.CompilerPath, dryRunArguments)}");
                if (RequiresStaging(options, resolution))
                {
                    Console.WriteLine("Input will be staged under the CS2 content directory and compiled outputs will be copied to the selected output directory");
                }

                Console.WriteLine("Dry run complete; resource compilation was not started");
                return 0;
            }

            using var workspace = CompileInputWorkspace.Create(options, resolution);
            var arguments = BuildArguments(options, resolution, workspace.CompilerInputPath, workspace.IsStaged);
            var exitCode = ResourceCompilerProcess.Run(resolution.CompilerPath, arguments);
            if (exitCode == 0 && workspace.IsStaged)
            {
                workspace.CopyCompiledFilesBack();
                Console.WriteLine($"Compiled files copied to: {workspace.OutputDirectory}");
            }
            else if (exitCode != 0)
            {
                Console.Error.WriteLine($"resourcecompiler.exe failed with exit code {exitCode}");
            }

            return exitCode;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Error: {exception.Message}");
            return 1;
        }
    }

    private static IReadOnlyList<string> BuildArguments(
        CompileOptions options,
        ResourceCompilerResolution resolution,
        string inputPath,
        bool isStaged = false)
    {
        var arguments = new List<string> { "-i", inputPath };
        if (options.Recursive)
        {
            arguments.Add("-r");
        }

        if (options.Force)
        {
            arguments.Add("-f");
        }

        if (options.NoVpk || isStaged)
        {
            arguments.Add("-novpk");
        }

        if (options.NoP4)
        {
            arguments.Add("-nop4");
        }

        if (options.Verbose)
        {
            arguments.Add("-v");
        }

        if (resolution.GameInfoPath is not null)
        {
            arguments.Add("-game");
            arguments.Add(ResourceCompilerLocator.GetGameDirectory(resolution));
        }

        return arguments;
    }

    private static string GetDryRunInputPath(CompileOptions options, ResourceCompilerResolution resolution)
    {
        if (!RequiresStaging(options, resolution))
        {
            return Directory.Exists(options.InputPath)
                ? Path.Combine(options.InputPath, "*")
                : options.InputPath;
        }

        var moduleContentDirectory = Path.Combine(
            resolution.Cs2Root!,
            "content",
            Path.GetRelativePath(Path.Combine(resolution.Cs2Root!, "game"), ResourceCompilerLocator.GetGameDirectory(resolution)));
        var placeholder = Path.Combine(moduleContentDirectory, ".source2vpktools_<temporary>");
        return Directory.Exists(options.InputPath) ? Path.Combine(placeholder, "*") : Path.Combine(placeholder, Path.GetFileName(options.InputPath));
    }

    private static bool RequiresStaging(CompileOptions options, ResourceCompilerResolution resolution)
    {
        if (resolution.Cs2Root is null)
        {
            return false;
        }

        if (options.OutputDirectory is not null)
        {
            return true;
        }

        var contentDirectory = Path.Combine(resolution.Cs2Root, "content");
        var relativePath = Path.GetRelativePath(contentDirectory, options.InputPath);
        return relativePath is not "." &&
               (relativePath.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relativePath));
    }

    private static string FormatCommand(string compilerPath, IReadOnlyList<string> arguments) =>
        string.Join(' ', new[] { QuoteArgument(compilerPath) }.Concat(arguments.Select(QuoteArgument)));

    private static string QuoteArgument(string argument) =>
        argument.Contains(' ', StringComparison.Ordinal) || argument.Contains('*')
            ? $"\"{argument.Replace("\"", "\\\"", StringComparison.Ordinal)}\""
            : argument;
}

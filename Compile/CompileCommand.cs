namespace Source2VpkTools;

internal static class CompileCommand
{
    public static int Run(CompileOptions options)
    {
        try
        {
            var resolution = ResourceCompilerLocator.Resolve(options);
            if ((options.OutputDirectory is not null || options.ContentRoot is not null) && resolution.Cs2Root is null)
            {
                throw new InvalidOperationException("指定编译输出目录或内容根时必须能解析 CS2 根目录");
            }

            Console.WriteLine($"资源编译器: {resolution.CompilerPath}");
            Console.WriteLine($"输入: {options.InputPath}");
            Console.WriteLine($"游戏配置: {resolution.GameInfoPath ?? "由原生编译器自动检测"}");
            Console.WriteLine($"输出: {options.OutputDirectory ?? "输入所在目录"}");
            if (options.DryRun)
            {
                var dryRunStaging = RequiresStaging(options, resolution);
                var dryRunRoot = dryRunStaging
                    ? CompileInputWorkspace.GetInputRoot(options.InputPath, resolution, options.ContentRoot)
                    : null;
                var directoryInput = Directory.Exists(options.InputPath);
                var dryRunInput = directoryInput
                    ? Path.Combine(Path.GetTempPath(), ".source2vpktools_<temporary>.txt")
                    : GetDryRunFilePath(options, resolution, dryRunRoot);
                var dryRunGameDirectory = dryRunStaging
                    ? Path.Combine(resolution.Cs2Root!, "game", ".source2vpktools_<temporary>")
                    : null;
                var dryRunArguments = BuildArguments(
                    options,
                    resolution,
                    dryRunInput,
                    dryRunStaging,
                    dryRunGameDirectory,
                    directoryInput);
                Console.WriteLine($"命令: {FormatCommand(resolution.CompilerPath, dryRunArguments)}");
                if (dryRunStaging)
                {
                    Console.WriteLine("输入将使用独立的 content/game 编译模块及专用 gameinfo.gi, 保留原游戏搜索路径");
                }
                if (directoryInput)
                {
                    Console.WriteLine($"输入清单: {CompileInputs.Enumerate(options.InputPath, options.Recursive).Length} 个源文件; 纹理和模型依赖不作为独立编译入口");
                }

                Console.WriteLine("试运行完成, 未启动资源编译");
                return 0;
            }

            using var workspace = CompileInputWorkspace.Create(options, resolution);
            var useFileList = Directory.Exists(options.InputPath);
            var compilerInput = useFileList ? workspace.CreateCompilerFileList(options.Recursive) : workspace.CompilerInputPath;
            var arguments = BuildArguments(options, resolution, compilerInput, workspace.IsStaged, workspace.GameDirectory, useFileList);
            var exitCode = ResourceCompilerProcess.Run(resolution.CompilerPath, arguments);
            if (exitCode == 0 && workspace.IsStaged)
            {
                workspace.CopyCompiledFilesBack();
                Console.WriteLine($"编译文件已复制到: {workspace.OutputDirectory}");
            }
            else if (exitCode != 0)
            {
                Console.Error.WriteLine($"resourcecompiler.exe 编译失败, 退出码: {exitCode}");
            }

            return exitCode;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"错误: {exception.Message}");
            return 1;
        }
    }

    private static IReadOnlyList<string> BuildArguments(
        CompileOptions options,
        ResourceCompilerResolution resolution,
        string inputPath,
        bool isStaged = false,
        string? gameDirectory = null,
        bool useFileList = false)
    {
        var arguments = new List<string> { useFileList ? "-filelist" : "-i", inputPath };
        if (options.Recursive && !useFileList)
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
            arguments.Add(gameDirectory ?? ResourceCompilerLocator.GetGameDirectory(resolution));
        }

        return arguments;
    }

    private static string GetDryRunFilePath(CompileOptions options, ResourceCompilerResolution resolution, string? inputRoot)
    {
        if (inputRoot is null)
        {
            return options.InputPath;
        }

        var placeholder = Path.Combine(resolution.Cs2Root!, "content", ".source2vpktools_<temporary>");
        return Path.Combine(placeholder, Path.GetRelativePath(inputRoot, options.InputPath));
    }

    private static bool RequiresStaging(CompileOptions options, ResourceCompilerResolution resolution)
    {
        if (resolution.Cs2Root is null)
        {
            return false;
        }

        if (options.OutputDirectory is not null || options.ContentRoot is not null)
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

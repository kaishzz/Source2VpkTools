namespace Source2VpkTools;

internal sealed class CompileInputWorkspace : IDisposable
{
    private readonly string? stagedContentDirectory;
    private readonly string? stagedGameDirectory;
    private string? compilerFileListPath;

    private CompileInputWorkspace(
        string compilerInputPath,
        string outputDirectory,
        bool isStaged,
        string? stagedContentDirectory,
        string? stagedGameDirectory,
        string? gameDirectory = null)
    {
        CompilerInputPath = compilerInputPath;
        OutputDirectory = outputDirectory;
        IsStaged = isStaged;
        this.stagedContentDirectory = stagedContentDirectory;
        this.stagedGameDirectory = stagedGameDirectory;
        GameDirectory = gameDirectory;
    }

    public string CompilerInputPath { get; }

    public string OutputDirectory { get; }

    public bool IsStaged { get; }

    public string? GameDirectory { get; }

    public string CreateCompilerFileList(bool recursive)
    {
        var directory = Path.GetDirectoryName(CompilerInputPath)!;
        var files = CompileInputs.Enumerate(directory, recursive);
        if (files.Length == 0)
        {
            throw new InvalidOperationException($"目录中没有可编译的源文件: {directory}");
        }

        compilerFileListPath ??= Path.Combine(Path.GetTempPath(), $".source2vpktools_{Guid.NewGuid():N}.txt");
        File.WriteAllLines(compilerFileListPath, files);
        return compilerFileListPath;
    }

    public static CompileInputWorkspace Create(CompileOptions options, ResourceCompilerResolution resolution)
    {
        var outputDirectory = options.OutputDirectory ?? (Directory.Exists(options.InputPath)
            ? options.InputPath
            : Path.GetDirectoryName(options.InputPath)!);
        outputDirectory = Path.GetFullPath(outputDirectory);

        if (options.OutputDirectory is not null &&
            string.Equals(outputDirectory, Path.GetFullPath(options.InputPath), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("编译输出目录不能与输入目录相同");
        }

        if ((options.OutputDirectory is not null || options.ContentRoot is not null) && resolution.Cs2Root is null)
        {
            throw new InvalidOperationException("指定编译输出目录或内容根时必须能解析 CS2 根目录");
        }

        if (resolution.Cs2Root is null)
        {
            return new CompileInputWorkspace(
                GetCompilerInputPath(options.InputPath),
                outputDirectory,
                false,
                null,
                null,
                resolution.GameInfoPath is null ? null : ResourceCompilerLocator.GetGameDirectory(resolution));
        }

        var contentDirectory = GetContentDirectory(resolution);
        if (options.OutputDirectory is null && options.ContentRoot is null && IsWithinDirectory(options.InputPath, contentDirectory))
        {
            return new CompileInputWorkspace(
                GetCompilerInputPath(options.InputPath),
                outputDirectory,
                false,
                null,
                null,
                resolution.GameInfoPath is null ? null : ResourceCompilerLocator.GetGameDirectory(resolution));
        }

        if (resolution.GameInfoPath is null)
        {
            throw new InvalidOperationException(
                "编译 CS2 content 目录外的输入时必须能解析 gameinfo.gi");
        }

        var inputRoot = GetInputRoot(options.InputPath, resolution, options.ContentRoot);
        var stageName = $".source2vpktools_{Guid.NewGuid():N}";
        var stagedContentDirectory = Path.Combine(resolution.Cs2Root, "content", stageName);
        var stagedGameDirectory = Path.Combine(resolution.Cs2Root, "game", stageName);

        if (IsWithinDirectory(stagedContentDirectory, inputRoot))
        {
            throw new InvalidOperationException("内容根包含编译暂存目录, 请指定更具体的 --content-root");
        }

        Directory.CreateDirectory(stagedContentDirectory);
        try
        {
            var excludedOutputDirectory = options.OutputDirectory is not null &&
                                          !IsWithinDirectory(options.InputPath, outputDirectory)
                ? outputDirectory
                : null;
            CopyContentDirectory(inputRoot, stagedContentDirectory, excludedOutputDirectory);
            Directory.CreateDirectory(stagedGameDirectory);
            CompileGameInfo.Write(resolution.GameInfoPath, stagedGameDirectory);
            var stagedInput = Path.Combine(stagedContentDirectory, Path.GetRelativePath(inputRoot, options.InputPath));
            return new CompileInputWorkspace(
                GetCompilerInputPath(options.InputPath, stagedInput),
                outputDirectory,
                true,
                stagedContentDirectory,
                stagedGameDirectory,
                stagedGameDirectory);
        }
        catch
        {
            DeleteDirectory(stagedContentDirectory);
            DeleteDirectory(stagedGameDirectory);
            throw;
        }
    }

    public void CopyCompiledFilesBack()
    {
        if (!IsStaged || stagedGameDirectory is null)
        {
            return;
        }

        var relativeInput = Path.GetRelativePath(stagedContentDirectory!, CompilerInputPath);
        var compiledDirectory = Path.Combine(stagedGameDirectory, Path.GetDirectoryName(relativeInput)!);
        if (!Directory.Exists(compiledDirectory))
        {
            throw new DirectoryNotFoundException(
                $"resourcecompiler.exe 已结束, 但未生成预期的输出目录: {compiledDirectory}");
        }

        Directory.CreateDirectory(OutputDirectory);

        foreach (var compiledFile in Directory.EnumerateFiles(compiledDirectory, "*", SearchOption.AllDirectories)
                     .Where(IsCompiledResource))
        {
            var relativePath = Path.GetRelativePath(compiledDirectory, compiledFile);
            var destinationPath = Path.Combine(OutputDirectory, relativePath);
            var destinationDirectory = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrWhiteSpace(destinationDirectory))
            {
                Directory.CreateDirectory(destinationDirectory);
            }

            File.Copy(compiledFile, destinationPath, overwrite: true);
        }
    }

    public void Dispose()
    {
        if (compilerFileListPath is not null)
        {
            File.Delete(compilerFileListPath);
        }
        DeleteDirectory(stagedContentDirectory);
        DeleteDirectory(stagedGameDirectory);
    }

    private static string GetContentDirectory(ResourceCompilerResolution resolution) =>
        Path.Combine(resolution.Cs2Root!, "content");

    private static string GetModuleContentDirectory(ResourceCompilerResolution resolution)
    {
        var gameDirectory = ResourceCompilerLocator.GetGameDirectory(resolution);
        var relativeModulePath = Path.GetRelativePath(Path.Combine(resolution.Cs2Root!, "game"), gameDirectory);
        if (relativeModulePath.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relativeModulePath))
        {
            throw new InvalidOperationException(
                $"gameinfo.gi 所在目录必须位于 CS2 game 目录内: {gameDirectory}");
        }

        return Path.Combine(resolution.Cs2Root!, "content", relativeModulePath);
    }

    internal static string GetInputRoot(string inputPath, ResourceCompilerResolution resolution, string? contentRoot = null)
    {
        if (contentRoot is not null)
        {
            var root = Path.GetFullPath(contentRoot);
            if (!Directory.Exists(root))
            {
                throw new DirectoryNotFoundException($"内容根目录不存在: {root}");
            }
            if (!IsWithinDirectory(inputPath, root))
            {
                throw new InvalidOperationException($"输入必须位于指定的内容根目录内: {root}");
            }
            return root;
        }

        var moduleRoot = GetModuleContentDirectory(resolution);
        if (IsWithinDirectory(inputPath, moduleRoot))
        {
            return moduleRoot;
        }

        if (Directory.Exists(inputPath))
        {
            return Path.GetFullPath(inputPath);
        }

        throw new InvalidOperationException("外部单文件输入必须使用 --content-root 指定资源内容根目录");
    }

    private static string GetCompilerInputPath(string inputPath, string? stagedDirectory = null)
    {
        var basePath = stagedDirectory ?? inputPath;
        return Directory.Exists(inputPath) ? Path.Combine(basePath, "*") : basePath;
    }

    private static void CopyContentDirectory(string inputRoot, string stagedDirectory, string? excludedOutputDirectory)
    {
        foreach (var sourceFile in Directory.EnumerateFiles(inputRoot, "*", SearchOption.AllDirectories)
                     .Where(file => !IsCompiledResource(file) &&
                                    (excludedOutputDirectory is null || !IsWithinDirectory(file, excludedOutputDirectory))))
        {
            var relativePath = Path.GetRelativePath(inputRoot, sourceFile);
            var destinationPath = Path.Combine(stagedDirectory, relativePath);
            var destinationDirectory = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrWhiteSpace(destinationDirectory))
            {
                Directory.CreateDirectory(destinationDirectory);
            }

            File.Copy(sourceFile, destinationPath);
        }
    }

    private static bool IsCompiledResource(string path) =>
        Path.GetFileName(path).EndsWith("_c", StringComparison.OrdinalIgnoreCase);

    private static bool IsWithinDirectory(string path, string directory)
    {
        var relativePath = Path.GetRelativePath(directory, path);
        return relativePath is "." ||
               (!relativePath.StartsWith("..", StringComparison.Ordinal) && !Path.IsPathRooted(relativePath));
    }

    private static void DeleteDirectory(string? path)
    {
        if (path is not null && Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
}

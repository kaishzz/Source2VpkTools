namespace Source2VpkTools;

internal sealed class CompileInputWorkspace : IDisposable
{
    private readonly string? stagedContentDirectory;
    private readonly string? stagedGameDirectory;

    private CompileInputWorkspace(
        string compilerInputPath,
        string outputDirectory,
        bool isStaged,
        string? stagedContentDirectory,
        string? stagedGameDirectory)
    {
        CompilerInputPath = compilerInputPath;
        OutputDirectory = outputDirectory;
        IsStaged = isStaged;
        this.stagedContentDirectory = stagedContentDirectory;
        this.stagedGameDirectory = stagedGameDirectory;
    }

    public string CompilerInputPath { get; }

    public string OutputDirectory { get; }

    public bool IsStaged { get; }

    public static CompileInputWorkspace Create(CompileOptions options, ResourceCompilerResolution resolution)
    {
        var outputDirectory = Directory.Exists(options.InputPath)
            ? options.InputPath
            : Path.GetDirectoryName(options.InputPath)!;

        if (resolution.Cs2Root is null)
        {
            return new CompileInputWorkspace(
                GetCompilerInputPath(options.InputPath),
                outputDirectory,
                false,
                null,
                null);
        }

        var contentDirectory = GetContentDirectory(resolution);
        if (IsWithinDirectory(options.InputPath, contentDirectory))
        {
            return new CompileInputWorkspace(
                GetCompilerInputPath(options.InputPath),
                outputDirectory,
                false,
                null,
                null);
        }

        if (resolution.GameInfoPath is null)
        {
            throw new InvalidOperationException(
                "An input outside the CS2 content directory requires a resolvable gameinfo.gi for temporary staging");
        }

        var moduleContentDirectory = GetModuleContentDirectory(resolution);
        var stageName = $".source2vpktools_{Guid.NewGuid():N}";
        var stagedContentDirectory = Path.Combine(moduleContentDirectory, stageName);
        var stagedGameDirectory = Path.Combine(ResourceCompilerLocator.GetGameDirectory(resolution), stageName);

        Directory.CreateDirectory(stagedContentDirectory);
        try
        {
            CopyInput(options.InputPath, stagedContentDirectory);
            return new CompileInputWorkspace(
                GetCompilerInputPath(options.InputPath, stagedContentDirectory),
                outputDirectory,
                true,
                stagedContentDirectory,
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

        if (!Directory.Exists(stagedGameDirectory))
        {
            throw new DirectoryNotFoundException(
                $"resourcecompiler.exe completed without producing the expected output directory: {stagedGameDirectory}");
        }

        foreach (var compiledFile in Directory.EnumerateFiles(stagedGameDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(stagedGameDirectory, compiledFile);
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
                $"The gameinfo.gi directory must be inside the CS2 game directory: {gameDirectory}");
        }

        return Path.Combine(resolution.Cs2Root!, "content", relativeModulePath);
    }

    private static string GetCompilerInputPath(string inputPath, string? stagedDirectory = null)
    {
        var basePath = stagedDirectory ?? inputPath;
        return Directory.Exists(inputPath) ? Path.Combine(basePath, "*") : basePath;
    }

    private static void CopyInput(string inputPath, string stagedDirectory)
    {
        if (Directory.Exists(inputPath))
        {
            foreach (var sourceFile in Directory.EnumerateFiles(inputPath, "*", SearchOption.AllDirectories)
                         .Where(static file => !IsCompiledResource(file)))
            {
                var relativePath = Path.GetRelativePath(inputPath, sourceFile);
                var destinationPath = Path.Combine(stagedDirectory, relativePath);
                var destinationDirectory = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrWhiteSpace(destinationDirectory))
                {
                    Directory.CreateDirectory(destinationDirectory);
                }

                File.Copy(sourceFile, destinationPath);
            }

            return;
        }

        File.Copy(inputPath, Path.Combine(stagedDirectory, Path.GetFileName(inputPath)));
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

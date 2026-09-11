namespace Source2VpkTools;

internal sealed record ResourceCompilerResolution(
    string CompilerPath,
    string? Cs2Root,
    string? GameInfoPath);

internal static class ResourceCompilerLocator
{
    public static ResourceCompilerResolution Resolve(CompileOptions options)
    {
        string compilerPath;
        string? cs2Root;

        if (options.ResourceCompilerPath is not null)
        {
            compilerPath = Path.GetFullPath(options.ResourceCompilerPath);
            if (!File.Exists(compilerPath))
            {
                throw new FileNotFoundException($"The resource compiler was not found: {compilerPath}", compilerPath);
            }

            cs2Root = options.Cs2Root is null ? TryDeriveCs2Root(compilerPath) : Cs2InstallationLocator.Validate(options.Cs2Root);
        }
        else
        {
            cs2Root = options.Cs2Root is null
                ? Cs2InstallationLocator.Find()
                : Cs2InstallationLocator.Validate(options.Cs2Root);
            compilerPath = Cs2InstallationLocator.GetResourceCompilerPath(cs2Root);
            if (!File.Exists(compilerPath))
            {
                throw new FileNotFoundException($"resourcecompiler.exe was not found under the CS2 root: {cs2Root}", compilerPath);
            }
        }

        var gameInfoPath = options.GameInfoPath is null
            ? FindGameInfo(options.InputPath, cs2Root)
            : Path.GetFullPath(options.GameInfoPath);
        if (gameInfoPath is not null && !File.Exists(gameInfoPath))
        {
            throw new FileNotFoundException($"The gameinfo.gi file was not found: {gameInfoPath}", gameInfoPath);
        }

        return new ResourceCompilerResolution(compilerPath, cs2Root, gameInfoPath);
    }

    public static string GetGameDirectory(ResourceCompilerResolution resolution)
    {
        if (resolution.GameInfoPath is null)
        {
            throw new InvalidOperationException("A gameinfo.gi path is required to invoke resourcecompiler.exe");
        }

        var gameDirectory = Path.GetDirectoryName(resolution.GameInfoPath);
        if (string.IsNullOrWhiteSpace(gameDirectory))
        {
            throw new InvalidOperationException($"Could not determine the game directory from: {resolution.GameInfoPath}");
        }

        return gameDirectory;
    }

    private static string? TryDeriveCs2Root(string compilerPath)
    {
        var compilerDirectory = Directory.GetParent(compilerPath);
        if (compilerDirectory?.Name.Equals("win64", StringComparison.OrdinalIgnoreCase) != true)
        {
            return null;
        }

        var root = compilerDirectory.Parent?.Parent?.Parent?.FullName;
        return root is not null && Directory.Exists(root) ? root : null;
    }

    private static string? FindGameInfo(string inputPath, string? cs2Root)
    {
        if (cs2Root is null)
        {
            return null;
        }

        var contentRoot = Path.Combine(cs2Root, "content");
        var relativePath = Path.GetRelativePath(contentRoot, inputPath);
        var segments = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length >= 2 && segments[0].Equals("csgo_addons", StringComparison.OrdinalIgnoreCase))
        {
            var addonGameInfo = Path.Combine(cs2Root, "game", "csgo_addons", segments[1], "gameinfo.gi");
            if (File.Exists(addonGameInfo))
            {
                return addonGameInfo;
            }
        }

        var csgoGameInfo = Path.Combine(cs2Root, "game", "csgo", "gameinfo.gi");
        return File.Exists(csgoGameInfo) ? csgoGameInfo : null;
    }
}

using Microsoft.Win32;
using System.Text.RegularExpressions;

namespace Source2VpkTools;

internal static class Cs2InstallationLocator
{
    private const string DefaultInstallDirectory = "Counter-Strike Global Offensive";

    public static string Find()
    {
        foreach (var steamRoot in EnumerateSteamRoots())
        {
            foreach (var libraryRoot in EnumerateLibraryRoots(steamRoot))
            {
                var installDirectory = FindCs2Directory(libraryRoot);
                if (installDirectory is not null)
                {
                    return installDirectory;
                }
            }
        }

        throw new DirectoryNotFoundException(
            "Could not locate a CS2 installation through Steam AppID 730. Specify --cs2-root or --resource-compiler explicitly");
    }

    public static string Validate(string root)
    {
        var fullRoot = Path.GetFullPath(root);
        if (!Directory.Exists(fullRoot))
        {
            throw new DirectoryNotFoundException($"The CS2 root directory was not found: {fullRoot}");
        }
        return fullRoot;
    }

    public static string GetResourceCompilerPath(string cs2Root) =>
        Path.Combine(cs2Root, "game", "bin", "win64", "resourcecompiler.exe");

    private static string? FindCs2Directory(string libraryRoot)
    {
        var steamApps = Path.Combine(libraryRoot, "steamapps");
        var manifestPath = Path.Combine(steamApps, "appmanifest_730.acf");
        if (File.Exists(manifestPath))
        {
            var installDirectory = ReadValue(manifestPath, "installdir") ?? DefaultInstallDirectory;
            var candidate = Path.Combine(steamApps, "common", installDirectory);
            if (File.Exists(GetResourceCompilerPath(candidate)))
            {
                return candidate;
            }
        }

        var fallback = Path.Combine(steamApps, "common", DefaultInstallDirectory);
        return File.Exists(GetResourceCompilerPath(fallback)) ? fallback : null;
    }

    private static IEnumerable<string> EnumerateSteamRoots()
    {
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (OperatingSystem.IsWindows())
        {
            AddRoot(roots, Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) as string);
            AddRoot(roots, Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Valve\Steam", "InstallPath", null) as string);
            AddRoot(roots, Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath", null) as string);
        }

        AddRoot(roots, Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam");
        AddRoot(roots, Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Steam");
        return roots;
    }

    private static IEnumerable<string> EnumerateLibraryRoots(string steamRoot)
    {
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { steamRoot };
        var libraryFile = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
        if (File.Exists(libraryFile))
        {
            foreach (Match match in Regex.Matches(File.ReadAllText(libraryFile), "\\\"path\\\"\\s+\\\"(?<path>[^\\\"]+)\\\"", RegexOptions.IgnoreCase))
            {
                var path = match.Groups["path"].Value.Replace("\\\\", "\\", StringComparison.Ordinal);
                if (!string.IsNullOrWhiteSpace(path))
                {
                    roots.Add(path);
                }
            }
        }

        return roots;
    }

    private static string? ReadValue(string path, string key)
    {
        var pattern = $"\\\"{Regex.Escape(key)}\\\"\\s+\\\"(?<value>[^\\\"]+)\\\"";
        return Regex.Match(File.ReadAllText(path), pattern, RegexOptions.IgnoreCase).Groups["value"].Value.Trim() switch
        {
            { Length: > 0 } value => value,
            _ => null
        };
    }

    private static void AddRoot(ISet<string> roots, string? root, string? child = null)
    {
        if (string.IsNullOrWhiteSpace(root))
        {
            return;
        }

        var candidate = child is null ? root : Path.Combine(root, child);
        if (Directory.Exists(candidate))
        {
            roots.Add(Path.GetFullPath(candidate));
        }
    }
}

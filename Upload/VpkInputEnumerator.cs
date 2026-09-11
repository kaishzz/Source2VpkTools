namespace Source2VpkDump;

internal static class VpkInputEnumerator
{
    public static IReadOnlyList<string> Enumerate(string vpkPath)
    {
        var fullPath = Path.GetFullPath(vpkPath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"The VPK file was not found: {fullPath}", fullPath);
        }

        if (!fullPath.EndsWith(".vpk", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"The direct upload input must be a .vpk file: {fullPath}");
        }

        EnsureNotReparsePoint(fullPath);
        var files = new List<string> { fullPath };
        var stem = Path.GetFileNameWithoutExtension(fullPath);
        if (!stem.EndsWith("_dir", StringComparison.OrdinalIgnoreCase))
        {
            return files;
        }

        var directory = Path.GetDirectoryName(fullPath)!;
        var baseName = stem[..^"_dir".Length];
        var chunks = new List<(ulong Index, string Path)>();
        foreach (var candidate in Directory.EnumerateFiles(directory, "*.vpk"))
        {
            var candidateStem = Path.GetFileNameWithoutExtension(candidate);
            var prefix = $"{baseName}_";
            if (!candidateStem.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var suffix = candidateStem[prefix.Length..];
            if (suffix.Length == 0 || suffix.Any(static character => !char.IsDigit(character)) ||
                !ulong.TryParse(suffix, out var index))
            {
                continue;
            }

            EnsureNotReparsePoint(candidate);
            chunks.Add((index, Path.GetFullPath(candidate)));
        }

        var duplicateIndex = chunks
            .GroupBy(static chunk => chunk.Index)
            .FirstOrDefault(static group => group.Count() > 1);
        if (duplicateIndex is not null)
        {
            throw new InvalidDataException($"The VPK directory file has duplicate chunk index {duplicateIndex.Key}");
        }

        files.AddRange(chunks
            .OrderBy(static chunk => chunk.Index)
            .Select(static chunk => chunk.Path));
        return files;
    }

    private static void EnsureNotReparsePoint(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException($"The direct upload VPK is a reparse point and is not supported: {path}");
        }
    }
}

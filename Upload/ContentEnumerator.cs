namespace Source2VpkTools;

internal static class ContentEnumerator
{
    public static IReadOnlyList<ContentFile> Enumerate(string contentRoot)
    {
        var root = Path.GetFullPath(contentRoot);
        if (!Directory.Exists(root))
        {
            throw new DirectoryNotFoundException($"The content directory was not found: {root}");
        }

        EnsureNotReparsePoint(root, "directory");
        var rootPrefix = EnsureDirectoryPrefix(root);
        var files = new List<ContentFile>();
        var directories = new Stack<string>();
        directories.Push(root);

        while (directories.Count > 0)
        {
            var directory = directories.Pop();
            foreach (var childDirectory in Directory.EnumerateDirectories(directory))
            {
                EnsureNotReparsePoint(childDirectory, "directory");
                directories.Push(childDirectory);
            }

            foreach (var filePath in Directory.EnumerateFiles(directory))
            {
                EnsureNotReparsePoint(filePath, "file");
                var fullPath = Path.GetFullPath(filePath);
                if (!fullPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException($"The content file escapes the selected root: {filePath}");
                }

                var relativePath = NormalizeRelativePath(Path.GetRelativePath(root, fullPath));
                files.Add(ContentFile.FromPath(fullPath, relativePath));
            }
        }

        return files.OrderBy(static file => file.RelativePath, StringComparer.Ordinal).ToArray();
    }

    private static string EnsureDirectoryPrefix(string path)
    {
        return path.EndsWith(Path.DirectorySeparatorChar)
            ? path
            : path + Path.DirectorySeparatorChar;
    }

    private static void EnsureNotReparsePoint(string path, string kind)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException($"The content {kind} is a reparse point and is not supported: {path}");
        }
    }

    private static string NormalizeRelativePath(string path)
    {
        var normalized = path.Replace('\\', '/');
        if (string.IsNullOrWhiteSpace(normalized) || Path.IsPathRooted(normalized))
        {
            throw new InvalidDataException($"The content path is invalid: {path}");
        }

        var segments = normalized.Split('/');
        if (segments.Any(static segment => segment is "" or "." or ".."))
        {
            throw new InvalidDataException($"The content path contains an unsafe segment: {path}");
        }

        return normalized;
    }

}

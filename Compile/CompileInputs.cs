namespace Source2VpkTools;

internal static class CompileInputs
{
    private static readonly HashSet<string> DependencyExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".tga", ".tif", ".tiff", ".psd", ".exr", ".hdr", ".bmp",
        ".dds", ".fbx", ".obj", ".smd", ".blend", ".txt"
    };

    public static string[] Enumerate(string directoryPath, bool recursive)
    {
        var files = Directory.EnumerateFiles(directoryPath, "*", recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
        return files.Where(path => !Path.GetFileName(path).EndsWith("_c", StringComparison.OrdinalIgnoreCase) &&
                                   !DependencyExtensions.Contains(Path.GetExtension(path)))
            .Order(StringComparer.OrdinalIgnoreCase).ToArray();
    }
}

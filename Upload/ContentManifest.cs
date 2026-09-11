namespace Source2VpkTools;

internal sealed record ContentManifest(
    string SourceRoot,
    IReadOnlyList<ContentFile> Files,
    long TotalBytes)
{
    public static ContentManifest Create(string sourceRoot, IReadOnlyList<ContentFile> files)
    {
        return new ContentManifest(
            Path.GetFullPath(sourceRoot),
            files,
            files.Sum(static file => file.Length));
    }
}

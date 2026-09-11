namespace Source2VpkDump;

internal sealed class UploadStaging : IDisposable
{
    private UploadStaging(string rootDirectory, string contentDirectory, string? packagePath, string manifestPath)
    {
        RootDirectory = rootDirectory;
        ContentDirectory = contentDirectory;
        PackagePath = packagePath;
        ManifestPath = manifestPath;
    }

    public string RootDirectory { get; }

    public string ContentDirectory { get; }

    public string? PackagePath { get; }

    public string ManifestPath { get; }

    public static UploadStaging Create(string packageName)
    {
        return CreateCore(packageName);
    }

    public static UploadStaging CreateDirect()
    {
        return CreateCore(null);
    }

    private static UploadStaging CreateCore(string? packageName)
    {
        var rootDirectory = Path.Combine(Path.GetTempPath(), "Source2VpkDump", "upload", Guid.NewGuid().ToString("N"));
        var contentDirectory = Path.Combine(rootDirectory, "content");
        Directory.CreateDirectory(contentDirectory);

        var packagePath = packageName is null ? null : Path.Combine(contentDirectory, packageName);
        var manifestPath = Path.Combine(rootDirectory, "upload-manifest.json");
        return new UploadStaging(rootDirectory, contentDirectory, packagePath, manifestPath);
    }

    public void Dispose()
    {
        if (!Directory.Exists(RootDirectory))
        {
            return;
        }

        Directory.Delete(RootDirectory, recursive: true);
    }
}

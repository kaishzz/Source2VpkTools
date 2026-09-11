using System.Text.Json;

namespace Source2VpkTools;

internal static class WorkshopStateStore
{
    public static void Save(string path, WorkshopUploadResult result, ContentManifest manifest)
    {
        var state = new
        {
            workshopId = result.WorkshopId,
            workshopUrl = $"https://steamcommunity.com/sharedfiles/filedetails/?id={result.WorkshopId}",
            sourceRoot = manifest.SourceRoot,
            fileCount = manifest.Files.Count,
            totalBytes = manifest.TotalBytes,
            files = manifest.Files.Select(static file => new
            {
                path = file.RelativePath,
                length = file.Length,
                sha256 = file.Sha256
            })
        };

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true }));
    }
}

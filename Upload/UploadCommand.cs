using System.Text;
using System.Text.Json;

namespace Source2VpkTools;

internal sealed class UploadPreparation : IDisposable
{
    public UploadPreparation(UploadStaging staging, ContentManifest manifest)
    {
        Staging = staging;
        Manifest = manifest;
    }

    public UploadStaging Staging { get; }

    public ContentManifest Manifest { get; }

    public void Dispose() => Staging.Dispose();
}

internal static class UploadCommand
{
    public static int Run(UploadOptions options)
    {
        UploadPreparation? preparation = null;
        try
        {
            preparation = Prepare(options);
            WritePreparationSummary(preparation);
            if (options.DryRun)
            {
                WriteDryRunSummary(options, preparation);
                return 0;
            }

            return Submit(options, preparation);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Error: {exception.Message}");
            if (preparation is not null && options.KeepStaging)
            {
                Console.Error.WriteLine($"Staging directory kept: {preparation.Staging.RootDirectory}");
            }

            return 1;
        }
        finally
        {
            if (preparation is not null && !options.KeepStaging)
            {
                preparation.Dispose();
            }
        }
    }

    internal static UploadPreparation Prepare(UploadOptions options)
    {
        if (options.VpkPath is not null)
        {
            return PrepareDirectVpk(options);
        }

        var files = ContentEnumerator.Enumerate(options.ContentDirectory!);
        var manifest = ContentManifest.Create(options.ContentDirectory!, files);
        var staging = UploadStaging.Create(options.PackageName!);
        try
        {
            File.WriteAllText(staging.ManifestPath, JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
            VpkPackager.Pack(staging.PackagePath!, manifest.Files, progress =>
            {
                Console.WriteLine($"Packing {progress.Path} ({progress.ProcessedBytes:N0}/{progress.TotalBytes:N0} bytes)");
            });

            var validation = VpkPackager.Validate(staging.PackagePath!, manifest);
            if (!validation.IsSuccess)
            {
                WriteValidationFailure(validation);
                throw new InvalidDataException("The generated VPK did not match the source manifest");
            }

            return new UploadPreparation(staging, manifest);
        }
        catch
        {
            if (options.KeepStaging)
            {
                Console.Error.WriteLine($"Staging directory kept: {staging.RootDirectory}");
            }
            else
            {
                staging.Dispose();
            }

            throw;
        }
    }

    private static UploadPreparation PrepareDirectVpk(UploadOptions options)
    {
        var staging = UploadStaging.CreateDirect();
        try
        {
            var stagedFiles = new List<ContentFile>();
            foreach (var sourcePath in VpkInputEnumerator.Enumerate(options.VpkPath!))
            {
                var fileName = Path.GetFileName(sourcePath);
                var stagedPath = Path.Combine(staging.ContentDirectory, fileName);
                File.Copy(sourcePath, stagedPath);
                stagedFiles.Add(ContentFile.FromPath(stagedPath, fileName));
            }

            var sourceRoot = Path.GetDirectoryName(Path.GetFullPath(options.VpkPath!))!;
            var manifest = ContentManifest.Create(sourceRoot, stagedFiles);
            File.WriteAllText(staging.ManifestPath, JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
            return new UploadPreparation(staging, manifest);
        }
        catch
        {
            if (options.KeepStaging)
            {
                Console.Error.WriteLine($"Staging directory kept: {staging.RootDirectory}");
            }
            else
            {
                staging.Dispose();
            }

            throw;
        }
    }

    internal static void WritePreparationSummary(UploadPreparation preparation)
    {
        Console.WriteLine($"Source: {preparation.Manifest.SourceRoot}");
        Console.WriteLine($"Files: {preparation.Manifest.Files.Count:N0}");
        Console.WriteLine($"Bytes: {preparation.Manifest.TotalBytes:N0}");
        if (preparation.Staging.PackagePath is { } packagePath)
        {
            Console.WriteLine($"Package: {packagePath}");
        }
        else
        {
            Console.WriteLine($"Direct VPK files: {string.Join(", ", preparation.Manifest.Files.Select(static file => file.RelativePath))}");
            Console.WriteLine("Direct VPK copy: OK");
            return;
        }
        Console.WriteLine("Package validation: OK");
    }

    internal static int Submit(UploadOptions options, UploadPreparation preparation)
    {
        var description = options.DescriptionFilePath is null
            ? string.Empty
            : File.ReadAllText(options.DescriptionFilePath, Encoding.UTF8);
        var request = new WorkshopUploadRequest(
            options.WorkshopId,
            preparation.Staging.ContentDirectory,
            options.PreviewPath,
            options.Title,
            description,
            options.ChangeNote,
            options.Visibility,
            options.Tags);
        var progress = new Progress<UploadProgress>(static uploadProgress =>
        {
            var path = string.IsNullOrEmpty(uploadProgress.Path) ? string.Empty : $" {uploadProgress.Path}";
            Console.WriteLine($"{uploadProgress.Stage}{path} ({uploadProgress.ProcessedBytes:N0}/{uploadProgress.TotalBytes:N0} bytes)");
        });
        var uploadResult = new SteamUgcUploader().UploadAsync(request, progress, CancellationToken.None).GetAwaiter().GetResult();
        if (!uploadResult.Success)
        {
            var workshopItem = uploadResult.WorkshopId == 0
                ? string.Empty
                : $" Workshop ID: {uploadResult.WorkshopId}, URL: https://steamcommunity.com/sharedfiles/filedetails/?id={uploadResult.WorkshopId}";
            throw new InvalidOperationException($"Steam Workshop upload failed: {uploadResult.SteamResultCode} ({uploadResult.SteamResultMessage}).{workshopItem}");
        }

        if (options.StateFilePath is not null)
        {
            WorkshopStateStore.Save(options.StateFilePath, uploadResult, preparation.Manifest);
            Console.WriteLine($"State: {options.StateFilePath}");
        }

        Console.WriteLine($"Workshop ID: {uploadResult.WorkshopId}");
        Console.WriteLine($"Workshop URL: https://steamcommunity.com/sharedfiles/filedetails/?id={uploadResult.WorkshopId}");
        Console.WriteLine(uploadResult.WasCreated ? "Workshop item created" : "Workshop item updated");
        return 0;
    }

    internal static void WriteDryRunSummary(UploadOptions options, UploadPreparation preparation)
    {
        Console.WriteLine("Dry run complete; Steam upload was not started");
        if (options.KeepStaging)
        {
            Console.WriteLine($"Staging directory kept: {preparation.Staging.RootDirectory}");
        }
    }

    private static void WriteValidationFailure(VpkValidationResult validation)
    {
        Console.Error.WriteLine("Package validation failed");
        WritePaths("Missing", validation.MissingPaths);
        WritePaths("Unexpected", validation.UnexpectedPaths);
        WritePaths("Size mismatch", validation.SizeMismatches);
        WritePaths("Hash mismatch", validation.HashMismatches);
    }

    private static void WritePaths(string label, IReadOnlyList<string> paths)
    {
        foreach (var path in paths)
        {
            Console.Error.WriteLine($"{label}: {path}");
        }
    }
}

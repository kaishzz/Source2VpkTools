using System.Security.Cryptography;
using ValvePak;

namespace Source2VpkDump;

internal static class VpkPackager
{
    private const long MaxArrayLength = int.MaxValue;

    public static void Pack(
        string outputPackagePath,
        IReadOnlyList<ContentFile> files,
        Action<UploadProgress>? progress = null)
    {
        if (files.Count == 0)
        {
            throw new InvalidDataException("The upload content directory does not contain any files");
        }

        var totalBytes = files.Sum(static file => file.Length);
        var availableMemory = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
        if (files.Any(static file => file.Length > MaxArrayLength))
        {
            throw new InvalidDataException("A source file is larger than the ValvePak byte-array limit");
        }

        if (availableMemory > 0 && totalBytes > availableMemory / 2)
        {
            throw new InvalidDataException($"The source content is too large for the available memory budget ({totalBytes:N0} bytes requested, approximately {availableMemory / 2:N0} bytes allowed)");
        }

        var useChunks = totalBytes > 1_500_000_000 || files.Any(static file => file.Length > 200_000_000);

        using var package = new Package
        {
            WriteChunkSize = 200 * 1024 * 1024
        };

        long processedBytes = 0;
        foreach (var file in files)
        {
            var bytes = File.ReadAllBytes(file.FullPath);
            package.AddFile(file.RelativePath, bytes, multiChunk: useChunks);
            processedBytes += bytes.LongLength;
            progress?.Invoke(new UploadProgress("Packing", file.RelativePath, processedBytes, totalBytes));
        }

        Directory.CreateDirectory(Path.GetDirectoryName(outputPackagePath)!);
        package.Write(outputPackagePath);
    }

    public static VpkValidationResult Validate(string packagePath, ContentManifest expected)
    {
        using var package = new Package();
        package.Read(packagePath);

        var actual = new Dictionary<string, (long Length, string Sha256)>(StringComparer.Ordinal);
        if (package.Entries is not { } entries)
        {
            throw new InvalidDataException("The generated VPK does not contain any entries");
        }

        foreach (var entryGroup in entries)
        {
            foreach (var entry in entryGroup.Value)
            {
                package.ReadEntry(entry, out var bytes, validateCrc: true);
                var path = entry.GetFullPath().Replace('\\', '/');
                actual[path] = (bytes.LongLength, Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
            }
        }

        var expectedByPath = expected.Files.ToDictionary(static file => file.RelativePath, StringComparer.Ordinal);
        var missing = expectedByPath.Keys.Except(actual.Keys, StringComparer.Ordinal).OrderBy(static path => path).ToArray();
        var unexpected = actual.Keys.Except(expectedByPath.Keys, StringComparer.Ordinal).OrderBy(static path => path).ToArray();
        var sizeMismatches = expectedByPath.Keys
            .Intersect(actual.Keys, StringComparer.Ordinal)
            .Where(path => expectedByPath[path].Length != actual[path].Length)
            .OrderBy(static path => path)
            .ToArray();
        var hashMismatches = expectedByPath.Keys
            .Intersect(actual.Keys, StringComparer.Ordinal)
            .Where(path => !string.Equals(expectedByPath[path].Sha256, actual[path].Sha256, StringComparison.OrdinalIgnoreCase))
            .OrderBy(static path => path)
            .ToArray();

        return new VpkValidationResult(missing, unexpected, sizeMismatches, hashMismatches);
    }
}

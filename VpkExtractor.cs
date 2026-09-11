using ValvePak;

namespace Source2VpkDump;

internal static class VpkExtractor
{
    public static ExtractionResult Extract(
        string inputPath,
        string outputDirectory,
        Action<ExtractionProgress> progress)
    {
        if (!File.Exists(inputPath))
        {
            throw new FileNotFoundException("The input VPK file was not found", inputPath);
        }

        var outputRoot = Path.GetFullPath(outputDirectory);
        Directory.CreateDirectory(outputRoot);
        var outputPrefix = outputRoot.EndsWith(Path.DirectorySeparatorChar)
            ? outputRoot
            : outputRoot + Path.DirectorySeparatorChar;

        using var package = new Package();
        package.Read(inputPath);

        if (package.Entries is not { } entries)
        {
            throw new InvalidDataException("The VPK does not contain any entries");
        }

        var entryCount = 0;
        long byteCount = 0;

        foreach (var entryGroup in entries)
        {
            foreach (var entry in entryGroup.Value)
            {
                var relativePath = NormalizeEntryPath(entry.GetFullPath());
                var destinationPath = Path.GetFullPath(Path.Combine(outputRoot, relativePath));

                if (!destinationPath.StartsWith(outputPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException($"The VPK entry path escapes the output directory: {relativePath}");
                }

                var destinationDirectory = Path.GetDirectoryName(destinationPath);
                if (destinationDirectory is null)
                {
                    throw new InvalidDataException($"The VPK entry has no parent directory: {relativePath}");
                }

                Directory.CreateDirectory(destinationDirectory);
                package.ReadEntry(entry, out var bytes, false);
                File.WriteAllBytes(destinationPath, bytes);

                entryCount++;
                byteCount += bytes.LongLength;
                progress(new ExtractionProgress(relativePath, bytes.LongLength));
            }
        }

        return new ExtractionResult(outputRoot, entryCount, byteCount);
    }

    private static string NormalizeEntryPath(string packagePath)
    {
        if (string.IsNullOrWhiteSpace(packagePath))
        {
            throw new InvalidDataException("The VPK contains an entry with an empty path");
        }

        var normalizedPath = packagePath.Replace('/', Path.DirectorySeparatorChar)
                                         .Replace('\\', Path.DirectorySeparatorChar);
        if (Path.IsPathRooted(normalizedPath))
        {
            throw new InvalidDataException($"The VPK contains an absolute entry path: {packagePath}");
        }

        return normalizedPath;
    }
}

internal sealed record ExtractionProgress(string EntryPath, long ByteCount);

internal sealed record ExtractionResult(string OutputDirectory, int EntryCount, long ByteCount);

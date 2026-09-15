using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using ValveKeyValue;
using ValvePak;
using ValveResourceFormat;
using ValveResourceFormat.ResourceTypes;
using ValveResourceFormat.Serialization.KeyValues;

namespace Source2VpkTools;

internal static class EntityDumpCommand
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = true
    };

    public static int Run(EntityDumpOptions options)
    {
        try
        {
            var result = Dump(options);
            Console.WriteLine($"Input: {result.InputPath}");
            Console.WriteLine($"Output: {result.OutputDirectory}");
            Console.WriteLine($"Map VPKs: {result.MapVpkCount:N0}");
            Console.WriteLine($"Entity lumps: {result.EntityLumpCount:N0}");
            Console.WriteLine($"Entities: {result.EntityCount:N0}");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Error: {exception.Message}");
            return 1;
        }
    }

    private static EntityDumpResult Dump(EntityDumpOptions options)
    {
        Directory.CreateDirectory(options.OutputDirectory);
        var allLumps = new Dictionary<string, Dictionary<string, List<JsonObject>>>(StringComparer.OrdinalIgnoreCase);
        var mapVpkCount = 0;
        var entityLumpCount = 0;
        var entityCount = 0;

        using var package = new Package();
        package.Read(options.InputPath);

        if (package.Entries is not { } entries || !entries.TryGetValue("vpk", out var mapEntries))
        {
            throw new InvalidDataException("The input VPK does not contain nested map VPK entries");
        }

        foreach (var mapEntry in mapEntries.OrderBy(static entry => entry.GetFullPath(), StringComparer.OrdinalIgnoreCase))
        {
            mapVpkCount++;
            package.ReadEntry(mapEntry, out var mapBytes, false);
            var mapPath = NormalizeRelativePath(mapEntry.GetFullPath());
            var mapKey = Path.ChangeExtension(mapPath, null)!.ToLowerInvariant();
            var mapOutputDirectory = GetOutputPath(options.OutputDirectory, mapKey);
            Directory.CreateDirectory(mapOutputDirectory);

            var lumps = new Dictionary<string, List<JsonObject>>(StringComparer.OrdinalIgnoreCase);
            using var mapStream = new MemoryStream(mapBytes, writable: false);
            using var mapPackage = new Package();
            mapPackage.SetFileName(Path.GetFileName(mapPath));
            mapPackage.Read(mapStream);

            if (mapPackage.Entries is { } mapEntriesInPackage && mapEntriesInPackage.TryGetValue("vents_c", out var lumpEntries))
            {
                foreach (var lumpEntry in lumpEntries.OrderBy(static entry => entry.GetFullPath(), StringComparer.OrdinalIgnoreCase))
                {
                    var entities = ReadEntities(mapPackage, lumpEntry);
                    var lumpPath = NormalizeRelativePath(lumpEntry.GetFullPath());
                    lumps[lumpPath] = entities;
                    entityLumpCount++;
                    entityCount += entities.Count;
                    var relativeLumpPath = RemoveMapPathPrefix(lumpPath, mapKey);
                    WriteJsonc(GetOutputPath(mapOutputDirectory, relativeLumpPath + ".jsonc"), entities);
                }
            }

            allLumps[mapKey] = lumps;
        }

        var inputName = Path.GetFileNameWithoutExtension(options.InputPath);
        WriteJsonc(Path.Combine(options.OutputDirectory, $"{inputName}_full.jsonc"), allLumps);
        return new EntityDumpResult(options.InputPath, options.OutputDirectory, mapVpkCount, entityLumpCount, entityCount);
    }

    private static List<JsonObject> ReadEntities(Package package, PackageEntry lumpEntry)
    {
        package.ReadEntry(lumpEntry, out var lumpBytes, false);
        using var stream = new MemoryStream(lumpBytes, writable: false);
        using var resource = new Resource
        {
            FileName = $"{lumpEntry.FileName}.{lumpEntry.TypeName}"
        };
        resource.Read(stream);
        if (resource.DataBlock is not EntityLump entityLump)
        {
            throw new InvalidDataException($"The entity lump is not an EntityLump resource: {lumpEntry.GetFullPath()}");
        }

        return entityLump.GetEntities().Select(ToEntityRecord).ToList();
    }

    private static JsonObject ToEntityRecord(EntityLump.Entity entity)
    {
        var classname = GetEntityProperty(entity, "classname") ??
            throw new InvalidDataException("The entity is missing an exact classname property");
        var result = new JsonObject
        {
            ["classname"] = JsonValue.Create(classname)
        };

        var hammerUniqueId = GetEntityProperty(entity, "hammerUniqueId");
        if (hammerUniqueId is not null)
        {
            result["hammerUniqueId"] = JsonValue.Create(hammerUniqueId);
        }

        var targetname = GetEntityProperty(entity, "targetname");
        if (targetname is not null)
        {
            result["targetname"] = JsonValue.Create(targetname);
        }

        foreach (var (key, value) in entity)
        {
            var outputKey = GetCanonicalEntityKey(key);
            if (outputKey is null || outputKey is "classname" or "hammerUniqueId" or "targetname" or "connections")
            {
                continue;
            }

            var outputValue = ToEntityValue(value);
            if (outputValue is not null)
            {
                result[outputKey] = JsonValue.Create(outputValue);
            }
        }

        if (entity.Connections is { Count: > 0 } connections)
        {
            var connectionArray = new JsonArray();
            foreach (var connection in connections)
            {
                connectionArray.Add(ToConnectionRecord(connection));
            }

            result["connections"] = connectionArray;
        }

        return result;
    }

    private static JsonObject ToConnectionRecord(EntityLump.Connection connection)
    {
        return new JsonObject
        {
            ["output"] = connection.OutputName ?? string.Empty,
            ["target"] = connection.TargetName ?? string.Empty,
            ["input"] = connection.InputName ?? string.Empty,
            ["param"] = connection.OverrideParam ?? string.Empty,
            ["delay"] = connection.Delay,
            ["limit"] = connection.TimesToFire
        };
    }

    private static string? GetEntityProperty(EntityLump.Entity entity, string name)
    {
        foreach (var (key, value) in entity)
        {
            if (GetCanonicalEntityKey(key) == name)
            {
                return ToEntityValue(value);
            }
        }

        return null;
    }

    private static string? GetCanonicalEntityKey(string? key)
    {
        return key switch
        {
            "hammeruniqueid" or "hammerUniqueId" => "hammerUniqueId",
            _ => key
        };
    }

    private static string? ToEntityValue(KVObject value)
    {
        return value.ValueType switch
        {
            KVValueType.Null => null,
            KVValueType.Collection => value.ToString(),
            KVValueType.Array => string.Join(' ', value.Values
                .Select(ToEntityValue)
                .Where(static item => item is not null)),
            KVValueType.Boolean => (bool)value ? "1" : "0",
            KVValueType.String => (string)value,
            KVValueType.FloatingPoint => ((float)value).ToString(CultureInfo.InvariantCulture),
            KVValueType.FloatingPoint64 => ((double)value).ToString(CultureInfo.InvariantCulture),
            KVValueType.Int16 => ((short)value).ToString(CultureInfo.InvariantCulture),
            KVValueType.UInt16 => ((ushort)value).ToString(CultureInfo.InvariantCulture),
            KVValueType.Int32 => ((int)value).ToString(CultureInfo.InvariantCulture),
            KVValueType.UInt32 => ((uint)value).ToString(CultureInfo.InvariantCulture),
            KVValueType.Int64 => ((long)value).ToString(CultureInfo.InvariantCulture),
            KVValueType.UInt64 => ((ulong)value).ToString(CultureInfo.InvariantCulture),
            KVValueType.BinaryBlob => Convert.ToBase64String(value.AsBlob()),
            _ => value.ToString()
        };
    }

    private static void WriteJsonc<T>(string path, T value)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(value, JsonOptions)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal);
        File.WriteAllText(path, "// Generated by Source2VpkTools\n" + json + "\n", new UTF8Encoding(false));
    }

    private static string NormalizeRelativePath(string path)
    {
        var normalized = path.Replace('\\', '/');
        if (string.IsNullOrWhiteSpace(normalized) || normalized.StartsWith("/", StringComparison.Ordinal) ||
            Path.IsPathRooted(normalized))
        {
            throw new InvalidDataException($"The VPK contains an invalid entry path: {path}");
        }

        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(static segment => segment is "." or ".."))
        {
            throw new InvalidDataException($"The VPK entry path escapes the output directory: {path}");
        }

        return string.Join('/', segments);
    }

    private static string RemoveMapPathPrefix(string path, string mapPath)
    {
        var prefix = mapPath.TrimEnd('/') + "/";
        return path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? path[prefix.Length..]
            : path;
    }

    private static string GetOutputPath(string outputRoot, string relativePath)
    {
        var normalized = NormalizeRelativePath(relativePath);
        var fullRoot = Path.GetFullPath(outputRoot);
        var fullPath = Path.GetFullPath(Path.Combine(fullRoot, normalized.Replace('/', Path.DirectorySeparatorChar)));
        var prefix = fullRoot.EndsWith(Path.DirectorySeparatorChar) ? fullRoot : fullRoot + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"The generated output path escapes the output directory: {relativePath}");
        }

        return fullPath;
    }

    private sealed record EntityDumpResult(
        string InputPath,
        string OutputDirectory,
        int MapVpkCount,
        int EntityLumpCount,
        int EntityCount);

}

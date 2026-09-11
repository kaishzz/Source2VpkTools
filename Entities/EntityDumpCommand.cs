using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
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
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
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
        var allLumps = new Dictionary<string, Dictionary<string, List<EntityRecord>>>(StringComparer.OrdinalIgnoreCase);
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
            var mapKey = Path.ChangeExtension(mapPath, null)!.Replace(Path.DirectorySeparatorChar, '/');
            var mapOutputDirectory = GetOutputPath(options.OutputDirectory, mapKey);
            Directory.CreateDirectory(mapOutputDirectory);

            var lumps = new Dictionary<string, List<EntityRecord>>(StringComparer.OrdinalIgnoreCase);
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

    private static List<EntityRecord> ReadEntities(Package package, PackageEntry lumpEntry)
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

    private static EntityRecord ToEntityRecord(EntityLump.Entity entity)
    {
        var properties = new Dictionary<string, JsonNode?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in entity)
        {
            if (key is null || key.Equals("classname", StringComparison.OrdinalIgnoreCase) ||
                key.Contains("targetname", StringComparison.OrdinalIgnoreCase) ||
                key.Contains("hammerUniqueId", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            properties[key] = ToJsonNode(value);
        }

        var connections = entity.Connections?.Select(static connection => new EntityConnection(
            connection.OutputName,
            connection.TargetName,
            connection.InputName,
            connection.OverrideParam,
            connection.Delay,
            connection.TimesToFire)).ToList();

        return new EntityRecord(
            GetStringProperty(entity, "classname") ?? string.Empty,
            GetStringProperty(entity, "hammerUniqueId") ?? string.Empty,
            GetStringProperty(entity, "targetname"),
            properties,
            connections);
    }

    private static string? GetStringProperty(EntityLump.Entity entity, string name)
    {
        foreach (var (key, value) in entity)
        {
            if (key.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return value.ValueType == KVValueType.String ? (string)value : value.ToString();
            }
        }

        return null;
    }

    private static JsonNode? ToJsonNode(KVObject value)
    {
        return value.ValueType switch
        {
            KVValueType.Null => null,
            KVValueType.Collection => ToJsonObject(value),
            KVValueType.Array => ToJsonArray(value),
            KVValueType.Boolean => JsonValue.Create((bool)value),
            KVValueType.String => JsonValue.Create((string)value),
            KVValueType.FloatingPoint => JsonValue.Create((float)value),
            KVValueType.FloatingPoint64 => JsonValue.Create((double)value),
            KVValueType.Int16 => JsonValue.Create((short)value),
            KVValueType.UInt16 => JsonValue.Create((ushort)value),
            KVValueType.Int32 => JsonValue.Create((int)value),
            KVValueType.UInt32 => JsonValue.Create((uint)value),
            KVValueType.Int64 => JsonValue.Create((long)value),
            KVValueType.UInt64 => JsonValue.Create((ulong)value),
            KVValueType.BinaryBlob => JsonValue.Create(Convert.ToBase64String(value.AsBlob())),
            _ => JsonValue.Create(value.ToString())
        };
    }

    private static JsonObject ToJsonObject(KVObject value)
    {
        var result = new JsonObject();
        foreach (var (key, child) in value)
        {
            if (key is not null)
            {
                result[key] = ToJsonNode(child);
            }
        }

        return result;
    }

    private static JsonArray ToJsonArray(KVObject value)
    {
        var result = new JsonArray();
        foreach (var child in value.Values)
        {
            result.Add(ToJsonNode(child));
        }

        return result;
    }

    private static void WriteJsonc<T>(string path, T value)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(value, JsonOptions);
        File.WriteAllText(path, "// Generated by Source2VpkTools\n" + json + Environment.NewLine, new UTF8Encoding(false));
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

    private sealed record EntityRecord(
        [property: JsonPropertyName("classname")] string Classname,
        [property: JsonPropertyName("hammerUniqueId")] string HammerUniqueId,
        [property: JsonPropertyName("targetname")] string? Targetname,
        [property: JsonPropertyName("properties")] Dictionary<string, JsonNode?> Properties,
        [property: JsonPropertyName("connections")] List<EntityConnection>? Connections);

    private sealed record EntityConnection(
        [property: JsonPropertyName("output")] string Output,
        [property: JsonPropertyName("target")] string Target,
        [property: JsonPropertyName("input")] string Input,
        [property: JsonPropertyName("param")] string Param,
        [property: JsonPropertyName("delay")] float Delay,
        [property: JsonPropertyName("timesToFire")] int TimesToFire);
}

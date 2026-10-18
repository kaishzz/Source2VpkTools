using ValveKeyValue;

namespace Source2VpkTools;

internal static class CompileGameInfo
{
    public static void Write(string sourcePath, string gameDirectory)
    {
        var serializer = KVSerializer.Create(KVSerializationFormat.KeyValues1Text);
        using var source = File.OpenRead(sourcePath);
        var document = serializer.Deserialize(source, new KVSerializerOptions
        {
            FileLoader = new IncludedFileLoader(sourcePath)
        });
        var fileSystem = document.Root["FileSystem"];
        var originalPaths = fileSystem["SearchPaths"];
        var searchPaths = KVObject.ListCollection();
        searchPaths.Add("Game", new KVObject("|gameinfo_path|."));
        searchPaths.Add("Mod", new KVObject("|gameinfo_path|."));
        var originalDirectory = Path.GetDirectoryName(sourcePath)!.Replace('\\', '/') + "/";
        foreach (var (key, value) in originalPaths.Children)
        {
            // 原配置中的模块相对路径仍指向原模块
            var path = ((string)value).Replace("|gameinfo_path|", originalDirectory, StringComparison.OrdinalIgnoreCase);
            searchPaths.Add(key, new KVObject(path));
        }
        fileSystem["SearchPaths"] = searchPaths;
        using var destination = File.Create(Path.Combine(gameDirectory, "gameinfo.gi"));
        serializer.Serialize(destination, document);
    }

    private sealed class IncludedFileLoader : IIncludedFileLoader
    {
        private readonly Stack<string> files = new();

        public IncludedFileLoader(string sourcePath) => files.Push(Path.GetFullPath(sourcePath));

        public Stream OpenFile(string filePath)
        {
            var path = Path.GetFullPath(filePath, Path.GetDirectoryName(files.Peek())!);
            if (files.Contains(path, StringComparer.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"gameinfo 包含文件形成循环: {path}");
            }
            var stream = new IncludedFileStream(path, () => files.Pop());
            // 包含流关闭后恢复父文件, 保证嵌套和同级包含的相对路径正确
            files.Push(path);
            return stream;
        }
    }

    private sealed class IncludedFileStream(string path, Action onDispose)
        : FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read)
    {
        private Action? release = onDispose;

        protected override void Dispose(bool disposing)
        {
            try { base.Dispose(disposing); }
            finally
            {
                if (disposing)
                {
                    var callback = release;
                    release = null;
                    callback?.Invoke();
                }
            }
        }
    }
}

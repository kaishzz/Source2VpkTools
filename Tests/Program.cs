using Source2VpkTools;
using ValveKeyValue;

var tests = new (string Name, Action Run)[]
{
    ("外部目录保留模块相对路径", DirectoryReferences),
    ("单文件保留资源路径和依赖", SingleFileReferences),
    ("独立编译模块有对应游戏配置", GameConfiguration),
    ("目录试运行使用输入列表和独立模块且不写文件", DirectoryDryRun),
    ("单文件试运行保留暂存和直接编译路径", SingleFileDryRun),
    ("单文件和子目录输出保留原输出语义", SelectedInputOutput),
    ("显式输出到输入父目录时保留源文件和依赖", OverlappingOutput),
    ("嵌套资源目录保留完整模块路径", NestedResourceDirectories),
    ("内容根外的同名祖先不会扩大复制范围", AncestorResourceNames),
    ("内容根边界在创建暂存前验证", ContentRootBoundaries),
    ("安装目录内使用已知模块内容根", InstalledModuleRoot),
    ("输入内的独立输出目录不参与复制", OutputSubdirectoryExcluded),
    ("输入列表排除依赖且遵循递归选项", InputListSelection),
    ("配置保留原游戏挂载", OriginalSearchPaths),
    ("游戏配置支持嵌套包含文件", IncludedGameConfiguration),
    ("循环包含配置明确失败", CyclicGameConfiguration),
    ("工作区释放时清理内容和游戏目录", WorkspaceCleanup),
    ("进程退出时保留完整输出和退出码", ProcessCompletion)
};
var failures = 0;
foreach (var (name, run) in tests)
{
    try
    {
        run();
        Console.WriteLine($"通过: {name}");
    }
    catch (Exception error)
    {
        failures++;
        Console.Error.WriteLine($"失败: {name}: {error.Message}");
    }
}
Console.WriteLine($"通过 {tests.Length - failures}/{tests.Length} 项测试");
return failures == 0 ? 0 : 1;

static void DirectoryReferences()
{
    using var fixture = new Fixture();
    using var workspace = CompileInputWorkspace.Create(fixture.Options() with { ContentRoot = null }, fixture.Resolution);
    var stagedRoot = Path.GetDirectoryName(workspace.CompilerInputPath)!;
    var moduleName = Path.GetRelativePath(Path.Combine(fixture.Root, "content"), stagedRoot).Split(Path.DirectorySeparatorChar)[0];
    var moduleRoot = Path.Combine(fixture.Root, "content", moduleName);
    Require(File.Exists(Path.Combine(moduleRoot, "materials", "leader", "ping.png")),
        "模块根下找不到 materials/leader/ping.png");
    Require(File.ReadAllText(fixture.Texture) == Fixture.TextureText, "源文件被修改");
}

static void SingleFileReferences()
{
    using var fixture = new Fixture();
    using var workspace = CompileInputWorkspace.Create(fixture.Options() with { InputPath = fixture.Texture }, fixture.Resolution);
    var relative = Path.GetRelativePath(Path.Combine(fixture.Root, "content"), workspace.CompilerInputPath);
    var moduleName = relative.Split(Path.DirectorySeparatorChar)[0];
    Require(File.Exists(Path.Combine(fixture.Root, "content", moduleName, "materials", "leader", "ping.png")),
        "单文件输入未保留 PNG 依赖");
    Require(workspace.CompilerInputPath.EndsWith(Path.Combine("materials", "leader", "ping.vtex")), "单文件资源路径被扁平化");
}

static void GameConfiguration()
{
    using var fixture = new Fixture();
    using var workspace = CompileInputWorkspace.Create(fixture.Options(), fixture.Resolution);
    var stagedRoot = Path.GetDirectoryName(workspace.CompilerInputPath)!;
    var moduleName = Path.GetRelativePath(Path.Combine(fixture.Root, "content"), stagedRoot).Split(Path.DirectorySeparatorChar)[0];
    var gameInfo = Path.Combine(fixture.Root, "game", moduleName, "gameinfo.gi");
    Require(moduleName != "csgo", "仍在原 csgo 模块下编译");
    Require(File.Exists(gameInfo), "独立内容根没有对应的 gameinfo.gi");
    Require(File.ReadAllText(fixture.Resolution.GameInfoPath!) == Fixture.GameInfoText, "原 gameinfo 被修改");
}

static void DirectoryDryRun()
{
    using var fixture = new Fixture();
    var (exitCode, output) = DryRun(fixture, fixture.Options() with { DryRun = true, ContentRoot = null });
    Require(exitCode == 0, "dry-run 失败");
    Require(output.Contains("-filelist"), "目录编译仍使用通配符, 会把 PNG 作为编译入口");
    Require(!Directory.Exists(Path.Combine(fixture.DirectoryPath, "output")), "dry-run 创建了输出目录");
    Require(output.Contains(Path.Combine("game", ".source2vpktools_<temporary>")), "dry-run 仍指向原游戏模块");
    Require(!Directory.Exists(Path.Combine(fixture.Root, "content")), "dry-run 写入了内容目录");
}

static void SingleFileDryRun()
{
    using var fixture = new Fixture();
    var options = fixture.Options() with { InputPath = fixture.Texture, DryRun = true };
    var (stagedExit, stagedOutput) = DryRun(fixture, options);
    Require(stagedExit == 0 && stagedOutput.Contains(Path.Combine("materials", "leader", "ping.vtex")), "单文件试运行丢失模块相对路径");
    Require(stagedOutput.Contains(Path.Combine("game", ".source2vpktools_<temporary>")), "单文件试运行未使用独立模块");
    var (directExit, directOutput) = DryRun(fixture, options with { Cs2Root = null, OutputDirectory = null, ContentRoot = null });
    Require(directExit == 0 && directOutput.Contains(fixture.Texture), "直接编译试运行改变了输入路径");
    Require(!directOutput.Contains(".source2vpktools_"), "直接编译试运行错误启用了暂存");
    Require(!Directory.Exists(Path.Combine(fixture.Root, "content")), "单文件试运行写入了内容目录");
}

static void SelectedInputOutput()
{
    foreach (var selection in new[] { "file", "directory", "default-output" })
    {
        using var fixture = new Fixture();
        var options = fixture.Options() with
        {
            InputPath = selection == "directory" ? Path.GetDirectoryName(fixture.Texture)! : fixture.Texture,
            OutputDirectory = selection == "default-output" ? null : fixture.Options().OutputDirectory
        };
        using var workspace = CompileInputWorkspace.Create(options, fixture.Resolution);
        var compiled = Path.Combine(workspace.GameDirectory!, "materials", "leader", "ping.vtex_c");
        Directory.CreateDirectory(Path.GetDirectoryName(compiled)!);
        File.WriteAllText(compiled, "compiled");
        workspace.CopyCompiledFilesBack();
        Require(File.Exists(Path.Combine(workspace.OutputDirectory, "ping.vtex_c")), $"{selection}: 输出多出了 materials/leader 层级");
        Require(!File.Exists(Path.Combine(workspace.OutputDirectory, "gameinfo.gi")), "输出混入了工作区配置");
    }
}

static void OverlappingOutput()
{
    using var fixture = new Fixture();
    foreach (var output in new[] { Path.GetDirectoryName(fixture.Texture)!, fixture.Input })
    {
        foreach (var input in new[] { fixture.Texture, Path.GetDirectoryName(fixture.Texture)! }.Where(input => input != output))
        {
            using var workspace = CompileInputWorkspace.Create(fixture.Options() with
            {
                InputPath = input,
                OutputDirectory = output
            }, fixture.Resolution);
            var texture = Path.Combine(Path.GetDirectoryName(workspace.CompilerInputPath)!, "ping.vtex");
            Require(File.Exists(texture), "输出目录过滤排除了所选源文件");
            Require(File.Exists(Path.ChangeExtension(texture, ".png")), "输出目录过滤排除了 PNG 依赖");
        }
    }
}

static void OutputSubdirectoryExcluded()
{
    using var fixture = new Fixture();
    var output = Path.Combine(fixture.Input, "compiled-output");
    Directory.CreateDirectory(output);
    File.WriteAllText(Path.Combine(output, "stale.vdata"), "output");
    using var workspace = CompileInputWorkspace.Create(fixture.Options() with { OutputDirectory = output }, fixture.Resolution);
    var files = File.ReadAllLines(workspace.CreateCompilerFileList(true));
    Require(files.Length == 1 && Path.GetFileName(files[0]) == "ping.vtex", "输出目录中的文件参与了编译");
    Require(File.Exists(Path.Combine(output, "stale.vdata")), "复制过程改动了原输出目录");
}

static void NestedResourceDirectories()
{
    using var fixture = new Fixture();
    foreach (var relative in new[] { "materials/models/weapons/ping.vtex", "models/materials/nested/ping.vtex" })
    {
        var texture = Path.Combine(fixture.Input, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(texture)!);
        File.WriteAllText(texture, "texture");
        File.WriteAllText(Path.ChangeExtension(texture, ".png"), "image");
        foreach (var input in new[] { texture, Path.GetDirectoryName(texture)! })
        {
            using var workspace = CompileInputWorkspace.Create(fixture.Options() with { InputPath = input }, fixture.Resolution);
            var moduleName = Path.GetFileName(workspace.GameDirectory!);
            var stagedTexture = Path.Combine(fixture.Root, "content", moduleName, relative);
            Require(File.Exists(stagedTexture), $"嵌套目录误判内容根: {relative}");
            Require(File.Exists(Path.ChangeExtension(stagedTexture, ".png")), "嵌套目录中的 PNG 依赖丢失");
        }
    }
}

static void AncestorResourceNames()
{
    using var fixture = new Fixture();
    var input = Path.Combine(fixture.DirectoryPath, "models", "pack", "assets_source");
    Directory.CreateDirectory(Path.Combine(input, "materials"));
    File.WriteAllText(Path.Combine(input, "materials", "ping.vtex"), "texture");
    File.WriteAllText(Path.Combine(fixture.DirectoryPath, "models", "private.txt"), "outside");
    Require(CompileInputWorkspace.GetInputRoot(input, fixture.Resolution) == input, "目录输入被扩展到未选择的祖先目录");
    using var workspace = CompileInputWorkspace.Create(fixture.Options() with { InputPath = input, ContentRoot = null }, fixture.Resolution);
    var staged = Path.GetDirectoryName(workspace.CompilerInputPath)!;
    Require(File.Exists(Path.Combine(staged, "materials", "ping.vtex")), "所选内容根内的资源未复制");
    Require(Directory.GetFiles(staged, "*", SearchOption.AllDirectories).Length == 1, "复制范围超出了所选内容根");
}

static void ContentRootBoundaries()
{
    using var fixture = new Fixture();
    var cases = new[]
    {
        (fixture.Options() with { InputPath = fixture.Texture, ContentRoot = null }, "--content-root"),
        (fixture.Options() with { InputPath = fixture.DirectoryPath }, "输入必须位于"),
        (fixture.Options() with { ContentRoot = fixture.DirectoryPath }, "暂存目录")
    };
    foreach (var (options, message) in cases)
    {
        var rejected = false;
        try { using var workspace = CompileInputWorkspace.Create(options, fixture.Resolution); }
        catch (InvalidOperationException error) { rejected = error.Message.Contains(message); }
        Require(rejected, $"未明确拒绝无效内容根: {message}");
        Require(!Directory.Exists(Path.Combine(fixture.Root, "content")), "拒绝输入前已创建暂存目录");
    }
}

static void InstalledModuleRoot()
{
    using var fixture = new Fixture();
    var content = Path.Combine(fixture.Root, "content", "csgo");
    var texture = Path.Combine(content, "materials", "ping.vtex");
    Directory.CreateDirectory(Path.GetDirectoryName(texture)!);
    File.WriteAllText(texture, "texture");
    using var workspace = CompileInputWorkspace.Create(fixture.Options() with { InputPath = texture, ContentRoot = null }, fixture.Resolution);
    Require(workspace.CompilerInputPath.EndsWith(Path.Combine("materials", "ping.vtex")), "已知模块内的单文件路径改变");
}

static void IncludedGameConfiguration()
{
    using var fixture = new Fixture();
    var gameInfo = fixture.Resolution.GameInfoPath!;
    var parts = Path.Combine(Path.GetDirectoryName(gameInfo)!, "parts");
    Directory.CreateDirectory(Path.Combine(parts, "nested"));
    var defaults = Path.Combine(parts, "nested", "defaults.gi");
    File.WriteAllText(defaults, Fixture.GameInfoText);
    var baseFile = Path.Combine(parts, "base.gi");
    File.WriteAllText(baseFile, "#base \"nested/defaults.gi\"\n\"GameInfo\" { }");
    File.WriteAllText(Path.Combine(Path.GetDirectoryName(gameInfo)!, "sibling.gi"), "\"GameInfo\" { title \"同级配置\" }");
    foreach (var directive in new[] { "#base", "#include" })
    {
        var sourceText = $"{directive} \"parts/base.gi\"\n#base \"sibling.gi\"\n\"GameInfo\" {{ }}";
        File.WriteAllText(gameInfo, sourceText);
        using var workspace = CompileInputWorkspace.Create(fixture.Options(), fixture.Resolution);
        using var stream = File.OpenRead(Path.Combine(workspace.GameDirectory!, "gameinfo.gi"));
        var document = KVSerializer.Create(KVSerializationFormat.KeyValues1Text).Deserialize(stream);
        Require((string)document.Root["FileSystem"]["SteamAppId"] == "730", "包含文件中的配置未保留");
        Require((string)document.Root["title"] == "同级配置", "嵌套包含结束后未恢复父文件相对路径");
        Require(document.Root["FileSystem"]["SearchPaths"].Children.Any(entry => entry.Key == "Game" && (string)entry.Value == "core"), "包含文件中的游戏挂载丢失");
        Require(File.ReadAllText(gameInfo) == sourceText && File.ReadAllText(defaults) == Fixture.GameInfoText, "原配置或包含文件被修改");
    }
}

static void CyclicGameConfiguration()
{
    using var fixture = new Fixture();
    var gameInfo = fixture.Resolution.GameInfoPath!;
    File.WriteAllText(gameInfo, "#base \"gameinfo.gi\"\n\"GameInfo\" { }");
    var rejected = false;
    try { using var workspace = CompileInputWorkspace.Create(fixture.Options(), fixture.Resolution); }
    catch (InvalidDataException error) { rejected = error.Message.Contains("循环"); }
    Require(rejected, "循环包含未明确失败");
    Require(!Directory.EnumerateDirectories(Path.Combine(fixture.Root, "content")).Any(), "包含失败后残留暂存目录");
}

static void ProcessCompletion()
{
    var previous = Console.Out;
    using var output = new StringWriter();
    try
    {
        Console.SetOut(TextWriter.Synchronized(output));
        var exitCode = ResourceCompilerProcess.Run(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe"),
            new[] { "/d", "/c", "echo stdout-probe & echo stderr-probe 1>&2 & exit /b 7" });
        Require(exitCode == 7, "进程退出码丢失");
        Require(output.ToString().Contains("stdout-probe") && output.ToString().Contains("stderr-probe"), "进程结束时输出未完整回收");
    }
    finally { Console.SetOut(previous); }
}

static void InputListSelection()
{
    using var fixture = new Fixture();
    File.WriteAllText(Path.Combine(fixture.Input, "root.vdata"), "data");
    File.WriteAllText(Path.Combine(fixture.Input, "old.vdata_c"), "compiled");
    using var workspace = CompileInputWorkspace.Create(fixture.Options(), fixture.Resolution);
    var shallow = File.ReadAllLines(workspace.CreateCompilerFileList(false));
    Require(shallow.Length == 1 && Path.GetFileName(shallow[0]) == "root.vdata", "非递归扫描包含子目录, 依赖或编译产物");
    var recursive = File.ReadAllLines(workspace.CreateCompilerFileList(true));
    Require(recursive.Length == 2 && recursive.Any(path => Path.GetFileName(path) == "ping.vtex"), "递归扫描遗漏 vtex 或包含 PNG");
    Require(File.Exists(Path.ChangeExtension(recursive.Single(path => path.EndsWith("ping.vtex")), ".png")), "PNG 依赖未保留");
}

static void OriginalSearchPaths()
{
    using var fixture = new Fixture();
    File.WriteAllText(fixture.Resolution.GameInfoPath!,
        "\"GameInfo\" { FileSystem { SteamAppId 730 SearchPaths { Game csgo Game core Game |gameinfo_path|. Mod csgo } } }");
    using var workspace = CompileInputWorkspace.Create(fixture.Options(), fixture.Resolution);
    using var stream = File.OpenRead(Path.Combine(workspace.GameDirectory!, "gameinfo.gi"));
    var document = KVSerializer.Create(KVSerializationFormat.KeyValues1Text).Deserialize(stream);
    var paths = document.Root["FileSystem"]["SearchPaths"].Children
        .Where(entry => entry.Key == "Game").Select(entry => (string)entry.Value).ToArray();
    Require(paths.Length == 4 && paths[0] == "|gameinfo_path|." && paths[1] == "csgo" && paths[2] == "core", "原游戏挂载被覆盖或顺序改变");
    Require(paths[3] == Path.GetDirectoryName(fixture.Resolution.GameInfoPath!)!.Replace('\\', '/') + "/.", "原模块路径被重定向到隔离模块");
}

static void WorkspaceCleanup()
{
    using var fixture = new Fixture();
    var workspace = CompileInputWorkspace.Create(fixture.Options(), fixture.Resolution);
    var content = Path.GetDirectoryName(workspace.CompilerInputPath)!;
    var game = workspace.GameDirectory!;
    var fileList = workspace.CreateCompilerFileList(true);
    workspace.Dispose();
    Require(!Directory.Exists(content) && !Directory.Exists(game) && !File.Exists(fileList), "工作区或输入列表未清理");
    Require(File.Exists(fixture.Texture) && File.Exists(fixture.Resolution.GameInfoPath), "清理影响了原文件");
}

static (int ExitCode, string Output) DryRun(Fixture fixture, CompileOptions options)
{
    var compiler = Path.Combine(fixture.DirectoryPath, "resourcecompiler.exe");
    File.WriteAllText(compiler, "");
    var previous = Console.Out;
    using var output = new StringWriter();
    try
    {
        Console.SetOut(output);
        return (CompileCommand.Run(options with { ResourceCompilerPath = compiler }), output.ToString());
    }
    finally { Console.SetOut(previous); }
}

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

sealed class Fixture : IDisposable
{
    public const string TextureText = "\"m_fileName\" \"string\" \"materials/leader/ping.png\"";
    public const string GameInfoText = "\"GameInfo\" { FileSystem { SteamAppId 730 SearchPaths { Game csgo Game core Mod csgo } } }";
    public string DirectoryPath { get; } = Path.Combine(Path.GetTempPath(), "Source2VpkToolsTests", Guid.NewGuid().ToString("N"));
    public string Root => Path.Combine(DirectoryPath, "CS2");
    public string Input => Path.Combine(DirectoryPath, "assets_source");
    public string Texture => Path.Combine(Input, "materials", "leader", "ping.vtex");
    public ResourceCompilerResolution Resolution => new("unused.exe", Root, Path.Combine(Root, "game", "csgo", "gameinfo.gi"));
    public Fixture()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Texture)!);
        File.WriteAllText(Texture, TextureText);
        File.WriteAllText(Path.ChangeExtension(Texture, ".png"), "image");
        Directory.CreateDirectory(Path.GetDirectoryName(Resolution.GameInfoPath!)!);
        File.WriteAllText(Resolution.GameInfoPath!, GameInfoText);
    }
    public CompileOptions Options() => new(Input, Root, null, Resolution.GameInfoPath, Path.Combine(DirectoryPath, "output"), true, false, true, true, false, false, Input);
    public void Dispose() => Directory.Delete(DirectoryPath, recursive: true);
}

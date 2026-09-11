# Source2VpkDump

`Source2VpkDump` 是一个独立的 Source 2 VPK 解包工具, 不依赖 ModSharp, 不需要启动游戏或服务器

工具只读取 VPK 中的 entry 并写出已编译文件, 不会反编译 `.vdata_c`, `.vtex_c`, `.vmdl_c`, `.vsnd_c` 等资源, 也无法从编译文件恢复原始源文件

## 使用

```powershell
vpkdump.exe "D:\Steam\steamapps\workshop\content\730\123456789\123456789_dir.vpk"
```

默认输出到当前目录的 `assets` 文件夹, 也可以指定输出目录:

```powershell
vpkdump.exe "D:\path\123456789_dir.vpk" --output "D:\Dump\assets"
```

支持将 VPK 文件直接拖拽到 EXE, Windows 会把文件路径作为命令行参数传入

对于拆分 VPK, 应传入 `*_dir.vpk`, 并保持同目录下的其他 VPK 分片文件不变

## 输出

VPK 内部路径会原样保留在输出目录下, 例如 `materials/example.vtex_c` 会输出为:

```text
assets\materials\example.vtex_c
```

工具不会清理输出目录, 不会删除 VPK 中不存在的旧文件

## 构建

要求安装 .NET 10 SDK:

```powershell
dotnet build Source2VpkDump.csproj --configuration Release
```

框架依赖版本位于:

```text
bin\Release\net10.0\vpkdump.exe
```

如果希望在未安装 .NET 10 Runtime 的机器上使用, 推荐发布为独立单文件:

```powershell
dotnet publish Source2VpkDump.csproj --configuration Release --runtime win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

独立单文件位于:

```text
bin\Release\net10.0\win-x64\publish\vpkdump.exe
```

将该目录加入 Windows 的 `PATH` 后, 就可以在任意终端中调用 `vpkdump`

# Source2VpkTools

`Source2VpkTools` 是一个独立的 Source 2 VPK 解包和 Steam Workshop 上传工具, 不依赖 ModSharp, 不需要启动 CS2 或 Workshop Manager

工具只读取 VPK 中的 entry 并写出已编译文件, 不会反编译 `.vdata_c`, `.vtex_c`, `.vmdl_c`, `.vsnd_c` 等资源, 也无法从编译文件恢复原始源文件

## 使用

```powershell
Source2VpkTools.exe "D:\Steam\steamapps\workshop\content\730\123456789\123456789_dir.vpk"
```

默认输出到当前目录的 `assets` 文件夹, 也可以指定输出目录:

```powershell
Source2VpkTools.exe "D:\path\123456789_dir.vpk" --output "D:\Dump\assets"
```

支持将 VPK 文件直接拖拽到 EXE, Windows 会把文件路径作为命令行参数传入

对于拆分 VPK, 应传入 `*_dir.vpk`, 并保持同目录下的其他 VPK 分片文件不变

## 上传到 CS2 Workshop

`upload` 的 `--content-dir` 是 VPK 的根目录, 程序会递归读取该目录下的所有普通文件, 保留相对路径并生成一个外层 VPK. 例如选择 `dump` 时, `dump\maps\test.vpk` 会进入外层 VPK 的 `maps/test.vpk`, 不会多出一层 `dump/`. 原始目录不会被修改, Steam 接收的是临时目录中的生成 VPK, 不是原始目录

该工具固定服务 CS2, Steam App ID 固定为 `730`, 不提供 `--appid` 参数. 打包前会根据当前可用内存检查源文件总量, 避免 ValvePak 的 byte-array writer 在大型内容上发生不可控 OOM

先使用 dry-run 检查文件数量和 VPK round-trip:

```powershell
Source2VpkTools.exe upload `
  --content-dir "C:\Users\24854\Downloads\3782142946\dump" `
  --package-name "pak01_dir.vpk" `
  --title "My Map" `
  --preview "C:\Upload\preview.jpg" `
  --description-file "C:\Upload\description.txt" `
  --visibility private `
  --dry-run
```

实际上传时去掉 `--dry-run`, 并确保 Steam 已启动且已登录:

```powershell
Source2VpkTools.exe upload `
  --content-dir "C:\Users\24854\Downloads\3782142946\dump" `
  --package-name "pak01_dir.vpk" `
  --title "My Map" `
  --preview "C:\Upload\preview.jpg" `
  --description-file "C:\Upload\description.txt" `
  --visibility private `
  --state-file "C:\Upload\my-map-workshop.json"
```

更新已有项目时增加 `--workshop-id <已有项目ID>`. 新项目默认为 private, 只有 `SubmitItemUpdate` 成功后才会写入 `--state-file`

Steamworks.NET 只提供托管绑定, 项目默认使用仓库内官方 `2025.164.1` Standalone 包的 Windows-x64 文件, 并自动复制匹配的 `Steamworks.NET.dll` 和 `steam_api64.dll`. `steam_appid.txt` 已随构建输出复制并默认为 `730`. 这些 DLL 不复制到 CS2 安装目录

如果需要改用 NuGet 的 `2024.8.0` 绑定, 可显式使用:

```powershell
dotnet build Source2VpkTools.csproj --configuration Release `
  -p:UseSteamworksNetNuGet=true `
  -p:SteamApi64Path="D:\Steamworks.NET-Standalone_2025.164.1\Windows-x64\steam_api64.dll"
```

上传模式不会调用 `SteamAPI_RestartAppIfNecessary`, 不会启动 CS2, 不会打开 Workshop Manager, 也不会读取或修改 `gameinfo.gi`. `--keep-staging` 可在失败时保留临时 VPK 目录用于排查

## 输出

VPK 内部路径会原样保留在输出目录下, 例如 `materials/example.vtex_c` 会输出为:

```text
assets\materials\example.vtex_c
```

工具不会清理输出目录, 不会删除 VPK 中不存在的旧文件

## 构建

要求安装 .NET 10 SDK:

```powershell
dotnet build Source2VpkTools.csproj --configuration Release
```

框架依赖版本位于:

```text
bin\Release\net10.0\Source2VpkTools.exe
```

如果希望在未安装 .NET 10 Runtime 的机器上使用, 推荐发布为独立单文件:

```powershell
dotnet publish Source2VpkTools.csproj --configuration Release --runtime win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

独立单文件位于:

```text
bin\Release\net10.0\win-x64\publish\Source2VpkTools.exe
```

将该目录加入 Windows 的 `PATH` 后, 就可以在任意终端中调用 `Source2VpkTools`
## 直接上传已有 VPK

如果已有完整 VPK, 可以使用 --vpk 直接上传. 指定 *_dir.vpk 时, 程序会自动复制同目录下匹配的 _001.vpk, _002.vpk 等分片, 不会再套一层 VPK. --vpk 与 --content-dir 互斥, 直接上传模式不需要 --package-name

示例:

    Source2VpkTools.exe upload --vpk "C:\Users\24854\Downloads\3782142946\3782142946_dir.vpk" --title "My Workshop Item" --preview "C:\Upload\preview.jpg" --visibility private --dry-run

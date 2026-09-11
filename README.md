# Source2VpkTools

`Source2VpkTools` 是一个独立的 Source 2 VPK 解包、资源编译、地图实体导出和 Steam Workshop 上传工具, 不依赖 ModSharp, 不需要启动 CS2 或 Workshop Manager

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

## 编译 Source 2 资源

`compile` 会调用本机 CS2 Workshop Tools 提供的原生 `resourcecompiler.exe`, 将 `.vmat`, `.vtex`, `.vmdl`, `.vdata`, `.vmap` 等源文件编译为运行时使用的 `_c` 文件. 工具不会实现或替换 Valve 的编译器, 不会修改 `gameinfo.gi`, 也不会启动 CS2 或 Workshop Tools UI. `--gameinfo` 指向文件路径, 传给原生编译器时会自动转换为其要求的所在目录

输入路径必须由调用者指定, CS2 安装目录和编译器路径默认通过 Steam AppID `730` 自动查找:

```powershell
Source2VpkTools.exe compile `
  --input "C:\Steam\steamapps\common\Counter-Strike Global Offensive\content\csgo_addons\my_addon" `
  --recursive
```

自动查找失败时, 可以显式指定覆盖路径:

```powershell
Source2VpkTools.exe compile `
  --input "D:\CS2\content\csgo_addons\my_addon" `
  --cs2-root "D:\CS2" `
  --resource-compiler "D:\CS2\game\bin\win64\resourcecompiler.exe" `
  --gameinfo "D:\CS2\game\csgo\gameinfo.gi" `
  --recursive
```

可用选项:

```text
--input <path>                 必填, 单个源文件或目录
--cs2-root <directory>         覆盖自动发现的 CS2 根目录
--resource-compiler <file>     覆盖 resourcecompiler.exe
--gameinfo <file>              覆盖 gameinfo.gi, 只读不修改
--recursive                    递归编译目录
--force                        强制重新编译
--novpk                        请求编译器输出 loose 文件
--dry-run                      只解析路径并显示命令, 不启动编译器
```

`--dry-run` 可用于确认自动解析结果:

```powershell
Source2VpkTools.exe compile `
  --input "D:\CS2\content\csgo_addons\my_addon" `
  --recursive `
  --dry-run
```

如果 `--input` 不在 CS2 的 `content` 目录下, 原生编译器无法通过自身的 content 映射识别它, 工具会将输入临时复制到对应模块的 content 目录, 强制使用 `-novpk` 编译, 再把生成的编译文件复制回输入目录并清理临时目录. 该过程不会修改 `gameinfo.gi`, 但会在 CS2 的 content/game 目录创建带随机名称的临时目录; 正常结束或编译失败时都会清理. 因此可以直接指定任意外部源文件或目录, 但目录输入仍需使用 `--recursive` 才会处理子目录

## 导出地图实体

`entities` 使用 `ValveResourceFormat` 解析 Workshop VPK 中嵌套地图 VPK 的 `vents_c` 实体块, 输出 JSONC 格式的实体、属性和 I/O 连接. 输入应为外层 Workshop VPK, 通常是 `*_dir.vpk`:

```powershell
Source2VpkTools.exe entities `
  --input "C:\Steam\steamapps\workshop\content\730\123456789\123456789_dir.vpk" `
  --output "D:\Dump\entities"
```

输出目录会包含一个合并的 `<vpk-name>_full.jsonc`, 以及按地图和实体块拆分的 JSONC 文件. 该命令只读取 VPK, 不会修改输入文件或 CS2 安装目录

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

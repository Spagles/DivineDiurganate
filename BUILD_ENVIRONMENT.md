# DivineDiurganate 云端编译交接文档（RimWorld 1.6）

本项目已做“云端可编译”改造，构建时不依赖本机 Steam/workshop 目录。下面是**从空环境到可编译**的最小步骤与依赖清单，适合作为 Docker/CI 参考。

## 必备依赖

### 1) .NET SDK（必须）
- 版本：**8.0.416**（由根目录 `global.json` 固定）
- 安装脚本：仓库内置 `dotnet-install.sh`
- 建议安装目录：仓库根目录 `./.dotnet`（便于隔离、可缓存）

安装命令（Linux 示例）：
```bash
chmod +x dotnet-install.sh
./dotnet-install.sh --version 8.0.416 --install-dir ./.dotnet
export PATH="$(pwd)/.dotnet:$PATH"
```

### 2) NuGet 包（构建时自动还原）
这些包在 `Source/DivineDiurganate/DivineDiurganate.csproj` 中声明，`dotnet build` 时自动下载：
- `Krafs.Rimworld.Ref`（RimWorld/Unity 引用）
- `Microsoft.NETFramework.ReferenceAssemblies.net48`（.NET Framework 4.8 参考程序集）

> 无需手动下载，首次构建会自动还原到 NuGet 缓存目录。

### 3) 已内置的 Workshop 依赖（无需下载）
以下 DLL 已打包在仓库内，项目直接引用：
- `Source/DivineDiurganate/Libs/0Harmony.dll`
- `Source/DivineDiurganate/Libs/AlienRace.dll`
- `Source/DivineDiurganate/Libs/FacialAnimation.dll`

## 构建命令（必须运行）
```bash
dotnet build "Source/DivineDiurganate/DivineDiurganate.sln" -c Release
```

> 注意：如果使用 `./.dotnet` 安装目录，请使用 `./.dotnet/dotnet build ...` 或确保 PATH 中包含 `./.dotnet`。

## 参考资料目录（只读）
用于查 Def/API/签名，**不要修改**：
- `Data/**`（RimWorld 原版 + DLC 的 Def/XML/语言）
- `dll1.6/**`（API/签名参考文本）

## 产物输出
构建输出路径已经在项目里配置（Release/Debug 均输出到 Mod 的 Assemblies 目录），**不要手动复制 DLL**。


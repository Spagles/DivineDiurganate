你在仓库 Kalospacer/DivineDiurganate 中工作。目标是对 RimWorld 1.6 的 Mod 进行开发，并且每次修改都要能在云端环境完成编译验证。仓库里包含了两份参考资料目录：Data/（RimWorld 原版 + DLC 的 Def/XML/语言等）和 dll1.6/（API/签名参考文本）。它们用于查 Def/字段/defName/patch 目标/API 类型，不要随意改动或删除。

构建与验证（硬性要求）

每次改完必须运行：DivineDiurganate.sln" -c Release
若失败：先修复编译错误，再继续功能性改动；修复后重新 build 确认通过。
项目结构与改动范围

主要代码：Source/DivineDiurganate/**
主要内容（XML/Defs/语言/贴图等）：仓库根目录下的 1.6/、Content/、Languages/、About/（按实际需要改）
参考库（只读）：Data/**、dll1.6/**
依赖与可移植性约束

该项目已做“云端可编译”改造：DivineDiurganate.csproj 为 SDK-style net472，使用 NuGet Krafs.Rimworld.Ref（RimRef）提供 RimWorld/Unity 的引用。
workshop 依赖 DLL 已打包进仓库：Source/DivineDiurganate/Libs/（例如 0Harmony.dll、AlienRace.dll、FacialAnimation.dll），项目从这里引用；不要再引用本机 Steam/workshop 路径。
输出与产物

编译输出路径已经在项目里配置（Debug/Release 都指向 Mod 的 Assemblies 目录），不要手动复制 DLL。
参考资料怎么用（查找策略）

查某个 defName/字段/原版定义：在 *.xml 里搜索（defName、对应 ThingDef/RecipeDef/... 节点）。
查翻译/label/key：在 Data/**/Languages/** 里搜。
查 API 类型/方法签名：在 dll1.6/** 里搜对应类名/方法名（文本索引）。
对于你不确定的 RimWorld 机制，优先用仓库内 Data/ 和 dll1.6/ 做事实核对，不要凭印象。
工作方式

做最小必要改动；避免无关重构。
修改 XML 时确保结构正确、defName 唯一、patch 目标路径存在。
提交前确保 build 通过；提交信息清晰说明改动点与原因。如果遇到环境缺失问题，请自行安装修复环境后继续任务。
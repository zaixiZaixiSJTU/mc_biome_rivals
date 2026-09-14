# 工作区检查点：2026-09-14

关联任务：`BR-000`

本文件记录 `b57f44a` 之后尚未提交工作的归属与验证证据。它不是改动日志，也不表示下列切片已经提交。

## 已验证基线

- 仓库：`D:\gitt\mc_biome_rivals`
- HEAD：`b57f44a feat: add end crystal end phase damage`
- Unity：`6000.0.28f1c1`
- 开发中版本：协议 31、规则集 `prototype-0.48`、内容版本 40。
- `scripts/validate.ps1`：通过；内容注册表为 74 张牌、53 个已实现效果、16 个待实现效果；服务端规则测试 162/162；TypeScript 类型检查与服务端构建通过。
- `scripts/validate-unity.ps1`：通过；Unity EditMode 测试 202/202。
- `git diff --check`：无空白错误；仅报告现有 CRLF/LF 转换警告。
- 共享卡牌定义、主题、中文名称和中文规则文本与 Unity Resources 镜像的 SHA-256 均一致；服务端生成目录的新鲜度由 `validate.ps1` 校验通过。

验证时间均为 2026-09-14。任何后续代码修改都会使这些结果失效，提交前必须重新运行对应层级验证。

## 工作区规模

BR-000 首次盘点时共有：

- 61 个已修改文件；
- 32 个已删除文件；
- 31 个未跟踪文件；
- 无暂存文件。

`Assets/Generated/MinecraftCardIcons` 与 `Assets/Generated/MinecraftWorldTextures` 下的本机提取资源已被 `.gitignore` 排除，不属于待提交文件。

## 切片 A：2.5D 体素战场与原版实体模型

归属 `BR-001`。

主要文件：

- `DemoBattlefield3D.cs`、`DemoBattlefieldThemeCatalog.cs`、`DemoSceneBuilder.cs`；
- `DemoMinecraftModelFactory.cs`、`DemoMinecraftEntityModelBuilder.cs`、`DemoMinecraftEntityGeometry.cs`、`DemoJsonParser.cs`；
- `DemoEntity.shader`、`DemoEntityIdleAnimator.cs`、`DemoWorldAssetProvider.cs`；
- `DemoMinecraftEntityModelTests.cs` 以及 `DemoLocalMatchTests.cs` 中的场景/模型测试；
- `extract-minecraft-world-textures.ps1`、`extract-minecraft-entity-models.ps1`、`build-demo.ps1`；
- `shared-schema/card-art/bedrock-entity-source.v1.json`；
- `demo-voxel-*`、`demo-faction-*` 预览截图。

删除项：旧 `DemoCompositeBackdrop.shader`、整张插画背景以及 14 张 `near/far` 半场图片和对应 `.meta`。全仓代码搜索未发现运行时残余引用。

文档修正：`Demo_UI_Design_v0.1.md` 和 `battlefield-half-module-spec-v1.md` 已把旧插画/半场合成方案明确标记为历史方案，禁止新实现重新引用。

## 切片 B：烈焰人、烈焰棒与 FIRE

归属 `BR-002`。

主要文件：

- `server-nakama/src/rules/match-engine.ts`、`types.ts`、`invariants.ts` 与规则测试；
- 三份协议 Schema 与 `shared-schema/README.md`；
- `MatchState.cs`、`MatchStateStoreTests.cs`、`GameVersions.cs`；
- 卡牌定义注册表、实现效果注册表、生成后的服务端目录与 Unity Resources 副本；
- `DemoLocalMatch.cs`、`DemoCardTargeting.cs`、`DemoSceneController.cs` 中的离线演示和表现映射；
- `blaze-fire-status-spec-v1.md` 与 `demo-blaze-fire-preview-v1.png`；
- `server-nakama/README.md`、根 `readme.md`。

所有批量更新为协议 31 / 规则集 `prototype-0.48` / 内容版本 40 的既有机制规范，也随本切片提交，避免版本声明跨提交失配。

## 切片 C：任务管理文档

归属 `BR-000`，但应与实际代码切片在同一整合序列中提交，避免日志先于代码声称功能已经落地。

- `docs/development/change-log.md`
- `docs/development/task-roadmap.md`
- 本检查点文件

`change-log.md` 还需要在 BR-002 完成时补充 FIRE 条目、验证命令和对应提交号。

## 需要隔离判断的文件

以下文件不得混入切片提交，必须逐项确认：

- `scripts/add_units.py`：一次性直接改写模型工厂的辅助脚本；功能已经反映在 C# 源码中，但它不是可重复、幂等的正式资源管线。当前保留原文件，不自动删除，也不纳入计划提交。
- `client-unity/ProjectSettings/PackageManagerSettings.asset`：Unity 自动产生且包含 `https://packages.unity.cn` 注册表。提交会影响所有开发环境；在确认团队包源策略前保持未跟踪。
- `client-unity/ProjectSettings/ProjectSettings.asset`：已完成字段审查。项目仍使用旧输入 API 与 `StandaloneInputModule`，因此“双输入系统”、目标像素密度和平台默认版本补齐均属 Unity 自动噪声；这些修改已移除，文件现与 HEAD 一致。

## 存在交叉修改的文件

以下文件同时承担视觉演示和 FIRE 表现，不能按整文件粗暴拆分，需要逐块审查或让 BR-001、BR-002 按固定顺序提交：

- `DemoLocalMatch.cs`
- `DemoCardTargeting.cs`
- `DemoSceneController.cs`
- `DemoLocalMatchTests.cs`
- 根 `readme.md`

推荐顺序：先提交 BR-001 的体素场景/模型基础，再在其上提交 BR-002 的 FIRE 规则与表现。拆分时每个提交后都至少运行受影响测试；最终再运行两条全量验证命令。

## BR-000 剩余退出条件

1. 决定两个仍被隔离的未跟踪文件（`scripts/add_units.py`、`PackageManagerSettings.asset`）应提交、忽略或删除；未经确认不破坏其内容。
2. 按上面的 A/B/C 边界完成暂存预演；当前检查已经确认所有发生变化的 Unity Asset 均有一致的 `.meta` 成对变化，Schema 的 Unity 镜像哈希一致且服务端生成目录校验通过。
3. 完成 BR-001、BR-002 的实际提交后，把提交号和最终验证结果写入 `change-log.md`。
4. 最终工作区只允许保留明确标记为本机生成或刻意暂存的文件。

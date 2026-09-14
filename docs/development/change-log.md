# 改动日志

本文件按时间倒序记录影响视觉表现、资源管线或运行时架构的改动。

## 2026-09-14 烈焰人、烈焰棒与 FIRE 权威状态闭环

- **规则切片**：NT-003「烈焰人」在主动攻击或反击造成普通伤害且目标仍存活后施加 `FIRE 2`；被敌方击杀时向击杀者生成 TK-013「烈焰棒」。烈焰棒先对存活敌方生物造成 1 点普通伤害，目标存活时再施加或刷新着火。
- **状态时序**：`FIRE` 在受影响对象控制者的两次结束阶段各造成 1 点真实伤害，随后移除；刷新不叠加，保留最新持续时间与权威伤害来源。致死会立即进入统一死亡、掉落与终局结算，不创建无意义状态，也不继续执行已失效的阶段步骤。
- **协议与内容**：升级至 `protocolVersion 31`、`rulesetVersion prototype-0.48`、内容版本 40；快照和状态生命周期事件公开 `FIRE`，Unity 状态仓库执行同构校验。卡牌效果注册状态更新为 53 个 `IMPLEMENTED`、16 个 `PENDING`。
- **离线表现**：Demo 可确定性预置烈焰人与烈焰棒，复用 3D 地表目标选择、原版烈焰人体素模型和状态显示；视觉验收见 [`demo-blaze-fire-preview-v1.png`](../design/assets/demo-blaze-fire-preview-v1.png)。
- **验证**：`scripts/validate.ps1` 通过，包括内容同步校验、TypeScript 类型检查、162/162 服务端规则测试和服务端构建；`scripts/validate-unity.ps1` 使用 Unity `6000.0.28f1c1` 通过 202/202 EditMode 测试。

## 2026-09-14 场地扩建、群系装饰与光影强化

- **扩大场地**：体素地形由 23×13 扩展至 **23×17**（玩家半场 z −8…−1、敌方半场 z 1…8），地基与四周边框同步放大；靠近边框的两排地面略抬升形成缓阶；河道加密石块并新增砂砾河岸（新纹理 `gravel`）。部署格与棋盘坐标不变，UI 投影映射不受影响。
- **群系装饰随阵营切换重建**：装饰迁入独立的 `BattlefieldDecor` 子树，`RebuildDecor()` 在 `BuildNow` 与每次 `SetBattlefieldThemes` 时按双方群系重建。七种群系各配装饰套件：平原=橡树/灌木丛（新纹理 `oak_log`）、沙漠=仙人掌×4+沙堆、冰原=冰柱×4+雪堆、深暗=巨石+黑橡树干、海洋=珊瑚堆+海晶灯、下界=玄武岩柱+火苗+岩浆池、末地=黑曜石柱+紫珀块。每侧两根**萤石灯柱**（新纹理 `glowstone`），挂群系环境色的点光源（随装饰一起销毁重建）。
- **光影强化**：太阳仰角 48° → 38°、方位 −32° → −30°，拉长全场阴影；阴影浓度 `shadowStrength` 提至 0.82；灯柱点光源提供近景氛围光。
- **测试**：七群系循环新增装饰重建断言（每群系地标装饰、双侧灯柱、装饰子树规模），202/202 通过。
- **视觉验收**：默认场景 [`demo-voxel-battlefield-preview-v3.png`](../design/assets/demo-voxel-battlefield-preview-v3.png) 与沙漠 vs 末地 [`demo-faction-desert-end-preview-v3.png`](../design/assets/demo-faction-desert-end-preview-v3.png) 通过——地形延伸至画面边缘无黑洞、群系装饰与灯柱可见、长影子清晰、部署格与棋子无遮挡、无渲染错误；溺尸与末影人等原版模型在逐面明暗下清晰可辨。

## 2026-09-14 全部占位模型替换为原版 MC 模型（36 生物 + 3 建筑）

对照卡牌注册表盘点了全部战场对象，把剩余占位模型全部替换为原版 Minecraft 模型：

- **14 个新增生物模型**（此前只有 21/36 个生物卡有专属模型，其余显示通用方块占位）：尸壳、沙漠考古学家（原版沙漠村民皮肤）、恶地劫掠者、商队领头骆驼、北极熊、地牢骷髅、海龟、僵尸猪灵、炽足兽、凋灵骷髅（含要塞凋灵骷髅）、末影螨、末影人、潜影贝、末影龙化身。全部从 bedrock-samples 提取几何 + 配套贴图，接入工厂元数据表（按几何实际比例选择高度/宽度适配、悬停高度）。至此 36 个生物卡全部拥有原版模型，通用方块生物仅作资产缺失时的兜底。
- **3 个建筑换掉通用方块结构**：重生锚（原版 `respawn_anchor_side1/top` 方块纹理 + 四角充能发光）、下界要塞（下界砖墙体 + 双塔 + 橙色窗光）、末地传送门框架（基岩方块环架 + 中央发光核心）。
- **提取脚本增强**：`extract-minecraft-entity-models.ps1` 下载加重试机制（三次尝试），避免网络抖动中断提取。
- **解析/构建修复**：骨骼父级引用大小写不敏感（原版劫掠者文件中 `rightItem` 的父级写作 `rightArm` 而骨骼名为小写）；骆驼与北极熊按高度适配（几何高大于宽，宽度适配会放大变形）；末影龙 `wing1` 骨骼烘焙 180° 偏航（基岩几何把右翅画在左侧、原版靠动画翻转），双翅对称展开后按翼展适配。
- **测试**：`FactoryMetadataCoversEveryEntityCard` 覆盖全部 36 个生物卡；几何集成测试新增 15 个 TestCase 并放宽最宽模型（龙）的包围盒上限。

## 2026-09-13 实体模型精细化：原版逐面明暗、骨骼待机动画与覆盖层

第一轮几何修复后，模型数据正确但观感偏"生硬"：均匀自发光把明暗洗平、模型完全静止、部分原版双层贴图部位缺失。本轮按原版 Minecraft 实体渲染风格细化：

- **新增实体着色器 `BiomeRivals/Demo/Entity`**（`Demo/Art/DemoEntity.shader`）：非光照渲染，`纹理 × 逐面顶点色 × 染色` 并支持雾效与镂空（alpha clip），外加 `_EmissiveBoost` 供火焰类生物（烈焰人、岩浆 cube 材质 0.3）无阴影提亮。删除原先 `_EmissionMap=纹理 + 0.34 均匀自发光` 的洗平做法。
- **构建器烘焙原版逐面明暗到顶点色**：顶面 100%、南北面 80%、东西面 60%、底面 50%（与原版 `RenderType.entityCutout` 的方向明暗一致），模型立刻获得方块感的体积光。
- **新增 `DemoEntityIdleAnimator`**：按骨骼驱动的程序化待机动画（正弦振荡骨骼局部旋转/位移），工厂元数据表为每个实体配置轨道——蜜蜂/蝙蝠扇翅（含翅尖跟随）、烈焰人 12 根火棒交错浮动+旋转、守卫者尾鳍三节摆动、海豚/鲑鱼摆尾、狼摇尾、蜘蛛腿部微颤、亡灵/劫掠者手臂摆动+头部扫视、羊/山羊点头、铁傀儡与雪傀儡手臂摆动等。棋子重建时自动重挂。
- **流浪者兜帽覆盖层**：提取配置新增 Java JAR 的 `stray_overlay.png`（`entity_stray_overlay`），流浪者以 0.55px 膨胀第二层渲染兜帽；绵羊羊毛层膨胀 1.0 → 1.2 更蓬松。
- 材质创建 API 增加 `emissiveBoost` 参数；实体染色固定为白色（原版贴图自带配色）。
- 测试：顶点色逐面明暗断言、蜜蜂扇翅动画轨道断言、流浪者兜帽覆盖层断言。
- 视觉验收：精化后截图 [`demo-voxel-models-preview-v2.png`](../design/assets/demo-voxel-models-preview-v2.png) 通过验收——逐面明暗带来的体积感、羊毛/海晶石像素纹理清晰、旧版均匀自发光造成的"洗平泛光"完全消除，无回归（对比基线见 `demo-voxel-models-preview-v1.png`）。

## 2026-09-13 阵营选择 → 场景显示一致性检查

对"选择阵营后战场是否随之切换"做了全链路核对（代码 + 自动化测试 + 实机截图）：

- **代码链路**：三条路径全部收敛到 `DemoBattlefield3D.SetBattlefieldThemes(playerFaction, opponentFaction)`——玩家侧栏按钮 `SelectFaction`、敌方轮换器 `SelectOpponentFaction`（锁定状态下拒绝切换并提示）、权威联机开局 `ApplyAuthoritativeFactionVisuals`。该调用同步更新：双方地形材质（主纹理/次纹理/副纹理 + 地基 + 远近边框）、七个部署格贴图与主题染色、双方环境灯光；UI 侧的玩家/敌方头像色、界面染色、头衔由 `ApplyPlayer/OpponentFactionVisuals` 同步更新。
- **自动化锁定**：EditMode 测试在七群系切换循环中新增断言——地形方块贴图、玩家/敌方地基贴图、玩家/敌方环境灯光颜色逐群系校验（连同既有的部署格贴图与染色断言），任何一方漂移都会导致测试失败。187/187 通过。
- **实机验证**：通过 `-previewPlayerFaction/-previewOpponentFaction` 命令行参数实拍两组此前未覆盖的组合，均通过视觉验收：
  - 沙漠 vs 末地 [`demo-faction-desert-end-preview-v1.png`](../design/assets/demo-faction-desert-end-preview-v1.png)：红砂岩/紫珀双方地形、红砂/紫珀部署格、沙漠卡组与 `db_` 效果槽、末影水晶悬浮体素模型。
  - 海洋 vs 深暗 [`demo-faction-ocean-cave-preview-v1.png`](../design/assets/demo-faction-ocean-cave-preview-v1.png)：海晶石/苔藓石砖双方地形、海洋卡组、敌方洞穴蝙蝠与洞穴蜘蛛模型。
  - 连同此前的平原 vs 下界（默认开局）、雪原 vs 海洋（`-previewGoat`）两组，四个组合、七种群系材质均有实机证据覆盖。
- 已知保留项不变：橡树/玄武岩柱等装饰为通用布景，不随群系切换。

## 2026-09-13 战场升级为真正的 2.5D 体素模型并修复生物渲染错误

### 背景

此前战场由两套"伪 2.5D"内容组成：

1. **背景与地面**：`DemoSceneBuilder` 把手绘背景图 `demo-battlefield-bg-v1.png` 写入 `DemoBattlefield3D.illustratedBackdrop`，一旦存在该图，`BuildNow()` 直接跳过真实体素地形 `BuildTerrain()` / `BuildBiomeDecor()`，只显示一块贴着手绘插画的全屏 Quad；七个群系主题则依赖 14 张手绘半场图（`field-{faction}-v1.png` / `field-{faction}-far-v1.png`）经 `DemoCompositeBackdrop.shader` 合成。
2. **场上生物**：`DemoMinecraftModelFactory` 用约 380 行手写的立方体 + 手调 UV 坐标拼出 17 种生物模型。这些 UV 是按旧版（重做前）Minecraft 实体贴图布局凭手估算的，而本地提取的实体贴图来自 1.21.10（狼 1.20.5 重做、蝙蝠 1.21.2 重做、鲑鱼 1.21.5 重做等），两者不匹配，导致脸、毛色、翅膜等大面积贴图错乱；透明贴片（蜂翼、烈焰棒等）的镂空也不稳定。

### 调研结论（开源/官方资料）

- Java 版客户端 JAR 中**并不包含**实体几何 JSON（实体模型硬编码在 Java 代码里；本机 1.20.1 与 1.21.10 JAR 均已验证）。
- 实体几何的公开数据源是 **Mojang 官方 bedrock-samples 仓库**（`resource_pack/models/entity/*.geo.json`，即 Blockbench 兼容的 `minecraft:geometry` 格式），配套 `resource_pack/textures/entity/` 贴图可以保证"几何 + 贴图"版本一致。参考资料：[Mojang/bedrock-samples](https://github.com/Mojang/bedrock-samples)、[Microsoft geometry 1.12.0 schema](https://learn.microsoft.com/en-us/minecraft/creator/reference/content/schemasreference/schemas/minecraftschema_geometry_1.12.0)、[Blockbench](https://blockbench.net/)、[skinview3d（box-UV 参考实现）](https://github.com/bs-community/skinview3d)、[SizableShrimp/EntityModelJson（Java 侧导出对照）](https://github.com/SizableShrimp/EntityModelJson)。
- 从实际数据确认的坐标/角度约定：模型正面朝 -Z；**基岩几何的 X 轴与 Java 镜像**（换算时 X 取反）；旋转为角度制，且 X/Z 轴符号与右手系相反（用羊 `bind_pose_rotation: [90,0,0]` 与蜜蜂翅膀姿态验证）；镜像立方体 `mirror` 只水平翻转 UV。

### 改动内容

**资源管线（生成物，均不提交 Git）**

- 新增 `scripts/extract-minecraft-entity-models.ps1` + `shared-schema/card-art/bedrock-entity-source.v1.json`：从 bedrock-samples 固定 commit（`7360724`）下载 17 个实体几何 JSON 与配套实体贴图（部分为 TGA），从本机 1.21.10 JAR 提取 `sheep_wool.png` 羊毛覆盖层，全部写入 `Assets/Generated/MinecraftWorldTextures/Resources/DemoWorld`，并记录 `entity-asset-provenance.local.json`（URL + SHA256）。
- `scripts/extract-minecraft-world-textures.ps1` 只再负责方块纹理；其实体贴图条目移交新脚本，避免同一资源两个来源。
- `scripts/build-demo.ps1 -WithMinecraftAssets` 现在会依次调用实体模型提取脚本。

**运行时（全部提交 Git）**

- 新增 `DemoJsonParser.cs`：无依赖的极简 JSON 解析器（支持 `minecraft:geometry` 与 legacy `geometry.*` 两种根格式）。
- 新增 `DemoMinecraftEntityGeometry.cs`：几何 DTO + 解析器（bones/cubes/uv/rotation/bind_pose_rotation/mirror/inflate/骨骼父子层级）。
- 新增 `DemoMinecraftEntityModelBuilder.cs`：按原版 box-UV 布局构建每个立方体网格（含镜像立方体 UV 翻转、零厚度双面贴片、立方体旋转烘焙、骨骼层级与枢轴、`inflate` 膨胀），并按每卡目标高度自动缩放、按悬停高度落地。生物模型**不再使用任何手写 UV**。
- 重写 `DemoMinecraftModelFactory.cs`：卡牌 → 几何模型/贴图/目标高度/悬停高度元数据表；羊增加羊毛覆盖层（第二层材质 + inflate）；烈焰人的 12 根火棒在几何里是靠动画摆放的，静态模型通过骨骼枢轴覆盖烘焙出经典三环姿态。幼年/小型变体（`tk_003`/`tk_004`/`tk_011`/`tk_014`）保留原有缩放。
- `DemoBattlefield3D`：删除插画背景 Quad 与 `CompositeBackdrop` 依赖；`BuildNow()` 永远构建真实体素地形与群系装饰；地形方块按群系主题取材并记录渲染器，`SetBattlefieldThemes` 运行时整体换材质（含地基、边框、部署格贴图）；部署格地面改用群系方块纹理平铺（不再屏幕投影手绘图）。实体材质改按贴图键缓存，并补上 `RenderType=TransparentCutout` 覆盖标签，修复 Standard 管线下透明贴片的裁剪。
- `DemoBattlefieldThemeCatalog`：由"近端/远端手绘图路径"重构为"群系 → 主/次/副地面方块纹理 + 地基纹理 + 颜色"映射，七个群系全部有真实方块纹理。
- `DemoSceneBuilder`：不再加载任何背景图，`Configure` 签名收敛为 `(blockShader, groundSurfaceShader)`。
- 删除不再使用的资源：`demo-battlefield-bg-v1.png`、14 张 `field-*-v1.png` / `field-*-far-v1.png`、`DemoCompositeBackdrop.shader`。
- `DemoWorldAssetProvider.CreateEntityMaterial` 增加 cutout 渲染队列标签（见上）。

**测试**

- 更新 `DemoLocalMatchTests.GeneratedSceneAndRuntimeHierarchyExist`：`Configure` 新签名、部署格贴图断言改为方块纹理、`_UseScreenProjection=0`、七群系切换断言改为群系主纹理、删除背景 Quad 断言，并新增"玩家/敌方体素地形已构建"断言。
- 新增 `DemoMinecraftEntityModelTests`：解析器（现代/legacy 格式、bind pose）、构建器（骨骼层级、UV 范围、自动缩放、X 镜像、悬停基面、旋转符号约定）、工厂元数据完整性，以及"本机已提取时对全部 17 个实体逐个建模并校验网格/UV/包围盒"的集成测试（未提取时自动 Ignore）。

### 已知限制

- 流浪者兜帽、骷髅披风等需要第二张贴图（overlay）的部位暂未实现双层渲染；烈焰人火棒的待机动画为静态烘焙姿势。
- 群系装饰（橡树、玄武岩柱）仍是通用布景，不随群系切换（地形、部署格、灯光已随群系切换）。
- 实体朝向、镜像与旋转约定来自对官方样本数据的推导与单元测试锁定，若个别模型出现左右贴图互换，调整 `DemoMinecraftEntityModelBuilder` 中 X 镜像与 -X/+X 区域映射即可。

### 视觉验收与追加修复

- 新增 `Demo/Editor/DemoPlaymodeCapture.cs`：命令行进入 Play 模式，复用运行时 `-captureDemo` 机制生成预览截图。
- 截图验收发现并修复：部署格地面在群系切换后仍保留旧染色（`grass_block_top` 等灰度纹理依赖主题色），`SetBattlefieldThemes` 现在同步更新部署格材质 `_Color`，并停止每帧把 `_Color` 强制回白色；测试新增"部署格染色随群系切换"断言。
- 预览截图（视觉验收通过）：[`demo-voxel-battlefield-preview-v1.png`](../design/assets/demo-voxel-battlefield-preview-v1.png)（平原 vs 下界：体素地形、岩浆怪与溺尸模型）、[`demo-voxel-models-preview-v1.png`](../design/assets/demo-voxel-models-preview-v1.png)（雪原 vs 海洋：守卫者、山羊、绵羊羊毛层、悬浮海豚）。

# Biome Rivals Demo UI 设计记录 v0.1

## 目标

这版 Demo 用一个可运行的纵向切片验证“选牌—查看详情—支付费用—部署/释放—结束回合”的基本闭环。视觉复杂度参考成熟数字卡牌游戏的信息层级，但不复刻任何现成产品界面；场景使用固定斜俯视正交摄像机，将真实 3D 方块棋盘与屏幕空间卡牌 UI 组合为 2.5D 战场。

运行时基准分辨率为 `1920×1080`，Canvas 使用 `Scale With Screen Size`。所有交互由运行时 UI 组件生成，场景只保留启动对象和背景资源，便于后续注册新卡、替换动画系统和进行自动化测试。

## 画面结构

| 区域 | 功能 | 视觉规则 |
|---|---|---|
| 顶部 | 对手信息、手牌与敌方战场 | 下界暗红、玄武岩、暖色边光 |
| 中部 | 双方单位格与建筑格、回合分隔 | 单位格 4 个、建筑格 3 个；上下地形允许采用不同坐标与节奏 |
| 底部 | 玩家信息、手牌扇形、费用 | 草原/森林绿、深色橡木、冷色描边 |
| 左侧 | 七群系快速切换 | 当前群系高亮；颜色由群系主题注册表驱动 |
| 右侧 | 卡牌检查器与主要操作 | 名称、类型、描述、数值、效果槽和部署提示集中显示 |

卡牌悬停、选中抬升、部署格高亮与回合反馈均使用短时平滑插值。正式动画库接入时应继续通过表现层接口隔离，避免动画直接修改规则状态。

### 背景与 UI 一致性约束

- 通用 HUD 只使用低饱和深板岩、旧木和羊皮纸色；青色仅表示能量、分界线和合法操作。
- 群系主色只进入卡牌框、激活按钮和少量状态点，不给整块外围面板染色。
- 空战场格采用半透明地面压印效果，已有单位才使用高不透明信息块。
- 背景统一覆盖轻微暗色罩层，保证文字可读，同时保留上下群系材质差异。

## 2.5D 场景分层

| 层级 | 职责 | 替换边界 |
|---|---|---|
| `DemoBattlefield3D` | 摄像机、灯光、精绘环境层、3D 世界槽位和单位 | 精绘背景负责整体材质质量，真实几何负责交互、遮挡和动画 |
| `DemoWorldAssetProvider` | 加载本机方块贴图与按 `cardId` 注册的 Prefab | 正式素材不应侵入回合和卡牌规则代码 |
| `DemoSceneController` | 手牌、检查器、点击热区和世界坐标到 UI 坐标投影 | 保持屏幕空间布局和输入职责 |
| `DemoLocalMatch` | 费用、槽位、连续建筑与回合状态 | 不访问场景对象或渲染组件 |

本机原型贴图通过 `scripts/extract-minecraft-world-textures.ps1` 从已拥有的 Java 客户端 JAR 按 17 项白名单提取到 Git 忽略目录。运行时先查找 `Resources/DemoWorld/Prefabs/{cardId}`；不存在时才生成程序化方块生物/建筑。因此以后注册正式 Prefab 不需要改部署逻辑。

默认场景采用“精绘背景 + 真实 3D 槽位/单位”的 2.5D 合成方式，不再用低精度程序化地形覆盖原有美术。程序化方块地形仅作为背景资源缺失时的可运行后备。进入性能阶段后，槽位之外的静态装饰应合并为少量 Mesh，单位和建筑继续保持独立对象。

## 素材与版权边界

- 本机提取的 Minecraft 图标只在 Editor 或 Development Build 中用于内部原型，并被 Git 忽略。
- 仓库中的公开预览强制使用原创菱形占位符，不包含 Minecraft 原版图标。
- 背景是项目专用生成素材，不含文字、Logo、角色、卡牌或其他游戏的可识别界面。
- 发布前仍需统一替换为具有明确授权的最终素材，并重新核对 Minecraft 使用规范。

## 战场背景生成记录

生成方式：Codex 内置 ImageGen。

最终提示词：

> Use case: stylized-concept. Asset type: 16:9 Unity digital card-game battlefield background. Create a polished empty block-built arena for a collectible card game. The upper half is a restrained Nether-like volcanic biome with dark basalt bricks, deep crimson accents, tiny controlled ember cracks and warm orange edge light. The lower half is a meadow-and-forest biome with dark oak planks, mossy stone, grass and subtle leaf details. Separate the halves with a narrow neutral deepslate lane and a faint turquoise river/rune accent. The arena must support overlay UI for four unit slots and three building slots on each side, but do not draw literal card slots; use subtle floor rhythm and material changes only. Upper and lower terrain geometry may differ organically while retaining balanced competitive readability. Style/medium: shippable modern digital card game environment, elegant blocky voxel-inspired materials, crisp game UI backdrop, restrained premium polish, not a screenshot and not concept sketch. Composition/framing: wide 16:9, straight-on shallow top-down view, perfectly centered central lane, large calm empty play areas, darkened outer edges reserved for HUD and buttons. Lighting/mood: controlled soft cinematic lighting, upper warm ember glow, lower cool natural moonlight, strong readable separation, no visual noise behind cards. Color palette: charcoal, deep crimson, ember orange, forest green, oak brown, muted turquoise. Constraints: absolutely no text, letters, numbers, logos, characters, creatures, cards, UI buttons, item icons, trademarks or watermark; no recognizable copied game interface; keep the central play surfaces uncluttered and high contrast for overlay elements. Avoid: busy scenery, tall objects blocking play space, excessive lava glow, photorealism, fisheye perspective, ornate fantasy filigree.

精绘环境层：`client-unity/Assets/Game/Demo/Art/demo-battlefield-bg-v1.png`。2.5D 版本保留它作为默认环境质感来源，并在其上叠加真实 3D 槽位、生物与建筑。

实际运行预览：[`assets/demo-runtime-preview-v1.png`](assets/demo-runtime-preview-v1.png)

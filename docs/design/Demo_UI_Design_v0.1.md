# Biome Rivals Demo UI 设计记录 v0.1

## 目标

这版 Demo 用一个可运行的纵向切片验证“选牌—查看详情—支付费用—部署/释放—结束回合”的基本闭环。视觉复杂度参考成熟数字卡牌游戏的信息层级，但不复刻任何现成产品界面；材质、形状与色彩使用方块、木板、深板岩、红石和群系分区表达 Minecraft 主题。

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

## 素材与版权边界

- 本机提取的 Minecraft 图标只在 Editor 或 Development Build 中用于内部原型，并被 Git 忽略。
- 仓库中的公开预览强制使用原创菱形占位符，不包含 Minecraft 原版图标。
- 背景是项目专用生成素材，不含文字、Logo、角色、卡牌或其他游戏的可识别界面。
- 发布前仍需统一替换为具有明确授权的最终素材，并重新核对 Minecraft 使用规范。

## 战场背景生成记录

生成方式：Codex 内置 ImageGen。

最终提示词：

> Use case: stylized-concept. Asset type: 16:9 Unity digital card-game battlefield background. Create a polished empty block-built arena for a collectible card game. The upper half is a restrained Nether-like volcanic biome with dark basalt bricks, deep crimson accents, tiny controlled ember cracks and warm orange edge light. The lower half is a meadow-and-forest biome with dark oak planks, mossy stone, grass and subtle leaf details. Separate the halves with a narrow neutral deepslate lane and a faint turquoise river/rune accent. The arena must support overlay UI for four unit slots and three building slots on each side, but do not draw literal card slots; use subtle floor rhythm and material changes only. Upper and lower terrain geometry may differ organically while retaining balanced competitive readability. Style/medium: shippable modern digital card game environment, elegant blocky voxel-inspired materials, crisp game UI backdrop, restrained premium polish, not a screenshot and not concept sketch. Composition/framing: wide 16:9, straight-on shallow top-down view, perfectly centered central lane, large calm empty play areas, darkened outer edges reserved for HUD and buttons. Lighting/mood: controlled soft cinematic lighting, upper warm ember glow, lower cool natural moonlight, strong readable separation, no visual noise behind cards. Color palette: charcoal, deep crimson, ember orange, forest green, oak brown, muted turquoise. Constraints: absolutely no text, letters, numbers, logos, characters, creatures, cards, UI buttons, item icons, trademarks or watermark; no recognizable copied game interface; keep the central play surfaces uncluttered and high contrast for overlay elements. Avoid: busy scenery, tall objects blocking play space, excessive lava glow, photorealism, fisheye perspective, ornate fantasy filigree.

项目资源：`client-unity/Assets/Game/Demo/Art/demo-battlefield-bg-v1.png`

实际运行预览：[`assets/demo-runtime-preview-v1.png`](assets/demo-runtime-preview-v1.png)

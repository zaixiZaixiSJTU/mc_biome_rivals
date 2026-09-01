# Biome Rivals Demo UI 设计记录 v0.1

## 目标

这版 Demo 用一个可运行的纵向切片验证“选牌—查看详情—支付费用—部署/释放—进入战斗—选择攻击者与目标—伤害/死亡—结束回合”的基本闭环。视觉复杂度参考成熟数字卡牌游戏的信息层级，但不复刻任何现成产品界面；场景使用与精绘背景消失方向匹配的固定斜俯视透视摄像机，将真实 3D 方块棋盘与屏幕空间卡牌 UI 组合为 2.5D 战场。

运行时基准分辨率为 `1920×1080`，Canvas 使用 `Scale With Screen Size`。所有交互由运行时 UI 组件生成，场景只保留启动对象和背景资源，便于后续注册新卡、替换动画系统和进行自动化测试。

通用 HUD 的像素规范集中在 `DemoUiMetrics`：Canvas 开启 `Pixel Perfect`，CanvasScaler 与运行时创建的 UI Sprite 统一使用 `16 PPU`。容器边框不再由四条边和四个角分别拉伸，而是由带 `5 px` Border 的 `Image.Type.Sliced` 九宫格渲染；内部材质使用同 PPU 的 `Image.Type.Tiled` 平铺。`CardDetailsPanel` 的内容安全边距为每边 `7 px`，避免边框吞占详情卡牌的有效面积。以上参数有 EditMode 层级与数值断言，修改时必须同步更新视觉验收图和测试。

UGUI 样式由 `DemoUiStyleCatalog` 集中提供材质、底色、描边和交互色，不允许业务代码直接拼装临时配色。运行时对象挂载对应的样式组件，后续转为 Prefab 时保留同一语义：

| 样式组件 | 使用范围 | 材质规则 |
|---|---|---|
| `BasePanel` | 左右侧栏、顶部信息、底部手牌底板、状态容器 | 暗色石砖边框 + 深色黑石内板 |
| `SecondaryButton` | 群系列表项、普通释放/确认操作 | 与 BasePanel 同源的中性石砖，仅用亮度反馈悬停和按下 |
| `PrimaryActionButton` | 唯一高优先级操作 `EndTurnButton` | 海晶高亮边框 + 深青内板；同屏不得出现第二个主按钮 |

群系按钮的主题色只显示为左侧 `3 px` 选中标记，不再改变整块按钮材质。这样主题信息仍可辨识，同时不会重新引入木质、下界砖、海晶砖混搭的外围 HUD。

三份 Prefab 位于 `Assets/Game/Demo/UI/Resources/DemoUI/Prefabs`，由 `DemoUiPrefabBuilder` 生成并纳入场景构建流程；运行时通过 `DemoUiPrefabProvider` 加载，资源临时缺失时才使用等价的组件化回退构建。

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
- 队伍栏、检查器、群系栏和回合控件不得使用纯色半透明矩形配矢量描边；容器统一由深色黑石砖内板、可平铺石砖/木板/下界砖/海晶砖边框、像素角件和金属铆钉构成，形成可被“打造”出来的实体感。
- 材质组件优先读取本机 Minecraft JAR 白名单提取纹理；缺少本地资源时使用同尺寸、点采样、可重复平铺的确定性像素后备纹理，禁止因此退回 Web/App 风格边框。
- 卡牌是独立视觉系统：手牌与详情卡统一使用 `card-frame-theme-study-v1.png` 的七群系框体结构，不得套用通用 HUD 的石砖/木板容器边框；卡牌内容继续由注册数据动态填充。
- 群系主色只进入卡牌框、激活按钮和少量状态点，不给整块外围面板染色。
- 空战场格默认不绘制任何框或编号；合法部署时直接提高对应地砖表面的亮度并轻微强调 Minecraft 式方块边缘，悬停合法地块时地砖材质变金、整体抬升并露出侧壁，已有单位的地块由 3D 模型占据。
- 背景统一覆盖轻微暗色罩层，保证文字可读，同时保留上下群系材质差异。

## 2.5D 场景分层

| 层级 | 职责 | 替换边界 |
|---|---|---|
| `DemoBattlefield3D` | 摄像机、灯光、精绘环境层、3D 世界槽位和单位 | 精绘背景负责整体材质质量，真实几何负责交互、遮挡和动画 |
| `DemoWorldAssetProvider` | 加载本机方块贴图与按 `cardId` 注册的 Prefab | 正式素材不应侵入回合和卡牌规则代码 |
| `DemoMinecraftModelFactory` | 将已注册单位构造成 Minecraft 式分件方块模型并套用对应生物皮肤 | 只负责后备表现；正式 Prefab 仍拥有最高优先级 |
| `DemoBattlefieldPointerController` | 从主相机发射 3D 射线，维护双方槽位的悬停、按下和点击状态 | 只命中带 `DemoBattlefieldSlotTarget` 的地表，不依赖 Canvas 格子位置 |
| `DemoSceneController` | 手牌、检查器、规则操作回调和世界坐标到文字 UI 的投影 | 保持屏幕空间布局；不创建部署格 Graphic/Button |
| `DemoLocalMatch` | 费用、手牌/牌库/弃牌、抽牌/爆牌/疲劳、`effectId` 效果、槽位、阶段、战场实例、攻击/反击、死亡与英雄生命/护甲 | 不访问场景对象或渲染组件；命令 DTO 与联网网关保持一致 |

手牌底板左下角持续显示“手牌 / 牌库 / 弃牌”三个区域计数，信息使用与 HUD 相同的低对比度浅色文字，不额外叠加 Web 风格浮层。新回合抽到的卡直接进入扇形手牌；满 7 张时卡牌公开进入弃牌堆，空牌库时状态板显示本次疲劳伤害。离线展示牌组为 5 张展示手牌加 25 张临时群系循环牌库，用于 UI 和回合测试；正式权威规则按 GDD 使用 30 张牌组与 3/4 张起手。

本机原型贴图通过 `scripts/extract-minecraft-world-textures.ps1` 从已拥有的 Java 客户端 JAR 按白名单提取 20 张方块贴图和 11 张生物皮肤到 Git 忽略目录。运行时先查找 `Resources/DemoWorld/Prefabs/{cardId}`；不存在时由 `DemoMinecraftModelFactory` 为已注册的蜜蜂、绵羊、狼、村民、岩浆怪、烈焰人、流浪者、鲑鱼、海豚、溺尸与守卫者构造 Minecraft 式分件模型；珊瑚礁直接使用 `tube_coral_block`，海底神殿则组合 `prismarine_bricks`、`dark_prismarine` 与 `sea_lantern` 构成横跨三个建筑格的体素建筑，其余对象才使用通用后备模型。因此以后注册正式 Prefab 不需要改部署逻辑。

部署、攻击与卡牌目标槽位不再使用屏幕空间矩形、透明 `Graphic` 或 UGUI `Button` 命中。每个槽位由一个合并的地砖顶面 Mesh、同 Mesh 的 `MeshCollider`、`DemoBattlefieldSlotTarget` 语义组件，以及一个仅在抬升时显示的合并侧壁 Mesh 组成，不为单块地砖创建 Renderer 或 Collider。`DemoBattlefieldPointerController` 使用主相机 `ScreenPointToRay` 在 3D 世界中寻找最近的双方语义槽位；UI 面板遮挡指针时停止世界命中。战斗阶段先持续高亮合法己方攻击者，选择后保持攻击者地砖的按下亮度，并点亮敌方生物/建筑目标；敌方英雄使用其实体 HUD 面板作为明确的点击目标。有目标卡牌进入独立选择态，已占用的合法地表直接发光而不抬升生物模型，右键或 Esc 可取消。

结构部署不再把每个空建筑格误当成合法起点。`DemoDeploymentRules` 会同时校验回合、阶段、手牌、费用、行类型、边界与整段连续空位；空闲完整范围以青色脉冲提示，悬停后全部待占地砖同步变金，越界或与已有对象相交时实际可见范围同步变红，点击非法范围只显示本地原因而不发送网络命令。结构落地后由一个稳定实例驱动一个居中的 3D 模型，模型宽度由首尾地块的世界坐标计算；任意占用格都映射到同一对象、共享生命，死亡事件一次释放完整范围。相邻同名建筑通过实例 ID 区分，不能再用相邻 `cardId` 猜测它们是否属于同一结构。

带配方的部署牌在卡牌详情下方复用石砖 `SecondaryButton`，并排显示“红石支付”和“合成支付”；当前选择使用菱形标记与暖金/青色材质区分，不增加 Web 风格开关。配方直接显示本地化材料名与数量。材料不足时，缺口文字、底部状态和候选地表范围同步变红；材料齐全时才显示合成收益。首个纵向切片是“沙漠神殿 = 可疑的沙子×1 + 陶片×1”，合成部署后从 8 点提升到 10 点最大生命。牌库计数旁仅在存在掩埋牌时追加“掩埋 n”，不显示位置；权威 `CARD_BURIED` / `CARD_EXCAVATED` 通过状态提示和像素横幅表现，出土后再播放正常抽牌。可用 `-previewCrafting` 与 `-previewCraftingMissing` 分别生成完整/缺料预览，基准图 `demo-crafting-payment-preview-v1.png` 与 `demo-crafting-missing-preview-v1.png` 已同步为新配方。

DB-003 的牌库查看使用全屏阻断式 `ChoiceOverlay`，容器、按钮与卡槽继续复用暗色石砖 `BasePanel` / `PrimaryActionButton`，只用沙漠暖金表示“可出土”和选中态。拥有者看到三张真实卡面；不可选卡仍可阅读，但标记为“保持牌库顺序”，只有带掩埋状态的卡能点击。若没有掩埋牌，主按钮明确显示“确认未发现”。非拥有者只看到三张保密卡背和等待文案，客户端不会获得真实 `cardId` 或 `selectable`。选择存在期间，手牌、世界空间战场、阵营切换与回合按钮均锁定；可用 `-previewArchaeology` 生成确定性视觉回归图 `demo-archaeology-choice-preview-v1.png`。

DB-005 的动态费用直接复用卡面左上角费用槽，不增加独立 Web 风格提示框。当前回合出土后，手牌与右侧详情中的数字同步从 3 变为高可读性的浅绿色 2，费用槽右下角显示小型 `-1` 像素标记，详情区补充“考古触发”说明；回合结束后全部恢复。`-previewRaiderDiscount` 用于生成确定性视觉回归图 `demo-raider-discount-preview-v1.png`。

地砖顶点经过真实 Model/View/Projection 变换，透视摄像机使远端地砖自然收窄；`DemoGroundSurface` Shader 的屏幕投影只用于采样精绘背景纹理，几何形变和深度遮挡仍完全由世界 Mesh 与相机矩阵决定。未激活地砖因此保留背景原位置的草、石路或木板纹理，高亮则直接修改地砖表面及方块边缘亮度。交互顺序为“合法目标脉冲—悬停变金并抬升—按下压低并收缩—松开后回弹或部署单位”，规则状态仍只存在于 `DemoLocalMatch`。

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

战斗阶段预览：[`assets/demo-combat-phase-preview-v1.png`](assets/demo-combat-phase-preview-v1.png)。进入战斗后手牌只降低内容亮度，石砖底板保持不变；检查器切换为战斗指令，主按钮切换为“结束回合”。

嘲讽目标预览：[`assets/demo-taunt-targeting-preview-v1.png`](assets/demo-taunt-targeting-preview-v1.png)。选定攻击者后，权威目标规则只允许嘲讽对象；强制目标直接改变真实地表材质为脉冲金色，非嘲讽对象不显示可交互光效，英雄面板同步禁用。场内铭牌显示关键词，避免只依赖颜色传达规则。

潜影贝亡语预览：[`assets/demo-shulker-deathrattle-preview-v1.png`](assets/demo-shulker-deathrattle-preview-v1.png)。潜影贝死亡后离开真实战场格，衍生的潜影壳进入己方手牌并复用同一卡面/详情组件；在线事件只向拥有者公开手牌中的卡牌身份，满手转弃牌时则公开。

尸壳掉落预览：[`assets/demo-loot-drop-preview-v1.png`](assets/demo-loot-drop-preview-v1.png)。铁傀儡击杀敌方尸壳后，腐肉作为战利品进入击杀者手牌并复用完整卡面/详情组件；底部状态明确显示伤害、反击、死亡和掉落结果。联机表现沿用 `CARD_GENERATED` 的私有手牌投影，并以金色“战利品”横幅区分于亡语。

地牢骷髅连锁预览：[`assets/demo-dungeon-skeleton-preview-v1.png`](assets/demo-dungeon-skeleton-preview-v1.png)。铁傀儡击杀地牢骷髅后，亡语先随机命中一个仍存活的合法敌方生物，再结算骨头掉落；伤害后的 5/3 铁傀儡保留在真实地表，骨头在手牌和详情视图中复用同一雪原材质卡面。状态板按实际事件顺序逐行呈现攻击、亡语与掉落。

岩浆怪召唤预览：[`assets/demo-magma-summon-preview-v1.png`](assets/demo-magma-summon-preview-v1.png)。同批死亡对象先全部离场，随后小型岩浆怪以新的稳定实例 ID 出现在来源释放格；模型复用 Minecraft 岩浆怪纹理与分层方块结构，并缩放为成体的 `62%`，出生位置通过真实地表材质短促发光。

多格结构部署预览：[`assets/demo-structure-placement-preview-v1.png`](assets/demo-structure-placement-preview-v1.png)。选中两格“沙漠神殿”后，建筑格 1—2 的真实地表作为一个候选范围同步变金；右侧提示同时给出占格数和确切格号。[`非法起点预览`](assets/demo-structure-placement-invalid-preview-v1.png) 使用高饱和深红地表和红色原因文字反馈越界，[`落地结构预览`](assets/demo-structure-deployed-preview-v1.png) 则证明一个实例模型居中横跨建筑格 1—2。重叠与三格结构释放另由规则/回放测试覆盖。

权威效果表现预览：[`assets/demo-card-effect-preview-v1.png`](assets/demo-card-effect-preview-v1.png)。已实现的法术/材料使用明确的“释放卡牌”动作，结算后同步刷新生命、护甲、手牌与区域计数，并在英雄实体 HUD 上播放与效果类型对应的短促颜色脉冲；待实现效果的按钮显示“效果尚未接入”并禁用。

权威起手调度预览：[`assets/demo-mulligan-preview-v1.png`](assets/demo-mulligan-preview-v1.png)。调度层复用主题卡面、暗色石砖底板和主行动按钮，不引入独立 Web 弹窗风格；选中卡牌通过整块材质色调变化、轻微抬升和“将替换”状态文字共同反馈。

有目标卡牌预览：[`assets/demo-targeted-card-preview-v1.png`](assets/demo-targeted-card-preview-v1.png)。`雪球`先进入目标选择态，只点亮合法敌方单位所在的真实 3D 地表；命令携带稳定对象实例 ID，服务端在扣费和弃牌前完成存活、阵营与类型校验。

珊瑚礁成长预览：[`assets/demo-coral-reef-preview-v1.png`](assets/demo-coral-reef-preview-v1.png)。建筑使用本机 Minecraft `tube_coral_block` 纹理构成体素珊瑚簇；待触发时建筑格地表以低强度粉紫色脉冲提示持续效果，单位铭牌显示结算后的永久生命，右侧卡牌与手牌继续复用同一材质化卡面。

海底神殿结束阶段预览：[`assets/demo-ocean-monument-preview-v1.png`](assets/demo-ocean-monument-preview-v1.png)。神殿是一个横跨三个建筑格的连续体素对象；相邻单位保持普通地表，孤立单位同时获得橙红地表材质脉冲与“神殿锁定”铭牌，所有标记都随 3D 地块接受同一相机透视。

驯服的狼相邻战吼预览：[`assets/demo-tamed-wolf-preview-v1.png`](assets/demo-tamed-wolf-preview-v1.png)。已成长的狼铭牌显示永久生命；选中另一张狼时，与动物相邻的可部署地块以金色贴地材质和轻微抬升反馈显示，远离动物的合法格继续使用普通青绿色，从落位前即可读出战吼结果。

村民农夫小麦战吼预览：[`assets/demo-villager-farmer-preview-v1.png`](assets/demo-villager-farmer-preview-v1.png)。村民农夫部署后保留 Minecraft 村民模型，并将小麦生成至己方手牌；新牌直接复用手牌与右侧详情的统一卡牌预制体。联机事件只向拥有者公开小麦身份；部署前达到七张手牌时，农夫先离手再生成小麦，因此合法流程仍保持七张而不会误判为爆牌。

林地苗圃动物成长预览：[`assets/demo-woodland-nursery-preview-v1.png`](assets/demo-woodland-nursery-preview-v1.png)。苗圃使用橡木花床、泥土和橡树叶构成低矮体素建筑，不再使用通用双塔占位模型；上一回合获得永久生命的放牧绵羊以 `2/4` 留在真实单位格。当前回合重新就绪后，苗圃建筑格以叶绿色贴地材质脉冲提示持续效果，与珊瑚礁的粉紫色状态明确区分。

林间集结空间替代预览：[`assets/demo-woodland-rally-preview-v1.png`](assets/demo-woodland-rally-preview-v1.png)。己方单位排只剩一个空格时，第一步在真实地表召唤使用 Minecraft 狼模型的林地伙伴，第二步因场地已满改为抽牌；右侧仍展示效果来源卡，底部手牌展示实际抽到的牌，避免只用状态文字暗示结算结果。

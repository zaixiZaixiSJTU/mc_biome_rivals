# Unity Client

这是锁定到 Unity 6.0 中国版 `6000.0.28f1c1` 的客户端工程。

## 首次打开

1. 安装 Unity `6000.0.28f1c1`；模块至少选择 Windows Build Support (IL2CPP) 和 Microsoft Visual Studio Community（已有 IDE 可不选）。
2. 用 Hub 打开本目录。若提示补丁版本升级，记录并审查 `ProjectSettings`/`Packages` 变化。
3. 等待 Package Manager 完成解析，在 Test Runner 中运行 EditMode 测试。
4. 创建 `Bootstrap` 场景；`GameCompositionRoot` 会在任何场景载入前自动建立，所以空场景也能启动基础设施。

## 命令行验证与画面排查

仓库使用 Unity CLI 调用项目锁定的 Editor，适合在 Editor 图形界面异常时检查许可证、脚本编译、EditMode 和 Development Player。以下命令从仓库根目录运行；关闭正在打开同一项目的 Unity Editor，避免 Library/测试进程冲突。

```powershell
# 检查 CLI、Editor、许可证与构建环境
unity doctor
unity license status --json

# 运行完整 EditMode 套件并保留 NUnit XML
New-Item -ItemType Directory -Force .\Temp | Out-Null
unity test .\client-unity --mode EditMode --timeout 900 `
  --output .\Temp\unity-editmode.xml

# 构建与当前源码绑定的 Windows Development Player
New-Item -ItemType Directory -Force .\Temp\DemoBuild | Out-Null
unity build .\client-unity --target StandaloneWindows64 `
  --execute-method BiomeRivals.Demo.Editor.DemoBuildAutomation.BuildWindowsFromCommandLine `
  --output-path .\Temp\DemoBuild\BiomeRivalsDemo.exe `
  --allow-dirty-build --timeout 1200 --no-tail

# 非 Development 编译隔离审查（新目录；不是可发行包）
unity build .\client-unity --target StandaloneWindows64 `
  --execute-method BiomeRivals.Demo.Editor.DemoBuildAutomation.BuildWindowsNonDevelopmentFromCommandLine `
  --output-path .\Temp\NonDevelopmentAudit\BiomeRivalsDemo.exe `
  --allow-dirty-build --timeout 1200 --no-tail

# 读取真正的 Mono 编译产物；Cecil 路径取本机锁定 Editor 的安装目录
.\scripts\assert-demo-runtime-boundary.ps1 `
  -ExecutablePath .\Temp\NonDevelopmentAudit\BiomeRivalsDemo.exe `
  -ProjectPath .\client-unity -ExpectedMode NonDevelopment `
  -CecilLibraryPath '<Editor>/Data/MonoBleedingEdge/lib/mono/gac/Mono.Cecil/0.11.1.0__0738eb9f132ed756/Mono.Cecil.dll'

# 两种本机 Player 都必须校验74张注册卡图，而不只是源码 manifest
.\scripts\assert-demo-card-art-package.ps1 `
  -ExecutablePath .\Temp\NonDevelopmentAudit\BiomeRivalsDemo.exe -ProjectPath .\client-unity

# 运行确定性战斗预览并留存 PNG、日志与源码清单报告
 .\scripts\capture-demo-preview.ps1 `
  -ExecutablePath .\Temp\DemoBuild\BiomeRivalsDemo.exe `
  -ProjectPath .\client-unity -PreviewCombatInteraction `
  -PlayerFaction plains_forest -OpponentFaction desert_badlands `
  -CaptureWidth 1280 -CaptureHeight 720 `
  -CapturePath .\Temp\combat-1280x720.png

# 实际结算一场英雄攻击致胜，检查持久胜负结果面板
.\scripts\capture-demo-preview.ps1 `
  -ExecutablePath .\Temp\DemoBuild\BiomeRivalsDemo.exe `
  -ProjectPath .\client-unity -PreviewMatchOutcome `
  -PlayerFaction ocean_river -OpponentFaction desert_badlands `
  -CaptureWidth 1280 -CaptureHeight 720 `
  -CapturePath .\Temp\match-outcome-1280x720.png
```

也可把截图画幅改为 `1920×1080`，并指定另一个 PNG 路径。截图脚本会验证 Player 退出码、确定性玩法日志、构建时记录的 Unity 源码清单和 PNG 尺寸；同一路径已有截图时会拒绝覆盖。`unity test --filter` 在当前 CLI/项目组合中可能在 XML 已生成后仍滞留到超时，常规回归应运行无筛选全量套件。若机器上安装多个 Editor，可用 `--editor-path` 显式指定 `6000.0.28f1c1`。Unity Editor 日志通常位于 `%LOCALAPPDATA%\Unity\Editor\Editor.log`，Player 与构建日志保存在 Unity `Logs` 或截图旁的 `.player.log`。

CLI 可以确认编译/规则测试并生成可检查的 Player 截图，但不能替代对真实 Editor 窗口故障的诊断，也不能用离线预览替代 Docker/Nakama 双客户端验收。

本机构建的开发版和非开发版现在都携带侧车目录 `MinecraftCardIcons`，必须连同 exe/Data 一起保留。构建前验证注册名称/提取记录/74张图片hash；manifest记录 `packagedCardArt`，上述包检查会拒绝缺失、篡改或多余图片。Player只从自身包目录读取卡图，不搜索当前工作目录或源码；Editor才读工程Generated目录。图片、模型与提取记录仍是本机自有安装的本地审查素材，非开发编译并不意味着可发布/提交这些资源。

## 第三方依赖边界

- Nakama Unity SDK 已通过 UPM 锁定到 `v3.21.1`。只有 `NakamaMatchTransport` 与 Networking 程序集引用 `NakamaRuntime`；规则、卡牌内容和 UI 不直接引用 SDK 类型。
- DOTween：导入并锁定版本，实现 `ITweenService`；业务 Presenter 只能依赖该接口。

不要直接把 Nakama 客户端散落在 UI 中，也不要从卡牌规则代码直接调用 DOTween。这样断线模拟、离线测试和以后替换依赖都能保持局部修改。

## 部署拒绝专项审查

使用 `scripts/validate-online-demo.ps1 -DeploymentRejections -RenderOnlineUi -TimeoutSeconds 300 -ExpectedArena <权威场地ID>`，同时指定来源匹配的 Development Player 和全新输出目录。它与其他专项互斥，使用普通洞穴卡组；每端真实发送 7/8 条非法部署命令，逐条检查拒绝关联、无事件、无支付及两次强制重连取得的完整查看者快照一致，并通过实际 UI 合法部署和接受后续命令。该投影不包含对方秘密牌序；服务端全秘密状态不变由独立领域单测覆盖。详见 [QA-056](../docs/development/task-packets/QA-056.md)。发布版不启用此入口。

## 本地联机

在线验收和截图验收共用 [Player 来源检查](../scripts/assert-demo-player-source.ps1)，先按构建清单逐项检查当前 Unity 源码；旧 Player 会在启动前被拒绝。在线脚本每次使用独立输出目录，不删除旧报告，并检查两端退出码、公共生命/棋盘计数和强制重连。可用 -ServerHost、-ServerPort、-ServerScheme 连接隔离服务，-OutputDirectory 指定尚不存在的报告目录；连接参数只临时覆盖本进程环境，退出后恢复。

    scripts/validate-online-demo.ps1 -ExecutablePath "<当前构建的 Windows Player.exe>" -ServerPort 18350 -OutputDirectory "<新的报告目录>"

该自动动作探针使用真实 Unity Player 和网络/权威状态链，但以无图形模式运行，不替代卡面、3D 指针和动画的视觉验收。2026-10-03 默认 17350 服务仍加载旧协议，验收服务为隔离的 18350；不要把验证失败误判为需要降级客户端协议。

在线即时回手专项复用同一入口，分别运行两张来源牌；每次使用新的输出目录：

    scripts/validate-online-demo.ps1 -ExecutablePath "<当前构建的 Windows Player.exe>" -ServerPort 18350 -ReturnCardId ed_002 -OutputDirectory "<新的 ED-002 报告目录>"
    scripts/validate-online-demo.ps1 -ExecutablePath "<当前构建的 Windows Player.exe>" -ServerPort 18350 -ReturnCardId ed_005 -OutputDirectory "<新的 ED-005 报告目录>"

专项让两个末地客户端通过普通匹配进入同一局。四张起手牌的座位执行合法部署，再经真实手牌 GraphicRaycaster 点击、详情 Cast 按钮和 3D 战场指针出牌；不注入牌库、能量或测试夹具。双方分别核验回手事件、私有投影、格位清空和强制重连，拥有者还核验精确实例的手牌/详情 CardUI 费用；随后结束回合检查到期与最终收敛。随机牌库可能导致超时，失败目录必须保留，新尝试用新目录。

同机公开事实 barrier 只防止一端重连未完另一端提前结束回合；不传手牌身份/费用，不修改对局。报告会核对双方公开事件顺序哈希。该探针仍为无图形 Player：证明 UI/指针事件链及实际 UI 组件绑定，不证明动画画面正确；视觉验收单独使用图形 Player 截图。报告校验器的正负控制已接入共享验证入口和现有 CI。

默认连接参数位于 `Assets/Game/Networking/Resources/Networking/nakama-connection.v1.json`，与根目录 `docker-compose.yml` 对齐；本机 HTTP 端口为 `17350`，映射到容器内 Nakama 标准端口 `7350`，以避开 Windows Hyper-V 常见的 `73xx` 保留段。主机、端口、协议和 server key 可分别用 `BIOME_RIVALS_NAKAMA_HOST`、`BIOME_RIVALS_NAKAMA_PORT`、`BIOME_RIVALS_NAKAMA_SCHEME`、`BIOME_RIVALS_NAKAMA_SERVER_KEY` 覆盖。

Demo 顶部的联机状态条用于认证、Socket、匹配、权威 Match 加入和重连。点击联机时会把当前己方群系作为 `factionId` 匹配属性提交，并在连接生命周期结束前锁定阵营选择；对手阵营由另一名玩家独立选择，不能由本机预设。收到私有快照后，`DemoOnlineMatchSession` 会切换到权威棋盘视图；双方群系、手牌、能量、生命、阶段、双排槽位与生物状态均来自 `MatchStateStore`。部署、施法、进入战斗、攻击和结束回合不做本地乐观结算，必须等 `acknowledgedCommandId` 对应的事件批次后才更新界面。

权威对局首先进入材质化起手调度层：玩家可点击任意起手牌标记替换，确认后显示双方准备状态；服务端完成“移出旧牌—抽替换牌—旧牌洗回”后才投影新的私有手牌。两人都确认前，手牌区、战场和回合按钮保持锁定。`-previewMulligan` 可在离线 Windows 构建中只预览该界面，用于视觉回归，不改变本地规则状态。

卡牌目标选择由稳定 `effectId` 规则注册，不再写死为敌方单位：当前可区分敌方生物、己方生物与己方建筑/结构，并以贴地高亮只标记合法目标。离线规则镜像与 Nakama 权威规则共同支持 62 个已注册效果；未实现的 `PENDING` 效果仍会在扣费前拒绝。

`流浪者`采用两阶段部署：先在 3D 战场选择敌方生物作为战吼目标，再选择己方单位格；任一步取消或目标失效都不会提前扣费。战吼施加不减攻击的`缓慢 1`，被敌方击杀时掉落`骨头`。`粉雪桶`可把同一状态强化到绑定 -2 攻击；联网事件与重连快照都保留来源、规则绑定值、实际修正和持续时间，场上标签以冰蓝色显示。可用 `-previewStray` 与 `-previewSlow` 生成确定性预览。

`可疑的沙子`会掩埋`陶片`并立即获得护甲；HUD 只公开掩埋数量，`CARD_EXCAVATED` 公开陶片、更新手牌/弃牌堆后继续正常抽牌。`沙漠考古学家`使用阻断式石砖选择层：拥有者查看牌库顶三张并只能选取金色标记的掩埋牌，对手只看到保密占位；选择期间手牌、战场、阵营和阶段按钮全部锁定。任一出土方式都会在本回合激活`恶地劫掠者`：手牌与详情卡面的费用槽实时从 3 变为绿色 2，并显示 `-1` 像素标记；回合结束恢复。`潜影贝`亡语会生成`潜影壳`；`CARD_GENERATED` 在己方手牌中显示真实卡牌，在敌方手牌中只增加未知占位，满手转弃牌时双方都能看到卡牌身份。`岩浆怪`亡语通过 `OBJECT_SUMMONED` 在释放格生成 1/1`小型岩浆怪`，客户端从事件恢复稳定实例与格位，并以贴地脉冲提示出生位置。

普通攻击同样先由规则视图计算合法目标。存在嘲讽单位时，英雄面板和非嘲讽对象会禁用，嘲讽对象使用金色地表材质高亮并在场内铭牌显示“嘲讽”；`CHARGE` 关键词来自权威战场快照，可绕过召唤回合攻击限制。`-previewTaunt` 可生成包含已选攻击者、嘲讽与普通目标的离线视觉回归场景。

`-previewDeathrattle` 会建立潜影贝对铁傀儡的确定性结算场景，让潜影贝死亡并将生成的潜影壳选入手牌，可用于检查亡语反馈、衍生卡卡面和区域计数。

`-previewLoot` 会让铁傀儡击杀敌方尸壳，将掉落的腐肉置入己方手牌并选中卡面，用于检查击杀归属、战利品提示与私有手牌投影。

`-previewDungeonSkeleton` 会让铁傀儡击杀敌方地牢骷髅：亡语先随机造成 1 点伤害，再将骨头交给击杀者；预览选中骨头并保留受伤后的战场对象，用于检查触发顺序、伤害脉冲和战利品卡面。

`-previewSummon` 会让岩浆怪攻击铁傀儡并死亡，在原单位格召唤缩小版 Minecraft 岩浆怪模型，用于检查战场召唤回放、格位复用与出生高亮。

多格结构的部署预览由 `DemoDeploymentRules` 从当前规则视图计算完整占格范围。只有连续空闲且未越界的起点保持可用；悬停合法起点会同时点亮全部待占地砖，非法范围使用红色地表反馈，并在本地预检阶段阻止无意义的联机命令。`-previewStructurePlacement`、`-previewStructurePlacementInvalid` 与 `-previewStructureDeployed` 分别用于合法范围、越界范围及完成部署后的视觉回归。战场模型按稳定实例而不是相邻 `cardId` 去重，因此两个相邻同名建筑仍会显示为两个对象，结构则只生成一个居中横跨全部占格的 3D 模型。

自动化双客户端验证可用 `-autoOnline -autoOnlineAction -nakamaDeviceId <独立ID> -onlineProbe <报告路径>`。测试设备覆盖值拥有独立的会话缓存键，不会让同机两个进程误用同一玩家身份。

在 Nakama Docker 服务健康且 Windows Demo 已生成后，可从仓库根目录执行 `scripts/validate-online-demo.ps1`。脚本会以独立设备 ID 隐藏启动两个客户端，校验它们进入同一权威对局、完成起手调度、保留各自群系投影，并只终止本次启动的进程。

建筑末端/多格结构专项使用 `-BuildingDeployment -RenderOnlineUi -ExpectedArena <权威场地ID>`，必须指定新的输出目录和来源匹配的 Development Player。它用两个普通洞穴卡组，实际手牌/3D 指针部署，再冻结并恢复双方完整战场；不改卡组、资源或服务器场地。服务器审查配置需另行选择场地，并在 server build 完成后启动，不能与清理构建并行。建筑证据见 [QA-054](../docs/development/task-packets/QA-054.md)。

山羊末端移动专项使用 `-GoatMovement -RenderOnlineUi -ExpectedArena <权威场地ID>`，与其他专项互斥；同样要求新输出目录与来源匹配的 Development Player。两个普通雪原卡组通过正常调度/抽牌/支付，mover 实际选择雪傀儡、山羊手牌、BattlecryTarget 动作按钮、友军模型及中间部署地表；observer 只观察，不伪报支付。双方验证唯一移动事件/精确末格、模型、私有投影和冻结强制重连。3/4/5 单位容量实跑已过，脚本在任一 Player 非零退出时提前失败并只清理本次客户端；证据与剩余服务端非法命令范围见 [QA-055](../docs/development/task-packets/QA-055.md)。诊断入口仅 Editor/Development Build 可运行，发布版不启用。

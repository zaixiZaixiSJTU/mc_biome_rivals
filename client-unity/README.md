# Unity Client

这是锁定到 Unity 6.0 中国版 `6000.0.28f1c1` 的客户端工程。

## 首次打开

1. 安装 Unity `6000.0.28f1c1`；模块至少选择 Windows Build Support (IL2CPP) 和 Microsoft Visual Studio Community（已有 IDE 可不选）。
2. 用 Hub 打开本目录。若提示补丁版本升级，记录并审查 `ProjectSettings`/`Packages` 变化。
3. 等待 Package Manager 完成解析，在 Test Runner 中运行 EditMode 测试。
4. 创建 `Bootstrap` 场景；`GameCompositionRoot` 会在任何场景载入前自动建立，所以空场景也能启动基础设施。

## 第三方依赖边界

- Nakama Unity SDK 已通过 UPM 锁定到 `v3.21.1`。只有 `NakamaMatchTransport` 与 Networking 程序集引用 `NakamaRuntime`；规则、卡牌内容和 UI 不直接引用 SDK 类型。
- DOTween：导入并锁定版本，实现 `ITweenService`；业务 Presenter 只能依赖该接口。

不要直接把 Nakama 客户端散落在 UI 中，也不要从卡牌规则代码直接调用 DOTween。这样断线模拟、离线测试和以后替换依赖都能保持局部修改。

## 本地联机

默认连接参数位于 `Assets/Game/Networking/Resources/Networking/nakama-connection.v1.json`，与根目录 `docker-compose.yml` 对齐。主机、端口、协议和 server key 可分别用 `BIOME_RIVALS_NAKAMA_HOST`、`BIOME_RIVALS_NAKAMA_PORT`、`BIOME_RIVALS_NAKAMA_SCHEME`、`BIOME_RIVALS_NAKAMA_SERVER_KEY` 覆盖。

Demo 顶部的联机状态条用于认证、Socket、匹配、权威 Match 加入和重连。点击联机时会把当前己方群系作为 `factionId` 匹配属性提交，并在连接生命周期结束前锁定阵营选择；对手阵营由另一名玩家独立选择，不能由本机预设。收到私有快照后，`DemoOnlineMatchSession` 会切换到权威棋盘视图；双方群系、手牌、能量、生命、阶段、双排槽位与生物状态均来自 `MatchStateStore`。部署、施法、进入战斗、攻击和结束回合不做本地乐观结算，必须等 `acknowledgedCommandId` 对应的事件批次后才更新界面。

权威对局首先进入材质化起手调度层：玩家可点击任意起手牌标记替换，确认后显示双方准备状态；服务端完成“移出旧牌—抽替换牌—旧牌洗回”后才投影新的私有手牌。两人都确认前，手牌区、战场和回合按钮保持锁定。`-previewMulligan` 可在离线 Windows 构建中只预览该界面，用于视觉回归，不改变本地规则状态。

卡牌目标选择由稳定 `effectId` 规则注册，不再写死为敌方单位：当前可区分敌方生物、己方生物与己方建筑/结构，并以贴地高亮只标记合法目标。离线规则镜像与 Nakama 权威规则共同支持 12 个已注册效果，其余 `PENDING` 效果仍会在扣费前拒绝。`可疑的沙子`会掩埋`陶片`并立即获得护甲；HUD 只公开掩埋数量，`CARD_EXCAVATED` 公开陶片、更新手牌/弃牌堆后继续正常抽牌。`沙漠考古学家`使用阻断式石砖选择层：拥有者查看牌库顶三张并只能选取金色标记的掩埋牌，对手只看到保密占位；选择期间手牌、战场、阵营和阶段按钮全部锁定。`-previewArchaeology` 可生成确定性的本地选择界面。`潜影贝`亡语会生成`潜影壳`；`CARD_GENERATED` 在己方手牌中显示真实卡牌，在敌方手牌中只增加未知占位，满手转弃牌时双方都能看到卡牌身份。`岩浆怪`亡语通过 `OBJECT_SUMMONED` 在释放格生成 1/1`小型岩浆怪`，客户端从事件恢复稳定实例与格位，并以贴地脉冲提示出生位置。

普通攻击同样先由规则视图计算合法目标。存在嘲讽单位时，英雄面板和非嘲讽对象会禁用，嘲讽对象使用金色地表材质高亮并在场内铭牌显示“嘲讽”；`CHARGE` 关键词来自权威战场快照，可绕过召唤回合攻击限制。`-previewTaunt` 可生成包含已选攻击者、嘲讽与普通目标的离线视觉回归场景。

`-previewDeathrattle` 会建立潜影贝对铁傀儡的确定性结算场景，让潜影贝死亡并将生成的潜影壳选入手牌，可用于检查亡语反馈、衍生卡卡面和区域计数。

`-previewSummon` 会让岩浆怪攻击铁傀儡并死亡，在原单位格召唤缩小版 Minecraft 岩浆怪模型，用于检查战场召唤回放、格位复用与出生高亮。

多格结构的部署预览由 `DemoDeploymentRules` 从当前规则视图计算完整占格范围。只有连续空闲且未越界的起点保持可用；悬停合法起点会同时点亮全部待占地砖，非法范围使用红色地表反馈，并在本地预检阶段阻止无意义的联机命令。`-previewStructurePlacement`、`-previewStructurePlacementInvalid` 与 `-previewStructureDeployed` 分别用于合法范围、越界范围及完成部署后的视觉回归。战场模型按稳定实例而不是相邻 `cardId` 去重，因此两个相邻同名建筑仍会显示为两个对象，结构则只生成一个居中横跨全部占格的 3D 模型。

自动化双客户端验证可用 `-autoOnline -autoOnlineAction -nakamaDeviceId <独立ID> -onlineProbe <报告路径>`。测试设备覆盖值拥有独立的会话缓存键，不会让同机两个进程误用同一玩家身份。

在 Nakama Docker 服务健康且 Windows Demo 已生成后，可从仓库根目录执行 `scripts/validate-online-demo.ps1`。脚本会以独立设备 ID 隐藏启动两个客户端，校验它们进入同一权威对局、完成起手调度、保留各自群系投影，并只终止本次启动的进程。

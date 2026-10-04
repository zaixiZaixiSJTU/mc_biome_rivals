# AI 玩家操作接口

2026-10-04，AI-086。提供现有全部对局规则操作，不另造规则或通过模拟鼠标驱动 AI。

## 使用边界

`IPlayerOperations` 位于 Networking，`DemoOnlineMatchSession` 是当前权威实现。人类 UI 原有调用与 AI 接口共用同一 session / dispatcher / gateway / 服务端校验。每个 AI 是独立账号与客户端，可在同一 Nakama 对局中担任任一方。当前不是“本地演示假对手变成完整双人离线引擎”，也没有创建外部 HTTP、MCP、LLM 服务或自动启动后台 bot。

接口只提供玩家能执行的操作；发牌、抽牌、天气触发、遗言、伤害/移动/埋藏/出土等效果由服务端按卡牌与回合结算，不开放改生命/改能量/读敌方手牌等作弊入口。注册/登录/匹配仍复用 `IPlayerAccountService`、GameCompositionRoot 和现有 Nakama transport。视觉悬停、镜头、弹窗与选中不是规则动作，不要求 AI 点击这些 UI。

## 观察、决策、提交

1. Unity主线程调用 `Observe()`，取得不可变 `PlayerObservation`（MatchId、Revision、CanIssueCommand、StateJson）。`ReadState()` 每次返回独立 DTO，可交策略或转成 JSON 交外部决策程序。
2. 观察只含当前 viewer 的私有信息。敌方手牌卡名/实例/费用变化与敌方私有选择被隐藏，保留数量；不暴露牌库顺序。原快照无需修改；DTO深拷贝保持 null，避免 JsonUtility 空对象误生成选择/装备。
3. 策略生成 `PlayerActionRequest`：matchId、expectedRevision、type、payload。`Create(observation, type, payload)` 绑定观察版本。
4. `ExecuteAsync` 自动添加协议/规则版本与新 commandId，冻结 payload 数组，沿用待确认门禁，返回 Accepted / Rejected / TransportFailed / TimedOut 与 Code/Message/Revision。
5. 过期观察、跨局动作、未知操作、空payload和未就绪会在发送前拒绝；费用、目标、回合、合成材料、手牌实例等最终合法性由服务端判定。它不是“保证合法动作列表”的接口，不会把任意 AI payload 自动纠正成合法动作。

所有调用在 Unity 主线程；远端耗时推理应在外部执行，返回后重新检查观察版本再提交。没有新增协议版本、服务器RPC或服务器部署要求。它是可信策略接入契约，不是第三方 C# 代码安全沙箱。

## 全部规则操作

| 玩家操作 | type | payload |
| --- | --- | --- |
| 起手调度 | MULLIGAN | cardIndices；空数组表示保留全部 |
| 部署生物/建筑/多格结构 | DEPLOY_CARD | cardId、handCardInstanceId、slotKind=UNIT/BUILDING、slotIndex、paymentMethod=REDSTONE/CRAFTING；指定战吼还带targetType/targetInstanceId |
| 使用法术/材料/装备等手牌 | PLAY_CARD | cardId、handCardInstanceId；按效果带targetType、targetInstanceId或targetInstanceIds |
| 处理战吼/窥牌/考古等选择 | RESOLVE_CHOICE | choiceId、selectedOptionIndex；使用options中的optionIndex，不将数组位置当索引 |
| 进入战斗阶段 | ENTER_COMBAT | 空payload |
| 生物或英雄攻击 | ATTACK | attackerInstanceId；英雄为HERO；targetType=HERO/UNIT/BUILDING、targetInstanceId |
| 结束回合 | END_TURN | 空payload |
| 认输 | CONCEDE | 空payload；基础策略不主动认输 |

同名卡必须用实际 `handCardInstanceId` 区分。合成使用部署支付方式，不新增绕过材料校验的“生成卡牌”命令。单位移动等已注册卡牌效果通过部署/出牌/选择触发；不发明当前协议不存在的独立 MOVE/BURY/EXCAVATE 命令。取消尚未提交的选择只需丢弃自己的候选动作；不得提交假的“撤销已确认命令”。

```csharp
IPlayerOperations player = session;
var observation = player.Observe();
if (!observation.CanIssueCommand) return;
var result = await player.ExecuteAsync(PlayerActionRequest.Create(
    observation, MatchCommandTypes.DeployCard,
    new MatchCommandPayloadDto {
        cardId = selectedCard.cardId,
        handCardInstanceId = selectedCard.handCardInstanceId,
        slotKind = "UNIT", slotIndex = 0,
        paymentMethod = MatchPaymentMethods.Redstone
    }));
// Accepted means server-confirmed, not merely sent. Read a fresh observation next.
```

## 策略替换与最基础人机对战

实现 `IMatchAgentPolicy.Decide(PlayerObservation)`，返回动作或 null（等待）。`MatchAgentRunner.TickAsync()` 每次最多一个动作、等待ACK、同一对局/版本不会盲目重试；终局/断开/等待/Dispose不新发动作。拒绝或超时会停在该版本，换策略/重建runner或取得新版本后才能继续，避免超时不确定时重复扣费。

场景公开 `DemoSceneController.SetAgentPolicy(policy)`，传null停止 AI，不销毁当前玩家session。CLI `-aiPlayer` 安装 `BasicMatchAgentPolicy`；配合 `-autoOnline` 启动匹配。该功能保留在非Development构建，不是诊断夹具。

基础策略使用原 `DemoAuthoritativeMatchView` / `DemoDeploymentRules`，保留调度牌、选择首个可选项、部署无需指定目标的生物/建筑（红石优先，亦可合成）、遵守准备度/嘲讽攻击、结束回合。不使用随机状态修改、不作弊、不自动认输。暂不规划法术、多目标、需指定战吼或复杂组合；**操作接口完整不代表基础策略已能善用每一种卡牌**。注册资料通过 `CardContentLoader` 获取，不在策略复制卡牌数值。

在默认本地服务运行、版本兼容时，可手动打开正常玩家客户端点击“匹配”，另开AI客户端：

```powershell
Start-Process -FilePath 'D:\gitt\mc_biome_rivals\Temp\CurrentUnityCli\ai086-none-player-r1\BiomeRivals.exe' -ArgumentList '-aiPlayer','-autoOnline','-previewPlayerFaction','plains_forest','-nakamaDeviceId','local-ai-opponent' -WindowStyle Hidden
```

玩家与AI必须使用不同deviceId/账号；不要把 `-aiPlayer` 和自动对局诊断控制器放在同一客户端，避免两个驱动争用角色。服务地址用已有 BIOME_RIVALS_NAKAMA_HOST/PORT/SCHEME 环境变量，服务凭据沿用本地配置，不写进本文件。

## 验证

当前18新增用例＋既有687＝Unity引擎705/705。全部8命令的字段转发/待确认、数组冻结、四类本地拒绝、断开/并发、观察深拷贝/敌方隐私、nullable对象、缺失viewer、runner ACK/拒绝后不重试/新版本继续/Dispose、基础部署及对手回合等待通过。

`scripts/validate-ai-player.ps1` 以Development玩家诊断客户端＋独立非Development AI 实跑权威对局，保留报告/截图/日志；不会重启服务或清理原数据，仅结束自己启动的Player。验收中的“玩家”由原命令探针驱动，不冒称人工游玩或每种卡牌效果的在线覆盖。细节见 [AI-086](task-packets/AI-086.md)。

# RULE-033 悬置与回手基础设施任务包

更新时间：2026-09-23
启动基线：`41e75e9 docs: close RULE-032 and scope suspension contract`

本任务只处理 `ed_002` 紫颂果、`ed_003` 末影人、`ed_005` 末影珍珠、`ed_006` 虚空凝视与 `tk_017` 末影龙化身。服务端继续作为规则事实源，Unity 离线 Demo 保持同构。每个子任务独立验证、独立提交；不得顺带实现 ED-001/ED-008 的累计奖励与终局召唤、交易或其他预留效果。保留来源未明的两个本机未追踪文件。

## RULE-033A 规则契约与任务拆分

状态：**已完成**。冻结裁决见 [`../../design/end-suspend-return-spec-v1.md`](../../design/end-suspend-return-spec-v1.md)。本项只提交规范和任务状态文档，不改运行时代码、协议 36、规则集 `prototype-0.61`、效果实现注册表 47、卡牌定义/catalog 41 或五张牌的 `PENDING` 状态。

验证：`scripts/validate.ps1 -WithDockerConfig` 通过，内容校验、TypeScript、服务端 218/218、构建和 Compose 配置均正常。已直接调用 Unity `6000.0.28f1c1`，但中国版编辑器在加载项目之前因缺少 `com.unity.editor.headless` entitlement 退出；因此本项不声称产生新的 Unity 测试结果，最近一次有效基线仍是 RULE-032F3 已复核的 EditMode 262/262。该授权问题须在 RULE-033B 开始运行时修复，不能用旧 XML 伪装新回归。

关键裁决：

- 回手折扣必须绑定精确手牌实例，禁止以 `cardId` 影响所有同名牌；部署/出牌命令也必须精确选择实例。
- 战场对象保存所有者；即时回手和悬置返回进入所有者区域，控制权不改所有权。
- 悬置区正面公开；下个所有者起始阶段在普通抽牌前按稳定顺序逐张返回，满手则公开进弃牌堆。
- 回手/悬置清除整个战场对象状态，不算死亡，不触发亡语或掉落。
- TK-017 只防当前控制者自己的**法术**；ED-002 材料、ED-003 战吼和 ED-006 敌方法术仍可作用。

## RULE-033B 稳定手牌实例与费用修正基础

状态：**实现与验证完成；独立提交待工作区整理**。协议 37 / `prototype-0.62`、服务端/Unity 手牌实例、费用修正和投影已闭环；服务端规则回归 **224/224**、生产 TypeScript 类型检查通过，覆盖终局费用到期/隐私、DB-005 实时折扣叠加截零、所有进入手牌路径分配实例、同名合成选中副本、规则引擎缺失实例 ID 原子拒绝，以及 MatchSnapshot/EventBatch 双方 7 张手牌上限。Docker/Nakama 3.40.0 隔离双端 smoke 已通过：普通对局 revision 0→2→3；Strider FIRE/净火对局 revision 22/24；WITHER/Fortress 重连后状态结算 revision 38/40，验证报告哈希记录于 change-log。Unity CLI EditMode **286/286 通过并干净退出**，覆盖 Nakama DEPLOY/PLAY JSON wire payload、在线会话透传选定手牌/目标实例、实例必填 API、同名牌恢复快照的身份/隐私、离线命令/预览歧义拒绝、失效选中副本锁定、合成预览重绑定，以及实例 ID 格式、折扣到期玩家归属和双方最多 7 张的快照/私有投影边界；报告：`Temp/RULE-033B-hand-instance-boundaries-editmode.xml`。内容校验确认 74 张定义/文本/美术注册一致，`ed_002`、`ed_003`、`ed_005`、`ed_006`、`tk_017` 均仍为 `PENDING`；未新增回手/悬置事件或运行时。测试在同源码临时项目副本运行，未覆盖或替换主项目内容。当前工作区尚有 40 个 tracked 修改及 3 个来源未明的 untracked 项；除 B/RULE-032 混合差异外，新增包含手牌 hover 层级 UI 与 Development Player CLI 输出路径改动，必须排除在 B 提交之外。卡牌定义/名称注册仅格式变化，卡牌文本注册含 RULE-032 `nt_008` 差异；另有 RULE-032 smoke 与通用 CLI 验证改动。完成 B 专项变更分组并单独提交前，不启动 RULE-033C。

目标：把权威手牌由裸卡牌 ID 升级为稳定实例，使相同 `cardId` 的不同副本可具有不同有效费用，并让所有命令、快照和 Unity 交互精确选择副本。

必须完成：

- 为抽牌、生成、起手替换、出土、掉落等所有进入手牌路径分配 `handCardInstanceId`；clone、Schema、私有投影和恢复完整。
- `DEPLOY_CARD`、`PLAY_CARD`、合成产物选择及 Unity 命令工厂使用实例 ID；服务端核验可选 `cardId` 一致性，消除 `indexOf(cardId)` 的歧义。
- 手牌实例支持整数费用修正和 `expiresAtEndOfTurnPlayerId`；统一有效费用函数叠加 DB-005 实时修正并在 0 截断；结束阶段发布到期事件。
- 对手只得到等量空占位；不得泄露手牌实例或修正。Unity 手牌、详情与可支付判断按实例显示，两张同名牌可显示不同费用。
- 五张目标牌保持 `PENDING`，不得新增 `OBJECT_RETURNED` 或悬置运行时。

验证：服务端覆盖同名隔离、错误/过期实例、原子拒绝、支付、合成、到期、快照与投影；Unity EditMode 覆盖 DTO、仓库、选择、命令和离线模型。直接调用指定 Unity 版本运行回归。

## RULE-033C 即时回手纵向切片

状态：等待 RULE-033B 变更分组与独立提交。

只实现对象 `ownerPlayerId`、通用即时回手事务、`ed_002`、`ed_005` 与 `tk_017`：支付前目标/TK-017 校验，回手清理、满手进所有者弃牌堆、精确 `-1/-2`、权威事件、Unity 因果回放、离线同构和内容注册。不得建立悬置区。

## RULE-033D 悬置生命周期基础设施

状态：等待 RULE-033C。

只建立公开 `suspendedCards`、稳定 `suspensionId`、进入/返回事件、所有者下个 `TURN_STARTED` 后且普通抽牌前返回、满手、折扣、终局停止、快照/投影/重连、Unity 仓库与离线同构。`ed_003`、`ed_006` 仍为 `PENDING`，基础设施测试使用规则级夹具，不接卡牌触发。

## RULE-033E1 末影人纵向切片

状态：等待 RULE-033D。

只实现 `ed_003`：可选且随部署提交的“另一个己方生物”目标；成功顺序 `CARD_DEPLOYED → OBJECT_SUSPENDED → OBJECT_STATS_CHANGED(+0/+1)`；目标在部署监听中失效则不悬置、不加血、不退款；战吼可作用 TK-017。完成内容注册、服务端、Unity 回放与离线测试。

## RULE-033E2 虚空凝视纵向切片

状态：等待 RULE-033E1。

只实现 `ed_006`：强制敌方存活生物目标、敌方法术可悬置 TK-017、支付前原子拒绝、锁定后失效不退款、`CARD_PLAYED → OBJECT_SUSPENDED`、完整注册、服务端/Unity/离线测试。

## RULE-033F 集成验收

状态：等待 E2；启动时必须继续拆成三个独立任务，不合并执行。

1. `RULE-033F1`：确定性 Unity Play Mode 演示与视觉审查。必须通过真实离线规则复现即时回手、同名牌精确折扣、悬置、起始阶段返回及 TK-017 三类来源边界；使用世界内高亮、末地粒子、材质化公开除外区托盘，保存 1920×1080 截图及哈希。
2. `RULE-033F2`：Docker/Nakama 双端与重连。验证公开除外区一致、手牌私密性、悬置前/返回前/折扣后重连、事件 payload、费用支付与到期，保存 Match ID、双方 revision 和报告哈希。
3. `RULE-033F3`：证据收口。只复核提交、版本、测试、Unity 图与在线报告，更新日志/路线图/交接并关闭 RULE-033。

## 最小交接

```text
任务编号：RULE-033B 提交收口
只审查、分组并独立提交已验证的 RULE-033B 修改；不得改动三个来源未明的 untracked 项，不得提前实现回手或悬置。
仓库：D:\gitt\mc_biome_rivals
基线：HEAD 为 `ba6e747`；B 的实现与测试已通过，但尚未提交。
先读：`git status --short`、`git diff --stat`、RULE-033B 的 40 个 tracked 修改，以及对应测试与 change-log。卡牌定义/名称 JSON 与 HEAD 语义相同但被重格式化；卡牌文本含 RULE-032 `nt_008` 差异；另有独立 UI hover 与 CLI build-output 改动；逐文件确认，勿把这些自动纳入 B 提交。
开始前记录 git status；保留来源未明的本机文件。
不要自动暂存整个工作区。先区分 B 相关变更、RULE-032 smoke/Unity CLI 通用验证修改、独立 UI hover/CLI build-output 改动和用户文件；仅在提交范围无歧义时形成 B 独立提交，否则向用户报告精确待确认文件清单。之后才启动 RULE-033C。
结束时报告：分组文件、排除的既有变更、测试证据、剩余风险和提交号。
```

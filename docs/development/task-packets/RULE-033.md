# RULE-033 悬置与回手基础设施任务包

更新时间：2026-09-27
启动基线：`c942129 feat: add stable hand card instances`

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

状态：**实现、验证与独立提交完成**（`c942129`）。协议 37 / `prototype-0.62`、服务端/Unity 手牌实例、费用修正和投影已闭环；服务端规则回归 **224/224**、生产 TypeScript 类型检查通过，覆盖终局费用到期/隐私、DB-005 实时折扣叠加截零、所有进入手牌路径分配实例、同名合成选中副本、规则引擎缺失实例 ID 原子拒绝，以及 MatchSnapshot/EventBatch 双方 7 张手牌上限。Docker/Nakama 3.40.0 隔离双端 smoke 已通过：普通对局 revision 0→2→3；Strider FIRE/净火对局 revision 22/24；WITHER/Fortress 重连后状态结算 revision 38/40，验证报告哈希记录于 change-log。Unity CLI EditMode **286/286 通过并干净退出**，覆盖 Nakama DEPLOY/PLAY JSON wire payload、在线会话透传选定手牌/目标实例、实例必填 API、同名牌恢复快照的身份/隐私、离线命令/预览歧义拒绝、失效选中副本锁定、合成预览重绑定，以及实例 ID 格式、折扣到期玩家归属和双方最多 7 张的快照/私有投影边界；报告：`Temp/RULE-033B-hand-instance-boundaries-editmode.xml`。内容校验确认 74 张定义/文本/美术注册一致；B 阶段五张目标牌保持 `PENDING`。当时工作区的 UI hover、Development Player CLI 输出路径、RULE-032 smoke、卡牌文本及来源未明本机文件均排除在 B 提交外。RULE-033C 已在 B 提交后启动。

目标：把权威手牌由裸卡牌 ID 升级为稳定实例，使相同 `cardId` 的不同副本可具有不同有效费用，并让所有命令、快照和 Unity 交互精确选择副本。

必须完成：

- 为抽牌、生成、起手替换、出土、掉落等所有进入手牌路径分配 `handCardInstanceId`；clone、Schema、私有投影和恢复完整。
- `DEPLOY_CARD`、`PLAY_CARD`、合成产物选择及 Unity 命令工厂使用实例 ID；服务端核验可选 `cardId` 一致性，消除 `indexOf(cardId)` 的歧义。
- 手牌实例支持整数费用修正和 `expiresAtEndOfTurnPlayerId`；统一有效费用函数叠加 DB-005 实时修正并在 0 截断；结束阶段发布到期事件。
- 对手只得到等量空占位；不得泄露手牌实例或修正。Unity 手牌、详情与可支付判断按实例显示，两张同名牌可显示不同费用。
- 五张目标牌保持 `PENDING`，不得新增 `OBJECT_RETURNED` 或悬置运行时。

验证：服务端覆盖同名隔离、错误/过期实例、原子拒绝、支付、合成、到期、快照与投影；Unity EditMode 覆盖 DTO、仓库、选择、命令和离线模型。直接调用指定 Unity 版本运行回归。

## RULE-033C 即时回手纵向切片

2026-10-03 QA-049 最新：真实双 Unity Player 的 ED-002/ED-005 在线回手专项已通过，出牌经过实际手牌/详情 UI 和 3D 指针；验证双方事件因果、支付、精确折扣、投影隐私、格位清空、双方重连及到期一次，手牌/详情实际 CardUI 费用一致。全量 Unity 441/441，新 Player 638 项来源匹配，普通对局回归通过。证据见 [QA-049](QA-049.md)。下方 QA-048 所述专项缺口已补齐；共享文件提交边界未收口，C 不宣告完成，不启动 D。

2026-10-03 QA-048 更新：真实 Unity 初始快照暴露 wire null 的引用实例化问题，现已修复且全量 432/432；新 Player 基础动作/重连实测通过。QA-047 的两场 JavaScript 回手仍有效，但不代表 Unity Player 已在线执行回手专项；该补验和共享文件提交边界仍待收口。详见 [QA-048](QA-048.md)，C 不提前视为整体完成。

状态：**即时回手在线门禁已通过；提交分组与证据收口待完成**（B 已于 `c942129` 独立提交）。2026-10-03 当前服务端独立 Compose 验收中，两场真实 Nakama 网络客户端分别通过 ED-002/ED-005 折扣、脱敏、双方重连和到期清零；Unity 全量 423/423，服务端 240/240。详细证据与旧服务版本不兼容的负向结果见 [`QA-047`](QA-047.md)。未新增提交；本轮不启动 033D。

只实现对象 `ownerPlayerId`、通用即时回手事务、`ed_002`、`ed_005` 与 `tk_017`：支付前目标/TK-017 校验，回手清理、满手进所有者弃牌堆、精确 `-1/-2`、权威事件、Unity 因果回放、离线同构和内容注册。不得建立悬置区。2026-09-27 规则测试 **231/231**、TypeScript 与 74 张卡牌内容校验通过；Unity CLI EditMode **297/297 Passed** 且命令干净退出，报告 `Temp/AuraReturnLethalReview/unity-full-editmode-cross-owner-return-final.xml`。Unity 离线 Demo 的 ED-002/ED-005 实际回手及费用到期回归均通过；MatchStateStore 新增双视角跨所有者回放验证，原拥有者收到手牌实例与折扣，对手只收到隐藏手牌占位。致死光环丢失回归验证：回手海龟令相邻岩浆怪生命归零后，服务端与离线 Demo 都完成死亡、弃牌及亡语召唤；既有 Unity 状态回放 `Temp/RULE033C-return-aura-editmode.xml` 覆盖非致死光环重算。Docker 双端 smoke 仍未执行：`docker desktop start --detach` 后 backend 因 `C:\Users\Lenovo\AppData\Local\Docker\run\sailor-ingest.sock` 旧 reparse point 无法访问/重命名而退出；未删除、重命名或重置 Docker 数据，等待用户确认是否只备份该路径后重试。在 Docker 复测和完整在线状态回放前不得关闭 C。

2026-09-28 复核：Unity CLI 再次全量 EditMode **298/298 Passed**，报告 `Temp/AuraReturnLethalReview/unity-interaction-cancel-full.xml`；服务端 **231/231** 与 TypeScript 类型检查通过。Docker Desktop 进程存在，但默认 `docker_engine` 与 `dockerDesktopLinuxEngine` 两个 named pipe 均不存在，也没有本机 Nakama/PostgreSQL 监听；`docker desktop status` 查询挂起。未重启 Docker 或更改内部 socket/容器数据。C 仍不能关闭，直到双端在线 smoke 完成。

2026-09-28 最新复核：在现有工作区重跑 `server-nakama` 的 `npm test`（**231/231**）与 `npm run typecheck` 均通过；Unity Hub CLI 在隔离项目副本全量 EditMode **303/303 Passed**，报告 `Temp/CurrentGoal-final.xml`。其中新增的 UI presenter 回归验证了客户端致死攻击事件时序，但不替代 Docker/Nakama 双端规则 smoke。在线验收条件未改变：Docker CLI 报 `dockerDesktopLinuxEngine` named pipe 不存在，`docker_engine` 与该 pipe 均不可用，本机 7349/7350/5432 无监听。未尝试启动/重置 Docker，RULE-033C 仍等待安全恢复服务后执行双端回手与重连 smoke。

2026-09-28 双端探针预备：新增 `server-nakama/scripts/smoke-end-return.mjs`（`npm run smoke:end-return`），依次创建两场末地阵营双客户端对局，分别收集 ED-002/ED-005 与己方单位；脚本验证公开事件顺序、回手实例 `-1/-2` 与本回合到期、双方重连后公开棋盘收敛、观察者手牌身份脱敏及拥有者折扣恢复。`node --check`、服务端规则 **231/231**、TypeScript 与 Unity CLI EditMode **303/303** 通过（Unity 报告 `Temp/RULE033C-current-goal-unity-editmode-r3.xml`，SHA-256 `BB8BEF42B5B9081D84BE7772D56603C8C441BD62BC03E7ED98D65CC1C57037D8`）。Docker daemon 仍不可用，探针尚未执行；C 不得关闭，也不得启动依赖 C 的 D。

2026-09-28 后续复核：Unity CLI 全量 EditMode **308/308 Passed**（`Temp/GoalContinue/unity-editmode-ui-hover-final.xml`），隔离 Windows Development Player 构建成功，`-PreviewHandHover` 实机截图通过源码清单与 1920×1080 校验。`scripts/validate.ps1 -WithDockerConfig` 再次通过：卡牌内容注册、TypeScript、服务端 **231/231**、构建及 Compose 静态配置全绿。当前只检查 Docker 状态，没有启动/重置 Docker；`desktop-linux` 的 `dockerDesktopLinuxEngine` 与 `default` 的 `docker_engine` pipe 均不存在，因此 `npm run smoke:end-return` 仍未执行，RULE-033C 保持进行中。

2026-09-28 实例边界回归：发现服务端状态校验仅限制每个玩家半场内的对象 ID 唯一，Unity 快照接收则未拒绝重复战场对象 ID。现在服务端与 Unity 均校验对局范围内唯一，并各自新增回归。`scripts/validate.ps1 -WithDockerConfig` **232/232** 服务端通过；Unity CLI 隔离副本 EditMode **309/309 Passed**，报告 `Temp/GoalContinue/unity-global-instance-editmode.xml`。更新后的隔离项目 Windows Development Player 构建成功，`capture-demo-preview.ps1 -PreviewHandHover` 验收图为 1920×1080，SHA-256 `7F531CD3112280ED81590B92DF231F077F0D4DA1C9217515B0D20166272A1713`。Docker 双客户端验收仍待 Engine API 恢复，C 未关闭。

2026-09-28 回手目标阵营回归：ED-002/ED-005 对敌方战场对象都须在支付前原子拒绝。`scripts/validate.ps1 -WithDockerConfig` 全链通过，服务端 **233/233**；Unity CLI `test` 在隔离项目全量 EditMode **309/309 Passed**，报告 `Temp/GoalContinue/unity-return-target-editmode-cli.xml`。Docker 双端在线验收仍待 Engine API 恢复。

2026-09-29 当前主工程复核：只读执行 `scripts/validate-unity.ps1 -UnityPath "D:\Unity 6000.0.28f1c1\Editor\Unity.exe" -UnityCliPath <Unity CLI>`，卡牌/效果/本地化/主题/卡图/世界纹理/Bedrock 来源 Schema 与注册校验均通过（74 定义、62 实现效果、74 名称/文本/卡图、7 主题、40 世界纹理、64 本机实体资源），卡框副本无漂移，Unity 主工程 EditMode **324/324 Passed**（`Temp/RULE-033B-validation-editmode.xml`）。`server-nakama` 当前 `npm test` **234/234 Passed**、`npm run typecheck` 通过。Docker API 仍因 `dockerDesktopLinuxEngine` named pipe 不存在而不可达；未启动 Docker、未改 socket 或容器数据，故此结果不替代 C 的双端 smoke。

2026-09-29 增量复核：通过 Unity CLI `1.0.0-beta.8` 对当前主工程重新执行 `unity test client-unity --mode EditMode --output Temp/GoalContinue/unity-active-worktree-audit.xml --editor-path "D:\Unity 6000.0.28f1c1\Editor\Unity.exe" --timeout 900`，**325/325 Passed**，XML SHA-256 `BB6D191C6491EF91D3426206E65C3A713400717AEC30019A4E87C7AA8467D2B4`；`server-nakama` 同时复跑 `npm test` **234/234 Passed** 和 `npm run typecheck`。`unity status` 未发现交互式 Editor 实例；CLI 通过指定 Editor 可独立启动批处理测试/构建。Docker API 仍报 `dockerDesktopLinuxEngine` named pipe 不存在；未启动/重置服务或改动 Docker 内部路径，在线双端验收仍未完成。

2026-09-28 离线 Player 交互补验：`-previewEndReturnInteraction` 的手牌与 Cast 按钮经 UI GraphicRaycaster 及 PointerDown/Up/Click 派发；战场格经 3D Physics 射线和按下/抬起命中。另明确验证结束回合按钮点位有 UI 命中且会挡住战场。验收核对回手实例、单位格清空、弃牌与费用状态。Unity CLI 隔离项目 EditMode **323/323 Passed**（报告 `Temp/GoalContinue/unity-end-return-ui-raycast-proof-editmode.xml`），Windows Development Player 构建成功，1920×1080 截图通过来源清单/日志/进程退出码/尺寸校验（SHA-256 `5062B5AE92D603C53897EEBFF0AA38BB39834CB1245DE013A3567D04A62391F3`；`Temp/GoalContinue/end-return-ui-raycast-proof-1920.png`）。这只是确定性指针/UI事件的离线场景证据，不关闭 C；Docker/Nakama 双端在线回手、隐私与重连 smoke 仍待 Engine API 恢复。

## RULE-033D 悬置生命周期基础设施

2026-10-03 在线门禁复核更新：先前正常启动/授权移动失败的历史记录见 [`QA-046`](QA-046.md)。随后 Engine 外部恢复，独立服务即时回手双端验收通过，证据见 [`QA-047`](QA-047.md)。本轮先完成 C 的提交边界审查和证据收口，不合并启动 D。

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
历史交接（RULE-033B 已完成；勿按此启动）
任务编号：RULE-033B 提交收口
只审查、分组并独立提交已验证的 RULE-033B 修改；不得改动三个来源未明的 untracked 项，不得提前实现回手或悬置。
仓库：D:\gitt\mc_biome_rivals
基线：HEAD 为 `ba6e747`；B 的实现与测试已通过，但尚未提交。
先读：`git status --short`、`git diff --stat`、RULE-033B 的 40 个 tracked 修改，以及对应测试与 change-log。卡牌定义/名称 JSON 与 HEAD 语义相同但被重格式化；卡牌文本含 RULE-032 `nt_008` 差异；另有独立 UI hover 与 CLI build-output 改动；逐文件确认，勿把这些自动纳入 B 提交。
开始前记录 git status；保留来源未明的本机文件。
不要自动暂存整个工作区。先区分 B 相关变更、RULE-032 smoke/Unity CLI 通用验证修改、独立 UI hover/CLI build-output 改动和用户文件；仅在提交范围无歧义时形成 B 独立提交，否则向用户报告精确待确认文件清单。之后才启动 RULE-033C。
结束时报告：分组文件、排除的既有变更、测试证据、剩余风险和提交号。
```

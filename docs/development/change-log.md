# 改动日志

本文件按时间倒序记录影响视觉表现、资源管线或运行时架构的改动。

## 2026-09-18 RULE-031E1 下界触发器确定性演示审查

- **完整本地链路**：新增 `-previewNetherTriggerLifecycle`，在同一确定性 Play Mode 场景中串联疲劳首次掉血、僵尸猪灵永久成长至 3/3、两座重生锚授予 2 点临时红石、猪灵岩浆优先消耗其中 1 点并造成 1 点伤害、剩余 1 点到期；最终双方英雄均为 29、基础红石保持 9/9。
- **交互与可读性修正**：对手行动时右侧卡牌详情不再错误提示选择部署格，改为明确锁定手牌操作；部署预览在非己方回合完全关闭。底部状态文本启用 10–14 像素自动适配并压缩链路文案，完整信息可在材质化容器内读完。
- **Unity 实机证据**：直接调用 Unity `6000.0.28f1c1_a1337fc966e0`，EditMode 246/246；实际 Play Mode 1920×1080 截图为 `client-unity/Logs/nether-trigger-lifecycle-e1-final-20260918.png`。画面确认原版僵尸猪灵与两座重生锚模型、3/3、29/29、9/9、对手行动锁定提示和无误导部署高亮。E1 未运行或修改 Docker/Nakama。

## 2026-09-18 RULE-031D 重生锚纵向切片

- **权威触发闭环**：`nt_007` 仅在控制者自己的回合、英雄首次非致死实际掉血后触发；英雄掉血标记和单位监听完成后，存活重生锚按建筑格 0→2、实例 ID 稳定顺序逐座发布 `REDSTONE_CHANGED(reason = TEMPORARY_GRANTED)`。护甲完全吸收、对手回合掉血、同回合后续掉血与致死伤害均不授能。
- **资源与双端同构**：每座锚增加 1 点公开临时红石而不改变基础红石或容量，允许总量超过 10；普通支付和猪灵岩浆继续优先使用临时池，未使用部分在结束阶段合法效果后到期。Unity 状态仓库校验来源、存活建筑、己方行动回合、首次掉血标记和逐点增长；离线 Demo 覆盖多锚、优先支付、疲劳触发、对手回合屏蔽与到期。
- **Unity 视觉与资源管线**：新增 `-previewRespawnAnchor` 确定性 Play Mode 场景、权威事件的充能提示和场内脉冲。重生锚使用原版 `respawn_anchor_top`/`respawn_anchor_side1`、黑曜石与萤石构成专用 2.5D 方块模型；Minecraft 纵向动画 PNG 提取统一裁取首帧，修复卡图被渲染为紫色细线的问题。最终画面明确显示 `2/7` 与“临时 +2”。
- **注册与验证**：协议维持 34，规则集 `prototype-0.56`→`prototype-0.57`，卡牌定义/catalog 版本维持 41，效果实现注册表 43→44；`nt_007` 转为 `IMPLEMENTED`，合计 57 个已实现效果、12 个预留效果。服务端 201/201；Unity `6000.0.28f1c1_a1337fc966e0` EditMode 244/244。Unity 实际 Play Mode 的 1920×1080 截图为 `client-unity/Logs/respawn-anchor-preview-20260918.png`。

## 2026-09-18 RULE-031C2 僵尸猪灵岩浆与完整注册

- **结束阶段权威结算**：每个存活 `nt_002` 在控制者结束阶段按单位格、实例 ID 稳定顺序逐只重新读取可用红石；足够时通过统一支付函数优先扣除 1 点临时红石，再发布 `REDSTONE_CHANGED(reason = AUTOMATIC_PAYMENT)`，随后对敌方英雄造成 1 点普通伤害。结算插在海底神殿之后、状态伤害之前；不足的实例跳过且不发空事件，最后 1 点红石只供最先轮到的实例使用。
- **伤害与终局边界**：护甲可以完全吸收岩浆且不占首次掉血窗口；非致死实际掉血继续复用 C1 的首次标记与猪灵成长链。致死伤害在本次支付和伤害后立即停止其余猪灵、状态与回合移交。Unity 状态仓库要求每个猪灵岩浆伤害紧跟同来源的一点自动支付，并校验临时池优先、存活来源、行动方、普通伤害及生命/护甲投影。
- **离线、UI 与内容注册**：离线 Demo 新增基础＋临时总能量的优先支付与到期语义，双方猪灵均能在各自结束阶段结算；联机事件演出显示“岩浆喷射”并高亮来源模型。`nt_002` 的冻结规则文本已同步到共享内容、Unity 和服务端 catalog，效果状态改为 `IMPLEMENTED`，可从手牌正常部署。卡牌详情与手牌规则文字均保持居中并启用自动字号适配，避免完整规则被截断。
- **版本与验证**：协议维持 34，规则集 `prototype-0.55`→`prototype-0.56`；卡牌定义/catalog 版本维持 41，效果实现注册表 42→43，共 56 个 `IMPLEMENTED`、13 个 `PENDING`。服务端 198/198；Unity `6000.0.28f1c1_a1337fc966e0` EditMode 240/240。Unity 实际进入 Play Mode 的专用 `-previewPiglinMagma` 场景显示两只原版僵尸猪灵按序支付、敌方从 30 降至 28、己方能量归零，1920×1080 截图为 `client-unity/Logs/piglin-magma-preview-20260918.png`。

## 2026-09-18 RULE-031C1 僵尸猪灵首次实际掉血成长

- **权威触发**：`nt_002` 存活在场时，其拥有者英雄在任一玩家回合第一次发生非致死实际掉血后，紧跟 `HERO_LIFE_LOSS_MARKED`，按单位格 0→3 为每个存活实例发布 `OBJECT_STATS_CHANGED(reason = PERMANENT_STAT_MODIFIER)`。每次永久增加 1 点攻击、1 点当前生命与1点最大生命；已有伤口比例不被重算，临时攻防修正及到期回合保持不变。护甲完全吸收、同回合后续掉血、首次掉血后的晚入场实例和致死掉血均不触发。
- **Unity 与离线同构**：Unity 状态仓库核验标记窗口、卡牌/实例来源、精确 +1/+1 和临时修正不变，拒绝矛盾的猪灵成长事件；重连快照直接恢复永久后的最终属性。离线 Demo 以统一英雄生命观察入口覆盖法术、疲劳、普通攻击、装备反击、出土陷阱、末影水晶脉冲与亡语，并在回合交接重置双方首次掉血窗口。
- **版本与边界**：协议形状维持 34，规则集 `prototype-0.54`→`prototype-0.55`，内容版本 41/42 不变。`nt_002` 继续 `PENDING` 且服务端与离线 Demo 都拒绝从手牌部署；本切片没有实现结束阶段岩浆支付/伤害，也没有修改 `nt_007`。
- **验证**：服务端 194/194；`scripts/validate-unity.ps1` 使用 Unity `6000.0.28f1c1_a1337fc966e0`，EditMode 236/236。另由 Unity 编辑器实际打开 Demo 场景、进入 Play Mode，并以双方不同群系和地表悬停状态输出 1920×1080 渲染截图 `client-unity/Logs/review-current-20260918.png`，用于确认当前 2.5D 透视、半场拼接与 UI 实机表现。

## 2026-09-15 RULE-031B2b 临时红石优先支付

- **权威费用**：部署、法术、材料与装备的红石费用统一检查基础＋临时总量，付款先用临时池、不足再扣基础池；合成材料支付不碰两池。可复用的 `trySpendRedstone` 在费用不足时返回 false 且两池均不改变，供后续岩浆自动支付使用。费用不足的实际命令保留原拒绝码，手牌、revision、事件游标、实例编号和去重表均不推进。
- **Unity 回放**：卡牌支付事件和 `REDSTONE_CHANGED(reason = AUTOMATIC_PAYMENT)` 核对前后两池变化，拒绝“临时未用却先扣基础”的伪事件；合成事件核对两池完全不变。联机 HUD 与可打出条件沿用 B2a 的总可用量，部分支付后的临时余量可经 Nakama 私有重连快照恢复。
- **版本与边界**：协议形状维持 34、规则集 `prototype-0.53`→`prototype-0.54`，内容版本 41/42 不变；`nt_002`/`nt_007` 仍 `PENDING`，没有实际自动授能或岩浆效果。后续将僵尸猪灵拆为 C1 永久成长、C2 岩浆与完整注册。
- **验证**：`scripts/validate.ps1 -WithDockerConfig` 通过，服务端 191/191；`scripts/validate-unity.ps1` 使用 Unity `6000.0.28f1c1_a1337fc966e0`，EditMode 233/233。覆盖部署、法术、材料、装备跨池、合成不扣池、自动支付函数、双命令原子拒绝、回合末余量到期、重连投影及 Unity 逐事件拒错。Docker 真实在线对局未在 B2b 单独运行。

## 2026-09-15 RULE-031B2a 临时红石状态、回放与到期

- **资源模型**：服务端保留 `redstone` 为基础可用量，新增当前行动方独有的 `temporaryRedstone`（0–3），快照派生并公开 `totalRedstone = redstone + temporaryRedstone`。基础上限仍为 10，故 10/10 + 临时 2 能公开显示总可用 12/10；结束阶段现有合法效果结算后、`TURN_ENDED` 前会发 `REDSTONE_CHANGED(reason = TEMPORARY_EXPIRED)` 并清空剩余临时量。若提前终局，同样先发布到期资源事件，再发布 `MATCH_ENDED`，避免最终快照留下非行动回合临时池。
- **协议与 Unity**：协议 33→34、规则集 `prototype-0.52`→`prototype-0.53`，卡牌部署、出牌、装备及 `TURN_STARTED` 事件携带基础/临时/总量；独立资源事件预留授能、自动支付和到期原因。Schema 校验范围、非行动方临时池及事件类型，跨字段求和由服务端派生与 Unity 入站状态仓库校验。Unity 联机视图使用总量作可用能量，HUD 有临时量时显示第二行“临时 +N”；重连快照保留三池。
- **边界与验证**：本切片不授能、不实现临时池优先支付，`nt_002`/`nt_007` 仍 `PENDING`；内容版本 41/42 不变。`scripts/validate.ps1 -WithDockerConfig` 通过，服务端 183/183；`scripts/validate-unity.ps1` 使用 Unity `6000.0.28f1c1_a1337fc966e0`，EditMode 229/229。测试覆盖超基础上限、非法池、公开及私有重连快照、资源事件回放、矛盾总量拒绝、结束阶段与终局到期及 UI 投影。Docker 真实在线对局尚未在本切片单独验证。

## 2026-09-15 RULE-031B1 首次英雄实际掉血标记

- **权威伤害窗口**：玩家状态新增 `heroLifeLostThisTurn`。每次英雄生命变化由统一事件出口观察：普通/真实伤害、攻击和装备反击、疲劳、建筑脉冲与亡语均使用同一判定；只消耗护甲或致死掉血不发布标记。首次非致死实际生命下降紧跟源事件发布 `HERO_LIFE_LOSS_MARKED`，同一单人回合中每名玩家至多一次；`TURN_ENDED` 对双方一起清零，回合交接后的疲劳可重新标记。
- **协议与重连**：协议 32→33、规则集 `prototype-0.51`→`prototype-0.52`；玩家快照和事件 Schema、Nakama 私有重入快照、Unity 状态仓库与重放同步。Unity 核对源事件、前后生命、护甲、回合、操作者和事件编号，拒绝护甲-only 伪标记。卡牌定义/catalog 版本 41、效果实现注册表版本 42 不变，`nt_002`/`nt_007` 继续 `PENDING`，未增加临时能量字段。
- **验证**：`scripts/validate.ps1 -WithDockerConfig` 通过，服务端 178/178；`scripts/validate-unity.ps1` 使用 Unity `6000.0.28f1c1_a1337fc966e0`，EditMode 226/226。覆盖双玩家、重复伤害、护甲-only、攻击/反击、疲劳、建筑、致死中止、单人回合重置、公开快照、私有重连和 Unity 回放。Docker 在线双客户端暂未在 B1 切片单独运行，留给 RULE-031E 拆分验收。
- **提交边界**：只提交 B1 的权威模型、协议、Unity 投影、测试和任务文档；本机未追踪的 `client-unity/ProjectSettings/PackageManagerSettings.asset`、`scripts/add_units.py` 保持原状。

## 2026-09-15 北极熊＋羊毛集成演示与联机回归

- **演示提交 `c2ed046`**：`-previewPolarBearWool` 确定性预置英雄 15 点生命、原版北极熊部署和一张羊毛施放，场上北极熊为 4/7；世界内三行标签区别显示嘲讽、永久攻 +1、临时血 +1，右侧与手牌复用统一卡面。1920×1080 实机验收图见 [`demo-polar-bear-wool-preview-v1.png`](../design/assets/demo-polar-bear-wool-preview-v1.png)，无文字溢出或布局遮挡。`scripts/validate-unity.ps1` 为 223/223；`scripts/build-demo.ps1 -WithWindowsPlayer` 和 Player 截图捕获通过。
- **恢复测试提交 `36ce93c`**：服务端通过权威 `PLAY_CARD` 施放羊毛后替换旧 session，重入者独享的 opcode 4 快照保留当前/最大生命、临时生命修正、过期回合、revision 与事件游标；Unity 状态仓库和 Demo 视图也验证快照替换后的属性恢复。`scripts/validate.ps1 -WithDockerConfig` 通过，服务端 174/174；`scripts/validate-unity.ps1` 通过，Unity EditMode 224/224。
- **真实联机**：当前构建的 Nakama 日志确认模块加载为协议 32、规则集 `prototype-0.51`。`npm run smoke:integration --workspace server-nakama` 对局 `9b2a5a77-5ed6-4b27-a04f-d14fa799fb9b.biome-rivals` 到 revision 3；`scripts/validate-online-demo.ps1` 两个 Windows Player 在首端强制断线重入后收敛到对局 `c052f850-8cd6-4c1a-acd6-62cea846183a.biome-rivals` 的 FINISHED revision 11、胜者 `bfa161a4-f933-4244-b2f3-fb15a93e1ae8`。报告与日志位于 `artifacts/online-probe-{a,b}.json`、`client-unity/Logs/online-probe-{a,b}.log`。
- **版本边界**：E1/E2 均未升级协议、规则集或卡牌版本。当前效果实现注册表 `contentVersion = 42`；卡牌定义注册表及生成 catalog 的 `contentVersion = 41`。内容校验通过且无生成物漂移，但两个版本字段的独立语义未被验证脚本明确约束，留给 DATA-040 决定是否统一。
- **剩余风险**：真实双端对局没有施放羊毛，不能声称完成“带羊毛状态的双端端到端重连”；该字段恢复目前由服务端及 Unity 针对性测试证明。联机探针使用 `-nographics`，其日志有无 GPU 时的 Shader unsupported 报错，不能用它判断视觉质量；视觉验收来自独立 Player 截图。Unity 批处理有一次测试成功后未在 60 秒内退出，验证脚本仅在确认完成日志后停止了对应进程。

## 2026-09-15 羊毛临时生命纵向切片

- **权威规则**：TK-001「羊毛」现在可在主行动阶段从手牌指定一个存活己方生物。0 费仍校验行动、阶段、手牌和稳定目标；合法施放进入弃牌并计入本回合出牌，产生 `CARD_PLAYED → OBJECT_STATS_CHANGED(reason = TEMPORARY_HEALTH_MODIFIER)`。每张牌令当前/最大生命同时 +1，并累计到施放者本回合结束时统一撤销。
- **叠加与原子性**：两张羊毛叠加 +0/+2；过期时只发一个事件、保留受伤并在需要时夹取当前生命。缺失、敌方、建筑、英雄、离场目标均在支付前拒绝，不推进 revision、命令去重表或任何卡牌/资源状态。
- **Unity 交互**：复用 3D 地表合法目标选择，只标记存活己方单位；权威属性事件和本地规则接受后才脉冲目标。世界标签区别显示“羊毛护持 +N 临时生命”，并从永久生命成长的计算中排除临时值。
- **版本与内容**：协议维持 32；规则集升级至 `prototype-0.51`，效果实现注册表版本升级至 42，卡牌定义/catalog 版本仍为 41。74 张牌中 55 个效果 `IMPLEMENTED`、14 个 `PENDING`，其余待办效果未改变。
- **验证**：`scripts/validate.ps1` 通过，服务端 173/173；Unity `6000.0.28f1c1` EditMode 222/222。确定性演示、Windows Player、Docker/Nakama 双客户端和截图留给 RULE-030E。

## 2026-09-15 临时生命协议基础设施

- **协议形状**：战场快照与 `OBJECT_STATS_CHANGED` 新增 `temporaryHealthModifier`、`temporaryHealthModifierExpiresOnTurn`；协议升级至 32，规则集升级至 `prototype-0.50`，内容版本仍为 41。
- **权威过期**：服务端在结束阶段既有触发与状态结算之后、`TURN_ENDED` 之前统一撤销临时攻击和临时生命。生命上限回退后会夹取当前生命；两类修正同时到期只发出一个 `TEMPORARY_EXPIRED` 事件，不伪造伤害或死亡触发。
- **校验与回放**：服务端拒绝负数、非整数、数值/过期字段不同步及侵蚀基础最大生命的状态；JSON Schema 拒绝零值/过期矛盾。Unity 权威状态仓库可无损恢复和回放新字段，离线 Demo 使用同构过期流程。
- **范围边界**：`tk_001` 羊毛仍为 `PENDING`，效果计数保持 54 个 `IMPLEMENTED`、15 个 `PENDING`；本切片不增加任何可创建临时生命修正的卡牌入口。
- **验证**：服务端规则测试 169/169；Unity `6000.0.28f1c1` EditMode 220/220。Windows Player、Docker 双端和视觉验收留给 RULE-030E。

## 2026-09-15 北极熊低生命部署加成

- **权威规则**：SI-005「北极熊」在部署时读取控制者英雄当前生命；生命不高于 15 时，基础 3/6 在 `CARD_DEPLOYED` 和既有动物入场监听之后永久获得 +1 攻击。生命 16 不触发，部署后的治疗或受伤不会追溯增减。
- **事件与版本**：复用 `OBJECT_STATS_CHANGED(reason = PERMANENT_STAT_MODIFIER)`，携带北极熊来源实例和最终属性；协议结构保持 31，规则集升级为 `prototype-0.49`，内容版本升级为 41。实现注册状态变为 54 个 `IMPLEMENTED`、15 个 `PENDING`。
- **Unity 同构**：离线 Demo 使用相同的 15 点边界与一次性部署检查，并提供范围校验的英雄生命重置入口供确定性测试；权威回放测试确认永久攻击变化不会破坏 `TAUNT` 或误写临时攻击字段。
- **验证**：服务端规则测试 166/166；Unity `6000.0.28f1c1` EditMode 217/217。截图、Windows Player 和 Docker 双端验收按拆分边界留给 RULE-030E。

## 2026-09-14 账户服务与最小大厅外壳

- **认证解耦**：新增 `IPlayerAccountService` / `IPlayerAccountBackend` / `IPlayerAccountSessionProvider`。设备认证、token 恢复、资料读取、改名和退出集中到 Nakama 账户适配器；实时传输只消费已认证 session，不再拥有身份生命周期。
- **可恢复状态机**：账户明确区分未登录、认证中、就绪、更新中、退出中和失败；认证失败或取消后可重试，跨对局复用同一账户会话。
- **大厅顶栏**：顶部材质化容器同时显示游客身份、当前原型卡组、连接状态与匹配入口；左侧栏从“群系”明确为“卡组”。实机布局已收窄到标题栏和回合栏之间，不再发生容器重叠。
- **配置边界**：连接资源缺失时直接报错，移除编译进代码的 server-key 后备值；开发配置及环境变量覆盖路径保持可用。
- **自动验证**：账户服务测试覆盖成功复用、失败重试、取消重试、改名、退出和输入校验；双客户端报告额外核对账户 Ready、账户 ID 与权威 viewer 一致及显示名非空。
- **验收结果**：Unity `6000.0.28f1c1` EditMode 214/214；Windows Player 构建通过；Docker/Nakama 双客户端对局 `684247e5-23b4-47c0-ad78-1d597c2e17f1.biome-rivals` 在完成强制断线恢复与动作链后收敛到 revision 16。视觉验收见 [`account-lobby-shell-preview-v1.png`](../design/assets/account-lobby-shell-preview-v1.png)。

## 2026-09-14 断线重连与权威状态恢复

- **旧会话替换**：Nakama 在双人房已满时允许同一 `userId` 的新 session 重入，并在加入阶段原子移除该玩家的旧 presence，消除 socket 已重连但服务端尚未来得及清理旧会话时的“match is full”竞态。
- **私有恢复快照**：重入者收到只投递给新 session 的 opcode `4` 快照；测试覆盖 revision/event 游标、等待选择及其私有选项、隐藏对手手牌，并断言权威去重表不会投影到客户端。
- **客户端故障边界**：快照和事件批次同时校验协议与规则集；不兼容后 Gateway 锁定为 `Failed`，底层 socket 的后续 `Ready` 不能误解锁交互。命令自身也同时校验协议与规则集。连接进入 `Reconnecting` 时立即结束所有结果未知的 pending 命令，避免等待超时或重复操作。
- **真实断线探针**：新增 `-autoReconnectProbe` 自动化诊断入口。Windows 双客户端验证会强制关闭第一端 socket，确认 `Reconnecting → Ready`、原 Match ID 和新快照恢复后，再继续部署、阶段切换、结束回合、攻击与投降。本次对局 `08ede1b0-4b1c-49b5-a978-474c270b0b3b.biome-rivals` 最终双方收敛到 revision 18、相同胜者，首端报告 `reconnectRecovered=true`。
- **验证**：服务端 163/163；Unity `6000.0.28f1c1` EditMode 208/208；Windows Player 构建及真实 Docker/Nakama 双客户端探针通过。

## 2026-09-14 Unity 双客户端权威对局探针

- **补齐投降边界**：为 `MatchCommandFactory` 和 `DemoOnlineMatchSession` 增加 `CONCEDE` 命令入口，Unity 不再只能被动接收投降事件。
- **真实动作链**：`-autoOnlineAction` 从仅发送 `ENTER_COMBAT` / `END_TURN` 升级为完整最小对局：双方确认起手，按当前手牌和能量部署安全生物，轮转阶段并结束回合，召唤疲劳结束后攻击敌方英雄，最后由攻击方投降。
- **交叉验证**：两个报告必须确认相同 Match ID、不同设备玩家、镜像阵营、相同最终 revision/胜者、FINISHED 状态、生命变化和双方可见的场上单位；部署、攻击、结束回合、投降四类动作缺一即失败。
- **实机结果**：本地 Docker Desktop 28.5.1、Nakama 3.40.0 与两个当前 Windows Player 完成对局，双方收敛到 revision 13；Unity 编译及 203/203 EditMode 测试通过，客户端探针日志无异常。

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

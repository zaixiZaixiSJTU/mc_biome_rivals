# RULE-035 通用同时败北平局结算

更新时间：2026-10-03

## 规则来源与边界

- GDD v0.5 §6.5 与 §11：双方英雄在同一次胜负检查中均为 0 生命时，本局平局。
- `docs/design/desert-temple-buried-chain-spec-v1.md` §14 是明确的卡牌特例：DB-007 炸药机关先结算敌方 3 点伤害，再结算己方 1 点真实伤害；双方归零时由出土牌拥有者获胜。不得把它改成通用平局。
- 只统一通用规则终局的状态、协议和视图表达；不更改其它卡牌数值、伤害顺序、触发时机或 `PENDING/IMPLEMENTED` 状态。

## 子任务与状态

2026-10-03 最新：当前工作区 C 功能验收补齐。QA-050 验证实际网络消息与 Unity wire 回放；[QA-051](QA-051.md) 新构建 644 项来源匹配的双 Unity Player，实际在线验证双方金色“平局”、终局 UI/3D/会话输入拒绝、双方强制重连后恢复，同 Match/revision 3，图形截图已查看、退出 0/0。DB-007 当前 Player 独立双杀仍为出土方胜利。服务端 241/241、Unity 449/449。fixture 服务已停止、数据保留，原服务健康。独立提交树仍待 REL-043，不关闭整体目标。下文旧计数和 Docker 不可达均为历史记录。

### RULE-035A 服务端终局语义与 Schema

**已完成。** 同一次终局检查双方都归零时，服务端保存 FINISHED、空 `winnerPlayerId` 并发出 `SIMULTANEOUS_DEFEAT`；状态不变量允许空赢家的充分必要状态为双英雄均已败北。事件 Schema 保证该 reason 与空赢家匹配，快照 Schema 保证双方生命均为 0。DB-007 出土流程仍按其专用规则结算。

版本：协议 39、规则集 `prototype-0.64`。`server-nakama` 类型检查、测试与构建通过；服务端回归 **236/236**。

### RULE-035B Unity 状态与结果映射

**已完成。** Unity 快照恢复与事件回放接受且验证平局形状，权威结果视图不把任一玩家标记为胜者；通用离线双败投影为平局。离线 DB-007 另外显式保留卡牌规则指定的出土方赢家，不能仅由双方生命推断输赢。

验证：Unity CLI `1.0.0-beta.8` / Editor `6000.0.28f1c1`，从当前源码制作的隔离项目运行全量 EditMode **342/342 Passed**；最近一次全量报告：`Temp/GoalContinue/CurrentAudit/editmode-current.xml`（DB-007 专项前一轮报告：`Temp/GoalContinue/SimultaneousDraw/editmode-db007-owner-wins-final.xml`）。其中 Unity Presenter→Canvas 文本/颜色集成测试真实消费权威 `MATCH_ENDED` 空赢家并断言金色“平局”；DB-007 双杀 EditMode 确认双方归零时玩家（出土方）仍是赢家。`server-nakama` 最近复核规则测试 **236/236 Passed**、TypeScript 类型检查通过。同一隔离工程构建 Windows Development Player 成功；主图形 Editor 未关闭或重启。实际 Player 通过 `-PreviewTntTrapOwnerWins` 执行炸药机关双杀并在 1280×720、1920×1080 截图中显示胜利；哈希见 `change-log.md` 的 UI-050。通用双方同时败北映射仍由 Unity EditMode 覆盖。

### RULE-035C Docker/Nakama 双端回放与重连

2026-10-03 分段结果：双 JS 真实网络 + Unity 正式网关/仓库实际 wire 回放（QA-050）及双真实 Unity Player 的 UI/终局输入/恢复段（QA-051）均通过，四项功能完成定义满足。图形 Match 0252fec4-4e26-4aa1-bb8c-6360fb6c6719.biome-rivals，双方终局/重连 revision 3，空赢家、生命均 0、一次 SIMULTANEOUS_DEFEAT；当前 Player 的 DB-007 特例回归也通过。测试轨迹明确来自限定夹具，不声称通用双败已在当前卡池合法可达。此结论仅限当前工作区，不替代依赖明确的独立提交/发行树验收。

#### RULE-035C1 测试夹具与探针（实现完成，在线验收未执行）

- `BIOME_RIVALS_ENABLE_TEST_FIXTURES` 是显式环境门，Compose 默认 `false`；只有开启后 match handler 才接收专用 opcode `255`。
- 夹具复制权威 MatchState，在副本注入双方英雄归零条件，再由 `BiomeRivalsRules.applyCommand` 执行普通 `PLAY_CARD`，最终事件经过与真实命令相同的客户端事件批次投影并广播给双方。夹具校验 FINISHED、空赢家和 `SIMULTANEOUS_DEFEAT`，失败不提交副本状态。
- 双端探针：`server-nakama` 下运行 `npm run smoke:simultaneous-draw`；包括正常开局/调度确认、两端事件一致性、共同终局 revision 和重连快照。Opcode `5` 仅回报测试夹具触发结果。
- 离线验收：默认关闭/开启门控与广播规则测试包含在服务端 **238/238** 内；TypeScript 类型检查、生产构建和 Compose 静态配置通过。Unity 隔离工程 EditMode **349/349 Passed**（`Temp/GoalContinue/CurrentAudit/editmode-continuation-final.xml`）。
- 尚缺：在实际 Nakama 中运行双客户端脚本并留存 Match ID、双方终局 revision、公开事件顺序、重连快照及报告哈希。Docker Engine 不可用，故本阶段不能关闭 C。

**待 Docker Engine 恢复后执行真实双端在线验收。** 已确认当前规则集中没有合法的通用双败触发链；DB-007 双杀有出土方获胜特例，不能用来冒充通用平局。仅测试环境可启用的确定性夹具与双端探针已在 RULE-035C1 完成，不新增玩家可用卡牌，也不改变生产规则。随后使用两个真实客户端验证：

**2026-10-02 可达性盘点：** 当前卡池没有合法的通用双败触发链。`ATTACK` 指向 `HERO` 时，服务端只对防守英雄结算伤害，`ATTACK_RESOLVED.damageToAttacker` 固定为 `0`；其他单次效果的自伤只作用于其控制者，疲劳也只作用于当前抽牌者。唯一同一效果同时命中双方英雄的是 DB-007 炸药机关，其冻结特例要求出土方获胜。现有 `generic lethal resolution emits a draw...` 单测在执行 `cd_006` 前直接把双方生命设为 `0`，这不是合法可达的对局状态，也不作为本项网络验收依据。因此 C 必须通过测试环境专用 fixture/hook 构造权威 `SIMULTANEOUS_DEFEAT` 事件并走双方真实广播、快照/重连；不能把该单测改成 Player 截图来替代。

1. 权威匹配以一个共同 revision 进入 FINISHED，`winnerPlayerId` 为空且终局 reason 为 `SIMULTANEOUS_DEFEAT`。
2. 双方玩家都在 UI 看到“平局”，且均不显示胜者；任一终局输入均被锁定。
3. 两端收到相同公开终局状态；至少一端重连后从权威快照恢复平局与最终英雄生命。
4. 使用独立回归确认 DB-007 双归零仍由出土方获胜，不因通用平局规则改变。

记录 Match ID、双方 revision、客户端退出码/日志、报告哈希及 Unity Player 版本。服务端单测、Schema 校验和 Compose 静态检查不替代本子任务。

## 当前状态与入口

Docker 已外部恢复，当前 RULE-035C 在线/Player 功能门禁通过，详见 QA-050/051。专用 fixture 容器停止、独立数据 volume 保留，原两套服务保持健康。已收到仅可恢复备份/移动 socket 的授权，但健康状态下没有操作该条目。待办为 REL-043 的独立提交树与发布审查，不再将旧 Docker 故障列为当前阻塞。历史 C1 未执行记录保留，仅用于追溯。

最小入口：本文件、`docs/development/change-log.md` 中 RULE-035、`docs/development/task-roadmap.md` 中 RULE-035、GDD §6.5/§11、DB-007 冻结规范 §14、`server-nakama/src/rules/match-engine.ts`、`client-unity/Assets/Game/Core/Runtime/MatchState.cs`。

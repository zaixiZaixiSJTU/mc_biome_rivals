# RULE-031 下界自伤与临时能量任务包

更新时间：2026-09-18
起始基线：`a45dd8f docs: close RULE-030 evidence and scope next contract`

只处理 `nt_002` 僵尸猪灵和 `nt_007` 重生锚；两张牌现均已完整实现。服务端是规则事实源，Unity 离线 Demo 保持同构。每项单独验证、单独提交；不得顺带实现 WITHER、炽足兽、下界要塞、悬置或回手。保留来源未明的两个本机未追踪文件。

## RULE-031A 规则契约冻结

状态：**已完成**。冻结裁决见 [`../../design/nether-self-damage-energy-spec-v1.md`](../../design/nether-self-damage-energy-spec-v1.md)；本项仅提交契约与任务状态文档，未改运行时代码、注册状态或版本。

目标：只消除实现前的规则歧义，不改运行时代码、注册状态或版本。

需定案：英雄“受到伤害”是否以最终实际掉血为准及护甲边界；同一回合内首次触发的作用域、重置点和多实例顺序；僵尸猪灵 +1/+1 的永久性及当前生命处理；其岩浆结束阶段支付与伤害顺序；重生锚仅己方回合触发的判定、临时红石能量上限/支付/过期与多锚顺序；非法状态及终局中止边界。

入口：`Minecraft_Biome_Rivals_GDD_v0.5.md` §3/4/6/16、`docs/design/Minecraft_Biome_Rivals_Prototype_Cards_v0.1.md` 中 NT-002/NT-007、`shared-schema/card-data/implemented-effect-registry.v1.json`、`server-nakama/src/rules/match-engine.ts` 的英雄伤害/结束阶段流程、`client-unity/Assets/Game/Demo/Runtime/DemoLocalMatch.cs`。

产物：`docs/design/nether-self-damage-energy-spec-v1.md`。完成定义：至少包含触发时点、首次标记、己方回合边界、资源临时性、支付原子性、同时触发顺序、受伤与护甲示例；提交只含契约/任务状态文档。

## RULE-031B1 首次英雄生命损失标记

状态：**已完成**。已建立“当前单人回合首次实际掉血”的权威标记：普通/真实伤害、护甲-only、攻击与反击、疲劳、建筑和亡语均通过同一英雄生命变化入口；快照、事件、服务端/Unity 仓库可回放，并在每次 `TURN_ENDED` 对双方一起重置。两张牌继续 `PENDING`，没有临时能量字段。协议升至 33、规则集升至 `prototype-0.52`，内容版本不变。完成证据见 `docs/development/change-log.md`；独立提交。

## RULE-031B2a 临时红石状态与回放形状

状态：**已完成**。基础/临时/总能量的权威模型已接入协议、Schema、Unity 状态投影和显示；`REDSTONE_CHANGED` 可逐事件回放，旧卡牌支付事件携带三池最终值。临时量只属于当前行动方，结束阶段合法效果完成后、`TURN_ENDED` 前发布到期事件并清零。Schema 校验字段范围与行动方，动态求和由服务端派生、Unity 入站语义校验。两张牌仍 `PENDING`，尚无实际授能和临时池优先支付。测试证据见 `change-log.md`；独立提交。

## RULE-031B2b 临时池优先支付

状态：**已完成**。部署、法术、材料与装备费用均调用同一临时优先支付函数，自动效果可复用该函数；费用不足返回原有拒绝码且不动两池、手牌、revision 或实例编号。合成材料不消耗能量；支付事件和 Unity 回放核验临时优先，B2a 的到期事件仍清除未用余量。重连快照保留部分支付后的临时池。两张牌仍 `PENDING`，没有实际自动授能或岩浆效果；验证见 `change-log.md`，独立提交。

## RULE-031C1 僵尸猪灵首次受伤永久成长

状态：**已完成**。`nt_002` 在己方英雄于任一玩家回合首次非致死实际掉血后永久 +1/+1；沿用 B1 标记，存活实例按单位格稳定顺序响应，晚入场不追溯。服务端权威事件、Unity 回放及离线 Demo 同构均有测试；卡牌仍 `PENDING`，服务端与离线 Demo 显式拒绝手牌部署。协议维持 34、规则集升至 `prototype-0.55`，内容版本不变；验证与 Unity 实机渲染证据见 `change-log.md`。

## RULE-031C2 僵尸猪灵岩浆与完整注册

状态：**已完成**。`nt_002` 的岩浆结束效果在海底神殿后、状态伤害前结算：存活实例按单位格顺序逐只重新读取余能，优先消耗临时池 1 点后对敌方英雄造成 1 点普通伤害，护甲吸收不标记、致死立即中止。服务端、Unity 回放、离线 Demo 与专用 Play Mode 预览均已验证；成长和岩浆两段完整后已将本牌改为 `IMPLEMENTED` 并开放手牌部署。协议维持 34、规则集升至 `prototype-0.56`，效果实现注册表升至 43；独立提交。

## RULE-031D 重生锚纵向切片

状态：**已完成**。`nt_007` 只在控制者己方回合首次非致死实际掉血后触发，存活实例按建筑格稳定顺序各授予 1 点临时红石；护甲-only、对手回合、重复掉血和致死伤害不授能。满基础池可临时超上限，统一支付优先消耗临时池，余量结束阶段到期。服务端事件、快照、Unity 回放、离线 Demo、专用 2.5D 模型与 Play Mode 预览均已验证；本牌转为 `IMPLEMENTED`。协议维持 34、规则集升至 `prototype-0.57`，效果注册表升至 44；独立提交。

## RULE-031E 集成验收

状态：**全部完成**。E1/E2/E3 均已独立验收和提交；RULE-031 关闭。

- **RULE-031E1 确定性演示审查**：**已完成**。同一 Play Mode 场景已串联猪灵成长、两锚授能、临时优先支付、岩浆伤害与余量到期；修复对手回合提示和状态文本适配。Unity 246/246，实机证据为 `client-unity/Logs/nether-trigger-lifecycle-e1-final-20260918.png`；未改 Nakama/Docker。
- **RULE-031E2 Docker 双端与重连**：**已完成**。专项双 socket 对局通过正常牌库与命令触发两锚、猪灵成长、临时授能、重连恢复、自动支付和余量到期；触发/恢复 revision 26，最终 revision 28。另由两个真实 Unity Windows Player 完成通用断线重连动作链并收敛到 FINISHED revision 12。证据见 `change-log.md` 与 `artifacts/nether-trigger-online-probe.json`；未改视觉或规则版本。
- **RULE-031E3 证据收口**：**已完成**。提交链、版本、测试、Unity 截图与两组在线产物均已交叉复核；最终 Unity 246/246。准确证据见 `change-log.md`，RULE-031 正式关闭。

## 最终证据索引

| 层级 | 证据 |
|---|---|
| 提交链 | `399c7a9 → e61d1b7 → f9f622e → 3d1a70d → 2f79a7d → b758f31 → 2a006ef → 72032e6 → 6825ee5` |
| 版本 | 协议 34；规则集 `prototype-0.57`；效果注册表 44；卡牌定义/catalog 41 |
| 自动测试 | 服务端 201/201；Unity EditMode 246/246 |
| 本地视觉 | `client-unity/Logs/nether-trigger-lifecycle-e1-final-20260918.png` |
| 专项在线 | `7b1263ac-b44c-4079-96e4-bed08c708e83.biome-rivals`，revision 26 恢复、28 结算 |
| Unity 双端 | `d1150229-86b1-410d-8b73-01377f1fe08a.biome-rivals`，FINISHED revision 12 |

## 最小交接

```text
任务编号：RULE-032A
只冻结 NT-004/NT-005/NT-008 的规则契约；不得修改运行时代码、注册状态或版本。
仓库：D:\gitt\mc_biome_rivals
基线提交：<RULE-031E3 提交完成后填写>
先读：docs/development/task-packets/RULE-032.md 中 RULE-032A。
开始前记录 git status；保留无关本机文件。
结束时报文档、歧义裁决、验证、风险和提交号。
```

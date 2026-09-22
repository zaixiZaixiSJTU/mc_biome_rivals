# RULE-032 下界状态与召唤任务包

更新时间：2026-09-22
起始基线：`634a6d6 docs: freeze nether status and summon rules`

本任务只处理 `nt_004` 炽足兽、`nt_005` 凋灵骷髅与 `nt_008` 下界要塞。服务端继续作为规则事实源，Unity 离线 Demo 保持同构。每个子任务独立验证、独立提交；不得顺带实现悬置/回手、末地奖励、交易或其他预留效果。保留来源未明的两个本机未追踪文件。

## RULE-032A 规则契约冻结

状态：**已完成**。冻结裁决见 [`../../design/nether-status-summon-spec-v1.md`](../../design/nether-status-summon-spec-v1.md)；本项只提交契约与任务状态文档，未改运行时代码、协议、注册状态或版本。

目标：只消除三张牌的实现歧义，不改运行时代码、协议、注册状态或版本。

必须定案：

- 炽足兽的“己方角色”是否包含英雄、单位和建筑；合法目标是否必须已经着火；没有目标时能否部署；移除 FIRE、治疗、死亡检查和其他状态反应的事件顺序。
- 凋灵骷髅在主动攻击与反击中的触发范围；只对存活生物施加还是允许死亡对象；WITHER 的伤害类型、数值、持续时间、结算时点、刷新/叠加、来源归属、击杀掉落与终局中止。
- 下界要塞多实例按建筑格/实例 ID 的稳定顺序；每座要塞重新检查能量和空单位格；临时红石优先支付、费用不足与满场均不扣费；令牌进入哪个单位格、召唤事件、单位格被前一实例占用后的重算及致死中止。

入口：

- `docs/design/Minecraft_Biome_Rivals_Prototype_Cards_v0.1.md`
- `Minecraft_Biome_Rivals_GDD_v0.5.md` 的状态、攻击、结束阶段、结构与 2.5D 章节
- `docs/design/nether-self-damage-energy-spec-v1.md`
- `server-nakama/src/rules/match-engine.ts`
- `client-unity/Assets/Game/Demo/Runtime/DemoLocalMatch.cs`
- `shared-schema/card-data/implemented-effect-registry.v1.json`

产物：`docs/design/nether-status-summon-spec-v1.md`。完成定义：规范覆盖目标集合、可选/强制选择、事件顺序、WITHER 完整生命周期、支付原子性、多实例稳定顺序、满场/费用不足/致死示例；提交只含规范和任务状态文档。

## RULE-032B 炽足兽纵向切片

状态：**已完成**。只实现 `nt_004`：服务端在支付前区分己方存活着火生物、敌方对象与建筑/结构；有合法目标时强制选择，无合法目标时允许部署。权威事件固定为 `CARD_DEPLOYED → OBJECT_STATUS_REMOVED(EFFECT_REMOVED) → OBJECT_STATS_CHANGED(HEAL)`，满血仍发布治疗结果。

Unity 状态仓库核验炽足兽来源、同控制者、FIRE 移除与紧邻治疗事件；离线 Demo 同构执行。场景控制只在确有着火友军时进入世界内目标选择，复用 3D 模型/地表高亮；无目标时开放部署格并明确提示战吼不触发。`nt_004` 已注册为 `IMPLEMENTED`。

版本与验证：协议 35、规则集 `prototype-0.58`、效果实现注册表 45、卡牌定义/catalog 41；58 个已实现效果、11 个预留效果。`scripts/validate.ps1` 通过，服务端 204/204；直接调用 Unity `6000.0.28f1c1`，EditMode 250/250。未引入 WITHER 或下界要塞运行时逻辑。

## RULE-032C WITHER 状态基础设施

状态：**已完成**（实现提交 `b09d3aa`）。已建立契约要求的 WITHER 枚举、Schema、快照/重连、非叠加刷新、完整来源、稳定结束阶段顺序、真实伤害、致死击杀/掉落和 Unity 因果回放；离线 Demo 同构结算。协议 36、规则集 `prototype-0.59`，效果实现注册表 45、卡牌定义/catalog 41；`nt_005` 保持 `PENDING`，白板部署不会创建 WITHER。

验证：`scripts/validate.ps1` 通过，服务端 209/209；直接调用 Unity `6000.0.28f1c1`，EditMode 256/256，结果为 `client-unity/Logs/rule032c-editmode-results.xml`。两个本机未追踪文件保持隔离，未实现 `nt_005` 触发或 `nt_008`。

## RULE-032D 凋灵骷髅纵向切片

状态：**已完成**（实现提交 `57fea39`）。`nt_005` 的主动攻击与反击会在正数普通伤害后、统一死亡前对存活生物施加 WITHER；死亡来源仍完成已成立触发，死亡目标不创建状态。1→2 刷新替换来源，2→2 保留来源和数组位置。Unity 权威回放验证攻击因果，离线 Demo 同构执行，内容已转为 `IMPLEMENTED`。

版本与验证：协议 36、规则集 `prototype-0.60`、效果实现注册表 46、卡牌定义/catalog 41；59 个已实现效果、10 个预留效果。`scripts/validate.ps1` 通过，服务端 214/214；直接调用 Unity `6000.0.28f1c1`，EditMode 258/258，结果为 `client-unity/Logs/rule032d-full-editmode-results.xml`。未实现 `nt_008`。

## RULE-032E 下界要塞纵向切片

状态：**已完成**（实现提交 `92c7fbc`）。`nt_008` 在猪灵岩浆之后、对象状态之前逐实例重检空格与能量，临时红石优先原子支付 1 点，并在最左空单位格召唤 3/3 `tk_015`。满场、能量不足和猪灵抢走最后能量均不扣费、不分配实例；令牌不继承 WITHER。Unity 因果回放、快照恢复、离线 Demo 和联机事件提示已同构。

版本与验证：协议 36、规则集 `prototype-0.61`、效果实现注册表 47、卡牌定义/catalog 41；60 个已实现效果、9 个预留效果。`scripts/validate.ps1` 通过，服务端 218/218；直接调用 Unity `6000.0.28f1c1`，EditMode 261/261，结果为 `client-unity/Logs/rule032e-final-editmode-results.xml`。

## RULE-032F1 确定性演示与 Unity 视觉审查

状态：**下一唯一任务**。只构建 `nt_004`、`nt_005`、`nt_008` 的本地确定性 Play Mode 演示，审查原版模型、体素战场、地表交互、状态/支付/召唤反馈与 UI 可读性；必须由 Unity 实际运行并输出 1920×1080 截图，不运行 Docker、不修改权威规则。

## RULE-032F2 Docker 双端与重连

状态：**等待 F1**。只验证三张牌的权威事件、双方投影、WITHER 恢复、要塞支付/召唤和中途重连；记录 Match ID、revision 与探针产物，不改视觉。

## RULE-032F3 证据收口

状态：**等待 F2**。只复核提交、版本、测试、Unity 截图和在线产物，更新日志、路线图与交接并关闭 RULE-032；不得新增玩法。

## 最小交接

```text
任务编号：RULE-032F1
只完成三张下界牌的确定性本地 Play Mode 演示与 Unity 视觉审查；不得运行 Docker 或修改权威规则。
仓库：D:\gitt\mc_biome_rivals
基线提交：`92c7fbc feat: implement nether fortress end phase summon`
先读：docs/design/nether-status-summon-spec-v1.md、docs/development/task-packets/RULE-032.md 的 RULE-032F1，以及最近 RULE-032B—E change-log。
开始前记录 git status；保留无关本机文件。
新增一个可重复的本地预览入口，在同一完整场景中明确展示炽足兽净火、凋灵骷髅施加/结算凋零、下界要塞支付并召唤 TK-015；复用原版模型、世界内反馈和统一像素 UI。直接调用 Unity 进入 Play Mode，输出并人工审查 1920×1080 截图。
结束时报告预览参数、截图路径/哈希、Unity 测试、视觉缺陷、剩余风险和提交号。
```

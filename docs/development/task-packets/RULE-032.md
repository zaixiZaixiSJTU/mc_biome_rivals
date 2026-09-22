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

状态：**已完成**（实现提交 `1d6d268`）。`-previewNetherStatusSummon` 通过真实离线规则在同一 Play Mode 场景复现炽足兽净火治疗、凋灵骷髅施加并结算 WITHER、下界要塞支付 1 点并在最左空格召唤 `tk_015`。WITHER 具有紫色贴地高亮和模型附着体素反馈；三格要塞使用原版下界砖、磨制黑石砖与岩浆纹理构成独立世界模型；New Input System 原生后端已启用。

Unity `6000.0.28f1c1` EditMode 262/262 通过。直接调用 Unity Play Mode 输出 1920×1080 截图 [`../../design/assets/demo-nether-status-summon-preview-v1.png`](../../design/assets/demo-nether-status-summon-preview-v1.png)，SHA-256 `A6DEF84189D046603B31B40F201D677A6E59BC8FD15FFBF3EDB33523FDCA1861`；视觉审查确认透视贴地、原版实体模型、要塞三格占位、状态色和统一像素 UI。未运行 Docker，协议 36、规则集 `prototype-0.61`、效果注册表 47、卡牌定义/catalog 41 均不变。

## RULE-032F2 Docker 双端与重连

状态：**已完成**。新增 `smoke:nether-status-summon` 与 `scripts/validate-nether-status-summon-online.ps1`，使用正常牌库、起手替换、逐回合红石增长和真实命令完成两个权威对局，不注入测试状态；随机起手不足时有界重匹配。

炽足兽对局 `4df72230-5bec-4910-9234-3b4a1480777b.biome-rivals` 在 revision 22 由烈焰人对存活友方目标施加 FIRE，在 revision 24 按相邻事件完成炽足兽部署、移除和治疗。WITHER/要塞对局 `93fe5fc6-0773-4330-8cbd-ea0fc5c51243.biome-rivals` 在 revision 35 施加剩余 2 次且来源完整的 WITHER；revision 36 要塞原子支付并在最左格召唤 `object-4` / `tk_015`，双方替换 socket 后均恢复同一 revision、7/0/7 红石、三格要塞、`nextInstanceId = 5` 和完整 WITHER；revision 38 继续结算 1 点真实伤害并 Tick 至 1 次。双方对所有命令的公开事件顺序一致，关键事件完整 payload 相等。

报告为 `artifacts/nether-status-summon-online-probe.json`，SHA-256 `FD2ADA0B7756F3BB24DDE052AD1486AE3C51B3AE0913F13B050B4AB9A540D005`。Docker Engine 29.8.0、Nakama 3.40.0 与 PostgreSQL 16.8 健康；服务端 218/218，全仓构建与 Compose 配置通过；直接调用 Unity `6000.0.28f1c1`，EditMode 262/262。未修改视觉、权威规则、协议 36、规则集 `prototype-0.61` 或内容版本。

## RULE-032F3 证据收口

状态：**已完成**。已复核 A—F2 的祖先提交链：契约 `634a6d6`、炽足兽 `1bfb63a`、WITHER 基础设施 `b09d3aa`、凋灵骷髅 `57fea39`、下界要塞 `92c7fbc`、Unity 演示 `1d6d268`、Docker 双端重连 `819eb60`；全部均为当前 HEAD 的祖先。F3 只更新证据、路线图和交接，没有新增玩法、视觉、协议或内容改动。

最终证据矩阵：

- **版本与内容**：协议 36、规则集 `prototype-0.61`、效果实现注册表 47、卡牌定义与服务端 catalog 41；74 张卡中 60 个效果为 `IMPLEMENTED`、9 个为 `PENDING`、5 张为 `NONE`。`nt_004`、`nt_005`、`nt_008` 均已实现。
- **规则与 Unity**：服务端 218/218；直接调用 Unity `6000.0.28f1c1`，EditMode 262/262。F1 的 1920×1080 Play Mode 截图为 [`../../design/assets/demo-nether-status-summon-preview-v1.png`](../../design/assets/demo-nether-status-summon-preview-v1.png)，SHA-256 `A6DEF84189D046603B31B40F201D677A6E59BC8FD15FFBF3EDB33523FDCA1861`。
- **在线与恢复**：Docker Engine 29.8.0、Nakama 3.40.0、PostgreSQL 16.8 健康。炽足兽对局 `4df72230-5bec-4910-9234-3b4a1480777b.biome-rivals` 的关键 revision 为 22/24；WITHER/要塞对局 `93fe5fc6-0773-4330-8cbd-ea0fc5c51243.biome-rivals` 的关键 revision 为 35/36/38，双方重连均恢复 revision 36。在线报告 SHA-256 为 `FD2ADA0B7756F3BB24DDE052AD1486AE3C51B3AE0913F13B050B4AB9A540D005`。

结论：RULE-032 已关闭，没有遗留实现或验收项。下一项为 RULE-033A，只冻结悬置/回手契约并拆分后续任务。

## 最小交接

```text
任务编号：RULE-033A
只冻结悬置/回手规则契约并拆分后续任务；不得实现运行时代码、改协议或注册卡牌。
仓库：D:\gitt\mc_biome_rivals
基线：包含 RULE-032F3 证据收口提交；RULE-032 已关闭。
先读：Minecraft_Biome_Rivals_GDD_v0.5.md、docs/design/Minecraft_Biome_Rivals_Prototype_Cards_v0.1.md、RULE-032 任务包的拆分方式，以及最近 RULE-032F2—F3 change-log。
开始前记录 git status；保留无关本机文件。
冻结除外区公开模型、所有权、进入/离开时机、本回合费用修正、不可回手/不可悬置边界、事件顺序与快照恢复；把实现、Unity、Docker 和证据收口拆成独立任务。
结束时报告冻结裁决、未决风险、后续子任务边界、版本不变证明和提交号。
```

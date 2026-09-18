# RULE-032 下界状态与召唤任务包

更新时间：2026-09-18
起始基线：`<RULE-031E3 提交完成后填写>`

本任务只处理 `nt_004` 炽足兽、`nt_005` 凋灵骷髅与 `nt_008` 下界要塞。服务端继续作为规则事实源，Unity 离线 Demo 保持同构。每个子任务独立验证、独立提交；不得顺带实现悬置/回手、末地奖励、交易或其他预留效果。保留来源未明的两个本机未追踪文件。

## RULE-032A 规则契约冻结

状态：**下一唯一任务**。

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

状态：**等待 A**。只实现 `nt_004`：服务端权威战吼、Unity 回放、离线 Demo、3D 目标选择、内容注册和针对性测试。不得引入 WITHER 或下界要塞逻辑。

## RULE-032C WITHER 状态基础设施

状态：**等待 B**。只建立契约要求的 WITHER 快照/事件/校验/结算/重连模型与服务端、Unity 测试；`nt_005` 保持 `PENDING`，不得从卡牌创建 WITHER。

## RULE-032D 凋灵骷髅纵向切片

状态：**等待 C**。只让 `nt_005` 在合法普通战斗伤害后创建 WITHER，覆盖主动攻击、反击、目标死亡、刷新/叠加、击杀归属和终局；同步 Unity 与内容注册。

## RULE-032E 下界要塞纵向切片

状态：**等待 D**。只实现 `nt_008` 的结束阶段支付与 `tk_015` 召唤，复用临时红石优先支付；覆盖多实例、末格竞争、满场、能量不足、令牌注册、重连和离线同构。

## RULE-032F1 确定性演示与 Unity 视觉审查

状态：**等待 E**。只构建三张牌的本地确定性 Play Mode 演示、原版模型/状态反馈与 UI 可读性；由 Unity 实际渲染截图，不运行 Docker。

## RULE-032F2 Docker 双端与重连

状态：**等待 F1**。只验证三张牌的权威事件、双方投影、WITHER 恢复、要塞支付/召唤和中途重连；记录 Match ID、revision 与探针产物，不改视觉。

## RULE-032F3 证据收口

状态：**等待 F2**。只复核提交、版本、测试、Unity 截图和在线产物，更新日志、路线图与交接并关闭 RULE-032；不得新增玩法。

## 最小交接

```text
任务编号：RULE-032A
只冻结 NT-004/NT-005/NT-008 的规则契约；不得修改运行时代码、注册状态或版本。
仓库：D:\gitt\mc_biome_rivals
基线提交：<RULE-031E3 提交完成后填写>
先读：docs/development/task-packets/RULE-032.md 中 RULE-032A 列出的入口。
开始前记录 git status；保留无关本机文件。
结束时报文档、歧义裁决、验证、风险和提交号。
```

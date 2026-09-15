# RULE-031 下界自伤与临时能量任务包

更新时间：2026-09-15
起始基线：`a45dd8f docs: close RULE-030 evidence and scope next contract`

只处理 `nt_002` 僵尸猪灵和 `nt_007` 重生锚。两个卡牌效果目前均为 `PENDING`；服务端是规则事实源，Unity 离线 Demo 保持同构。每项单独验证、单独提交；不得顺带实现 WITHER、炽足兽、下界要塞、悬置或回手。保留来源未明的两个本机未追踪文件。

## RULE-031A 规则契约冻结

状态：**已完成**。冻结裁决见 [`../../design/nether-self-damage-energy-spec-v1.md`](../../design/nether-self-damage-energy-spec-v1.md)；本项仅提交契约与任务状态文档，未改运行时代码、注册状态或版本。

目标：只消除实现前的规则歧义，不改运行时代码、注册状态或版本。

需定案：英雄“受到伤害”是否以最终实际掉血为准及护甲边界；同一回合内首次触发的作用域、重置点和多实例顺序；僵尸猪灵 +1/+1 的永久性及当前生命处理；其岩浆结束阶段支付与伤害顺序；重生锚仅己方回合触发的判定、临时红石能量上限/支付/过期与多锚顺序；非法状态及终局中止边界。

入口：`Minecraft_Biome_Rivals_GDD_v0.5.md` §3/4/6/16、`docs/design/Minecraft_Biome_Rivals_Prototype_Cards_v0.1.md` 中 NT-002/NT-007、`shared-schema/card-data/implemented-effect-registry.v1.json`、`server-nakama/src/rules/match-engine.ts` 的英雄伤害/结束阶段流程、`client-unity/Assets/Game/Demo/Runtime/DemoLocalMatch.cs`。

产物：`docs/design/nether-self-damage-energy-spec-v1.md`。完成定义：至少包含触发时点、首次标记、己方回合边界、资源临时性、支付原子性、同时触发顺序、受伤与护甲示例；提交只含契约/任务状态文档。

## RULE-031B1 首次英雄生命损失标记

状态：**已完成**。已建立“当前单人回合首次实际掉血”的权威标记：普通/真实伤害、护甲-only、攻击与反击、疲劳、建筑和亡语均通过同一英雄生命变化入口；快照、事件、服务端/Unity 仓库可回放，并在每次 `TURN_ENDED` 对双方一起重置。两张牌继续 `PENDING`，没有临时能量字段。协议升至 33、规则集升至 `prototype-0.52`，内容版本不变。完成证据见 `docs/development/change-log.md`；独立提交。

## RULE-031B2 临时红石能量基础设施

状态：**下一唯一任务**。只建立可快照、回放、优先支付和结束阶段清理的基础/临时能量模型及协议/Unity 投影；两张牌保持 `PENDING`。完成定义：容量/总量/临时可用量/过期及矛盾状态在服务端/Schema/Unity 同构校验，重连可恢复，资源支付原子性与回合末清理有测试；独立提交。

## RULE-031C 僵尸猪灵纵向切片

状态：**待 B2 完成**。一次完成 `nt_002` 的两段牌面效果：首次己方英雄实际受伤后的永久 +1/+1，以及岩浆结束阶段消耗余能并伤害敌方英雄。完成定义：伤害来源、护甲、同回合重复、多实例、支付、致死中止和 Unity 离线同构均有测试；注册状态只在两段都完成后改为 `IMPLEMENTED`，独立提交。

## RULE-031D 重生锚纵向切片

状态：**待 C 完成**。复用 B2 的临时能量字段，仅实现 `nt_007` 己方回合首次英雄受伤触发。完成定义：多锚顺序、资源边界、回合末过期、服务端权威事件、Unity 回放及离线同构均通过；独立提交。

## RULE-031E 集成验收（启动时再拆）

状态：**待 D 完成**。不得作为一个大任务直接执行；启动前按确定性演示、Docker 双端/重连、证据收口拆成独立子包，并为每包写入口、完成定义和提交边界。

## 最小交接

```text
任务编号：RULE-031B2
只建立临时红石能量基础设施，不提前开放 NT-002/NT-007 效果。
仓库：D:\gitt\mc_biome_rivals
基线提交：<启动时填写>
先读：docs/development/task-packets/RULE-031.md 中当前任务和列出的入口。
开始前记录 git status；保留无关本机文件。
结束时报文档、歧义裁决、验证、风险和提交号。
```

# Shared Schema

这里保存跨进程边界的规范，不保存任一语言的业务实现。

- `protocol/`：网络命令、事件批次与快照；
- `card-data/`：设计数据的机器可读约束。

修改流程：先修改 Schema 和版本，再更新 TypeScript/C# 类型，最后增加双端兼容性测试。原型期使用 JSON Schema 2020-12。

当前 `protocolVersion: 29`。协议 29 新增公开战场选择 `HEAL_UNIT`，用于雪屋在多个“最大缺失生命”单位并列时由回合玩家选择；双方都能看到公开候选的牌名与格位，只有拥有者可以提交。玩家公开状态继续包含 DARK、回合出牌计数、首次敌方战场对象指定标记与公开触发键。`CARD_BURIED.payload.cardId` 对拥有者保留真实值、对另一方投影为 `null`；`CARD_GENERATED` 与 `OBJECT_SUMMONED` 的 `sourceInstanceId` 同时允许真实 `object-*` 和无实体效果来源 `effect-*`。`PLAY_CARD.payload.targetInstanceIds` 支持双目标法术提交恰好两个不重复的稳定对象 ID。快照只发送观察者自己的卡牌 ID 和双方区域计数；对手手牌使用 `null` 占位，完整牌库顺序、权威随机种子/计数器与内部命令幂等记录不属于客户端 Schema。战场对象公开相邻生命修正、最终最大生命和 `SLOW`/`POISON` 状态；待处理选择会阻断除认输外的其他行动。

卡牌定义 Schema v4 新增 `manualPlayAllowed`。它与 `effectImplementationStatus` 正交：自动触发的效果可以是 `IMPLEMENTED`，同时通过 `manualPlayAllowed: false` 禁止客户端提交与服务端执行主动打出。

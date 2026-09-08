# Shared Schema

这里保存跨进程边界的规范，不保存任一语言的业务实现。

- `protocol/`：网络命令、事件批次与快照；
- `card-data/`：设计数据的机器可读约束。

修改流程：先修改 Schema 和版本，再更新 TypeScript/C# 类型，最后增加双端兼容性测试。原型期使用 JSON Schema 2020-12。

当前 `protocolVersion: 27`。`CARD_BURIED.payload.cardId` 对拥有者保留真实值、对另一方投影为 `null`；`CARD_GENERATED` 与 `OBJECT_SUMMONED` 的 `sourceInstanceId` 同时允许真实 `object-*` 和无实体效果来源 `effect-*`。`PLAY_CARD.payload.targetInstanceIds` 支持双目标法术提交恰好两个不重复的稳定对象 ID。快照只发送观察者自己的卡牌 ID 和双方区域计数；对手手牌使用 `null` 占位，完整牌库顺序、权威随机种子/计数器与内部命令幂等记录不属于客户端 Schema。双方已确认的 `factionId`、`mulliganCompleted`、`buriedCount`、`excavatedThisTurn` 与本回合公开触发标记属于公开状态。`CARD_DRAWN` 与生成到手牌的卡牌身份同样按观察者投影，`CARD_BURNED`、出土牌和公开弃牌则对双方可见。战场对象公开相邻生命修正、最终最大生命和 `SLOW`/`POISON` 状态；`OBJECT_STATUS_TICKED` 发布结束阶段结算后的剩余时长，属性事件可携带状态伤害来源与 `damageType`，使纯事件回放与权威状态保持一致。选择拥有者会收到真实考古、牌库顶窥视或移动选项；牌库选择向另一方投影为不含牌名的合法占位，`TOP_CARD_SCRY` 的解决事件也不会向对手泄露 `selectedCardId`；待处理选择会阻断除认输外的其他行动。

卡牌定义 Schema v4 新增 `manualPlayAllowed`。它与 `effectImplementationStatus` 正交：自动触发的效果可以是 `IMPLEMENTED`，同时通过 `manualPlayAllowed: false` 禁止客户端提交与服务端执行主动打出。

# Shared Schema

这里保存跨进程边界的规范，不保存任一语言的业务实现。

- `protocol/`：网络命令、事件批次与快照；
- `card-data/`：设计数据的机器可读约束。

修改流程：先修改 Schema 和版本，再更新 TypeScript/C# 类型，最后增加双端兼容性测试。原型期使用 JSON Schema 2020-12。

当前 `protocolVersion: 14`。快照只发送观察者自己的卡牌 ID 和双方区域计数；对手手牌使用 `null` 占位，完整牌库顺序与内部命令幂等记录不属于客户端 Schema。双方已确认的 `factionId`、`mulliganCompleted`、`buriedCount` 与 `excavatedThisTurn` 属于公开状态；具体调度结果只投影给牌的拥有者。`CARD_DRAWN` 同样按观察者投影，只有抽牌者收到 `cardId`；`CARD_BURNED` 因规则要求公开，双方都收到卡牌 ID。v10 增加公开 `OBJECT_SUMMONED`；v11 增加二元支付与公开 `MATERIALS_CONSUMED`；v12 增加 `CARD_BURIED` / `CARD_EXCAVATED` 及快照掩埋计数；v13 增加 `RESOLVE_CHOICE`、`pendingChoice`、`CHOICE_OFFERED` 与 `CHOICE_RESOLVED`；v14 增加可重连恢复的本回合出土标记。选择拥有者会收到真实选项与可选标记，另一方只收到同数量的隐藏占位；待处理选择会阻断除认输外的其他行动。掩埋牌身份在埋入与出土时公开、位置始终隐藏；每次 `CARD_EXCAVATED` 都将对应玩家的标记设为真，回合结束清除，因此客户端可确定性重建 DB-005 的动态费用，但不能推断牌库顺序。

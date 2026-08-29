# Shared Schema

这里保存跨进程边界的规范，不保存任一语言的业务实现。

- `protocol/`：网络命令、事件批次与快照；
- `card-data/`：设计数据的机器可读约束。

修改流程：先修改 Schema 和版本，再更新 TypeScript/C# 类型，最后增加双端兼容性测试。原型期使用 JSON Schema 2020-12。

当前 `protocolVersion: 15`。快照只发送观察者自己的卡牌 ID 和双方区域计数；对手手牌使用 `null` 占位，完整牌库顺序与内部命令幂等记录不属于客户端 Schema。双方已确认的 `factionId`、`mulliganCompleted`、`buriedCount` 与 `excavatedThisTurn` 属于公开状态；具体调度结果只投影给牌的拥有者。`CARD_DRAWN` 同样按观察者投影，只有抽牌者收到 `cardId`；`CARD_BURNED` 因规则要求公开，双方都收到卡牌 ID。v15 为战场对象增加公开、可重连的 `statuses`，并以 `OBJECT_STATUS_APPLIED` / `OBJECT_STATUS_REMOVED` 记录来源、剩余持续时间和绑定数值修正。粉雪桶的缓慢因此可由客户端确定性回放，禁止攻击与 -2 攻击也由权威状态统一判定。选择拥有者会收到真实考古选项与可选标记，另一方只收到同数量的隐藏占位；待处理选择会阻断除认输外的其他行动。

# Shared Schema

这里保存跨进程边界的规范，不保存任一语言的业务实现。

- `protocol/`：网络命令、事件批次与快照；
- `card-data/`：设计数据的机器可读约束。

修改流程：先修改 Schema 和版本，再更新 TypeScript/C# 类型，最后增加双端兼容性测试。原型期使用 JSON Schema 2020-12。

当前 `protocolVersion: 21`。快照只发送观察者自己的卡牌 ID 和双方区域计数；对手手牌使用 `null` 占位，完整牌库顺序与内部命令幂等记录不属于客户端 Schema。双方已确认的 `factionId`、`mulliganCompleted`、`buriedCount`、`excavatedThisTurn` 与本回合公开触发标记属于公开状态；具体调度结果只投影给牌的拥有者。`CARD_DRAWN` 同样按观察者投影，只有抽牌者收到 `cardId`；`CARD_BURNED` 因规则要求公开，双方都收到卡牌 ID。v21 的战场对象公开海龟相邻生命修正，属性事件可携带最终最大生命，并以 `PERMANENT_HEALTH_MODIFIER` 表达珊瑚礁和驯服的狼的永久成长；海底神殿沿用既有 `OBJECT_STATS_CHANGED/DAMAGE` 事件结构，不增加协议字段。选择拥有者会收到真实考古或移动选项，另一方只收到合法的公开占位；待处理选择会阻断除认输外的其他行动。

# Shared Schema

这里保存跨进程边界的规范，不保存任一语言的业务实现。

- `protocol/`：网络命令、事件批次与快照；
- `card-data/`：设计数据的机器可读约束。

修改流程：先修改 Schema 和版本，再更新 TypeScript/C# 类型，最后增加双端兼容性测试。原型期使用 JSON Schema 2020-12。

当前 `protocolVersion: 16`。快照只发送观察者自己的卡牌 ID 和双方区域计数；对手手牌使用 `null` 占位，完整牌库顺序与内部命令幂等记录不属于客户端 Schema。双方已确认的 `factionId`、`mulliganCompleted`、`buriedCount` 与 `excavatedThisTurn` 属于公开状态；具体调度结果只投影给牌的拥有者。`CARD_DRAWN` 同样按观察者投影，只有抽牌者收到 `cardId`；`CARD_BURNED` 因规则要求公开，双方都收到卡牌 ID。v16 允许 `DEPLOY_CARD` 携带可选的稳定目标实例，供 SI-003 这类“先选战吼目标、再选部署格”的单位使用；扣费与部署前会统一校验目标。公开 `statuses` 与状态事件同时记录实际生效的 `attackModifier` 和规则绑定下限 `boundAttackModifier`，从而让 0 攻击目标在状态到期时只恢复真正扣除的数值。流浪者与粉雪桶共用不叠加的 `SLOW`，后者可把已有缓慢强化到 -2 攻击。选择拥有者会收到真实考古选项与可选标记，另一方只收到同数量的隐藏占位；待处理选择会阻断除认输外的其他行动。

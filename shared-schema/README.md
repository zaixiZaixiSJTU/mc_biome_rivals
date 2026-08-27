# Shared Schema

这里保存跨进程边界的规范，不保存任一语言的业务实现。

- `protocol/`：网络命令、事件批次与快照；
- `card-data/`：设计数据的机器可读约束。

修改流程：先修改 Schema 和版本，再更新 TypeScript/C# 类型，最后增加双端兼容性测试。原型期使用 JSON Schema 2020-12。

当前 `protocolVersion: 7`。快照只发送观察者自己的卡牌 ID 和双方区域计数；对手手牌使用 `null` 占位，完整牌库顺序与内部命令幂等记录不属于客户端 Schema。双方已确认的 `factionId` 和 `mulliganCompleted` 属于公开状态；具体调度结果只投影给牌的拥有者。`CARD_DRAWN` 同样按观察者投影，只有抽牌者收到 `cardId`；`CARD_BURNED` 因规则要求公开，双方都收到卡牌 ID。v4 增加权威 `PLAY_CARD` 与英雄效果事件；v5 增加稳定目标实例与临时属性恢复；v6 加入群系 ID；v7 加入 `MULLIGAN_COMPLETED`、`MATCH_STARTED` 与起手选择命令。

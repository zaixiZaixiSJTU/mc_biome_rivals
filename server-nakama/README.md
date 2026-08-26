# Nakama Server Module

`src/rules` 是纯规则核心；`src/matches` 和 `src/rpc` 是 Nakama 边界适配器。构建产物只有 `build/index.js`，供 Docker 容器加载。

当前实现工程闭环需要的开局快照、卡牌部署、主行动/战斗阶段、普通攻击、同步反击、死亡离场、英雄伤害/护甲、胜负、结束回合和认输。战场格保存稳定实例 ID，多格结构的多个格子引用同一个状态对象。嘲讽、冲锋、法术目标与具体卡牌效果仍待实现。新增规则时先写规则测试，再接 Match Handler，避免把领域逻辑写进网络回调。

协议 opcode：`1` 命令、`2` 事件批次、`3` 命令拒绝、`4` 权威快照。协议结构以 `shared-schema/protocol` 为准。

当前战斗纵向切片使用 `protocolVersion: 2` 与 `rulesetVersion: prototype-0.2`。v2 将战场格内容从卡牌 ID 改为稳定实例 ID，不能由 v1 客户端静默兼容。

运行：

```powershell
npm test
npm run build
```

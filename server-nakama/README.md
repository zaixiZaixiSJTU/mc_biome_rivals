# Nakama Server Module

`src/rules` 是纯规则核心；`src/matches` 和 `src/rpc` 是 Nakama 边界适配器。构建产物只有 `build/index.js`，供 Docker 容器加载。

当前实现工程闭环需要的开局快照、卡牌部署、结束回合和认输。卡牌部署由权威规则校验手牌、红石、卡牌类型以及 4 个单位格/3 个建筑格；法术目标与效果结算仍待实现。新增规则时先写规则测试，再接 Match Handler，避免把领域逻辑写进网络回调。

协议 opcode：`1` 命令、`2` 事件批次、`3` 命令拒绝、`4` 权威快照。协议结构以 `shared-schema/protocol` 为准。

运行：

```powershell
npm test
npm run build
```

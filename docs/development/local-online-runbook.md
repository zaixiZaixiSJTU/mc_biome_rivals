# 本地权威联机验证

## 启动

```powershell
npm run build --workspace server-nakama
docker compose up -d
docker compose ps
npm run smoke:integration --workspace server-nakama
```

等待 `postgres` 健康且 `nakama` 为 running 后，启动两个 Windows Demo 实例，在顶部点击“联机”。双方状态应依次经过：

1. 身份认证中
2. 连接服务器
3. 寻找对手中
4. 进入权威对局
5. 权威对局已连接

服务端应记录一次 `Created authoritative Biome Rivals match`，随后两个客户端各收到只针对自己的 opcode `4` 初始快照。

`smoke:integration` 会自动创建两个临时设备用户，验证严格双人匹配、进入同一个权威房间、双方私有初始快照，以及一次 `END_TURN` 命令获得双方 opcode `2` 事件批次回执。它不会修改持久化玩家资产。

## 配置

默认参数与 `docker-compose.yml` 一致：

- HTTP `127.0.0.1:7350`
- server key `local_only_change_me`
- 两人严格匹配
- 匹配超时 30 秒
- 意外断线最多重连 3 次，并优先按原 Match ID 重入

可通过环境变量覆盖：`BIOME_RIVALS_NAKAMA_SCHEME`、`BIOME_RIVALS_NAKAMA_HOST`、`BIOME_RIVALS_NAKAMA_PORT`、`BIOME_RIVALS_NAKAMA_SERVER_KEY`。

## 状态与命令语义

`IMatchTransport` 只负责连接生命周期和 opcode 消息；`AuthoritativeMatchGateway` 负责协议反序列化；`MatchCommandDispatcher` 负责 pending 命令，并等待事件批次中的 `acknowledgedCommandId` 或 opcode `3` 拒绝。超时、断线和拒绝都不能被 UI 当成成功。

当前 Demo 的联机条是传输诊断入口，棋盘仍明确使用本地规则模型。下一阶段应让独立的权威对局 Presenter 订阅 `MatchStateStore`，再把线上命令接入同一套卡牌/战场 View。

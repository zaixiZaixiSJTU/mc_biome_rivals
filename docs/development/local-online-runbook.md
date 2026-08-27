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

`smoke:integration` 会自动创建两个临时设备用户，分别以海洋河流和末地群系进入严格双人匹配，验证进入同一个权威房间、双方私有初始快照中的公开群系映射、并发起手确认、正式开局事件，以及一次 `END_TURN` 命令获得双方 opcode `2` 事件批次回执。它不会修改持久化玩家资产。

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

收到 opcode `4` 后，Demo 标题会切换为“权威联机对局”，并以 `DemoAuthoritativeMatchView` 将观察者固定映射到近端：己方私有手牌、能量、生命、牌库/弃牌数量、双方单位/建筑槽和阶段全部从 `MatchStateStore` 渲染。部署、施法、进入战斗、攻击和结束回合经 `DemoOnlineMatchSession` 发往服务器；pending 期间交互锁定，只有 opcode `2` 的命令回执会推进画面，opcode `3`、断线和超时都会显示失败。

## Unity 双进程探针

Windows Development Player 支持以下仅用于自动验证的参数：

- `-autoOnline`：启动后自动进入匹配。
- `-previewPlayerFaction <ID>`：在进入匹配前选择本机群系；双进程验证应为两端传入不同 ID。
- `-previewOpponentFaction <ID>`：只用于离线预览远端半场；权威联机后会被服务器快照覆盖。
- `-previewMulligan`：离线显示真实卡面与材质化起手调度层，用于视觉回归，不提交规则命令。
- `-autoOnlineAction`：双方先自动保留全部起手牌；当前行动方随后发送 `ENTER_COMBAT` 与 `END_TURN`，两端等待相同的最终 revision。
- `-nakamaDeviceId <ID>`：为同机并行实例指定不同设备身份，长度 10–128。
- `-onlineProbe <json>`：写出 Match ID、观察者 ID、revision、阶段、手牌、能量、生命和双方群系。
- `-captureOnline <png>`：权威状态稳定且事件动画队列清空后截图。
- `-quitAfterOnlineProbe`：报告写完后退出。

两个报告必须具有相同 Match ID、不同观察者 ID、`ACTIVE` 状态和相同 revision；双方起手确认字段均为 `true`，私有手牌应不同，且 `playerFaction/opponentFaction` 互为镜像。

海洋河流对末地的权威双客户端验证截图：[`../design/assets/demo-authoritative-ocean-vs-end-v1.png`](../design/assets/demo-authoritative-ocean-vs-end-v1.png)。截图中的近端海洋与远端末地由各自玩家提交并经服务器快照确认，两块半场仍共用同一透视平面。

双方完成调度并进入正式回合后的权威截图：[`../design/assets/demo-authoritative-after-mulligan-v1.png`](../design/assets/demo-authoritative-after-mulligan-v1.png)。

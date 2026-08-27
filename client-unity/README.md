# Unity Client

这是锁定到 Unity 6.0 中国版 `6000.0.28f1c1` 的客户端工程。

## 首次打开

1. 安装 Unity `6000.0.28f1c1`；模块至少选择 Windows Build Support (IL2CPP) 和 Microsoft Visual Studio Community（已有 IDE 可不选）。
2. 用 Hub 打开本目录。若提示补丁版本升级，记录并审查 `ProjectSettings`/`Packages` 变化。
3. 等待 Package Manager 完成解析，在 Test Runner 中运行 EditMode 测试。
4. 创建 `Bootstrap` 场景；`GameCompositionRoot` 会在任何场景载入前自动建立，所以空场景也能启动基础设施。

## 第三方依赖边界

- Nakama Unity SDK 已通过 UPM 锁定到 `v3.21.1`。只有 `NakamaMatchTransport` 与 Networking 程序集引用 `NakamaRuntime`；规则、卡牌内容和 UI 不直接引用 SDK 类型。
- DOTween：导入并锁定版本，实现 `ITweenService`；业务 Presenter 只能依赖该接口。

不要直接把 Nakama 客户端散落在 UI 中，也不要从卡牌规则代码直接调用 DOTween。这样断线模拟、离线测试和以后替换依赖都能保持局部修改。

## 本地联机

默认连接参数位于 `Assets/Game/Networking/Resources/Networking/nakama-connection.v1.json`，与根目录 `docker-compose.yml` 对齐。主机、端口、协议和 server key 可分别用 `BIOME_RIVALS_NAKAMA_HOST`、`BIOME_RIVALS_NAKAMA_PORT`、`BIOME_RIVALS_NAKAMA_SCHEME`、`BIOME_RIVALS_NAKAMA_SERVER_KEY` 覆盖。

Demo 顶部的联机状态条用于验证认证、Socket、匹配、权威 Match 加入和重连生命周期。当前棋盘交互仍由明确标注的本地 Demo 状态驱动；在权威快照 Presenter 完成前，不把“通道已连接”误当成“线上棋盘已接管”。

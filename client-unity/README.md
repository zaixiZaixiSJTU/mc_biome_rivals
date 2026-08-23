# Unity Client

这是锁定到 Unity 6.0 中国版 `6000.0.28f1c1` 的客户端工程。

## 首次打开

1. 安装 Unity `6000.0.28f1c1`；模块至少选择 Windows Build Support (IL2CPP) 和 Microsoft Visual Studio Community（已有 IDE 可不选）。
2. 用 Hub 打开本目录。若提示补丁版本升级，记录并审查 `ProjectSettings`/`Packages` 变化。
3. 等待 Package Manager 完成解析，在 Test Runner 中运行 EditMode 测试。
4. 创建 `Bootstrap` 场景；`GameCompositionRoot` 会在任何场景载入前自动建立，所以空场景也能启动基础设施。

## 接入第三方依赖

- Nakama Unity SDK：锁定具体 release/tag，实现 `IMatchTransport`，只让该适配器引用 SDK，然后在 `GameCompositionRoot.RegisterOnlineTransport` 注册。
- DOTween：导入并锁定版本，实现 `ITweenService`；业务 Presenter 只能依赖该接口。

不要直接把 Nakama 客户端散落在 UI 中，也不要从卡牌规则代码直接调用 DOTween。这样断线模拟、离线测试和以后替换依赖都能保持局部修改。

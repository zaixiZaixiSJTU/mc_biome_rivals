# ADR-001：客户端、权威服务端与协议边界

- 状态：已接受
- 日期：2026-08-13
- 对应规格：GDD v0.5 §31

## 背景

项目需要兼顾卡牌动画表现、隐藏信息、断线重连、回放和后续频繁调平。玩法尚会变化，因此规则必须容易测试，第三方库必须容易替换。

## 决定

1. 客户端采用 Unity 6.0 中国版 `6000.0.28f1c1` 与 C#。菜单以 UI Toolkit 为主，战场与世界空间交互使用 GameObject/uGUI。
2. 对局采用 Nakama 权威 Match Handler。客户端提交意图命令，服务端验证并产生有序事件。
3. 服务端规则核心使用纯 TypeScript，不直接依赖 Nakama、Node.js、时钟、文件系统或全局随机数。
4. 协议、规则、表现三层分别版本化。JSON 只用于原型期边界传输，内部领域对象不依赖序列化格式。
5. 客户端动画由 PresentationQueue 消费权威事件；DOTween 通过 `ITweenService` 适配，不进入规则层。
6. PostgreSQL 保存账号和长期数据；进行中的对局状态由 Match Handler 管理，必要时写入快照。

## 结果

- 可以在不启动 Unity、Nakama 或数据库的情况下测试大部分规则。
- Unity 离线网关与在线网关共享同一接口，UI 开发不必等待后端。
- 修改协议时需要同步 Schema、TypeScript 类型、C# DTO 和兼容性测试；这是有意保留的显式成本。
- 原型期 JSON 易调试但流量较大；达到性能门槛后再依据测量结果决定是否迁移 MessagePack/Protobuf。

## 不在本 ADR 中决定

- 最终卡牌内容格式与热更新策略；
- 排位赛季、反作弊和生产部署拓扑；
- DOTween 的购买版本与 Nakama Unity SDK 的精确提交哈希。

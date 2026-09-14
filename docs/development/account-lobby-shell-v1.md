# 账户与大厅外壳 v1

状态：NET-022 最小可用边界，2026-09-14。

## 目标与边界

本阶段只建立可替换的账户、原型卡组和匹配入口，不实现邮箱注册、第三方登录、卡组编辑、收藏、好友或商业账户系统。

## 运行时分层

1. `IPlayerAccountService` 面向 UI，公开账户状态、游客认证、改名和退出；不暴露 Nakama SDK 类型。
2. `IPlayerAccountBackend` 是后端适配口。当前 `NakamaPlayerAccountBackend` 负责设备身份、token 恢复、资料读取与更新。
3. `IPlayerAccountSessionProvider` 只在 Networking 层把已认证会话交给实时匹配传输。
4. `NakamaMatchTransport` 不再创建或保存账户身份，只负责 socket、匹配、重连和 opcode。
5. `GameCompositionRoot` 持有跨对局复用的账户服务；断开一场匹配不会退出账户。

## 状态模型

`SignedOut → Authenticating → Ready` 是正常登录路径；资料更新使用 `Updating`，退出使用 `SigningOut`。认证失败进入 `Failed`，再次点击匹配会重新认证；认证取消返回 `SignedOut`。只有 `Ready` 且资料存在时 `CanMatch=true`。

## 当前大厅 UI

- 顶部像素石砖条展示游客身份、当前原型卡组、连接状态和“匹配/取消/断开”主入口。
- 左侧原“群系”栏明确改名为“卡组”；选择卡组后，群系、卡面和战场主题仍按同一 ID 切换。
- 匹配开始后卡组选择锁定；取消或断开后恢复选择。
- 昵称最多显示八个字符，完整资料保留在账户模型中。

## 配置与凭证

- 连接配置来自 Unity Resource `Networking/nakama-connection.v1.json`；资源缺失会直接失败，不使用编译进代码的 server key 后备值。
- 本地开发 key 仅存在于本地联机配置，部署环境可用 `BIOME_RIVALS_NAKAMA_*` 覆盖。
- 设备 ID 与 token 使用 `PlayerPrefs` 保存；命令行探针按设备 ID 使用隔离的 token key。
- `PlayerPrefs` 只满足原型阶段的会话恢复，不视为安全凭证库；正式发布前应由平台 Keychain/Credential Locker 适配器替换。

## 后续替换点

- 邮箱、平台或第三方登录：增加新的 `IPlayerAccountBackend`，UI 不依赖 Nakama。
- 卡组编辑与收藏：在卡组选择与 `MatchmakingPreferences` 之间增加独立 deck service；服务端仍需权威校验卡组。
- 正式大厅场景：复用 `IPlayerAccountService` 状态，不直接访问 socket 或 SDK session。

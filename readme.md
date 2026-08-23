# Minecraft: Biome Rivals

这是《Minecraft：群系争霸》的最小工程骨架。当前目标不是一次写完玩法，而是先建立一个可构建、可测试、可替换依赖的纵向切片：

- `client-unity/`：Unity 6 客户端，按 Core / Networking / Presentation / Bootstrap 分层；
- `server-nakama/`：Nakama TypeScript Runtime 与不依赖运行时的纯规则核心；
- `shared-schema/`：客户端和服务端共同遵守的 JSON Schema 协议；
- `ops/` 与 `docker-compose.yml`：本地 Nakama + PostgreSQL；
- `scripts/`：环境发现、安装和统一验证入口；
- `docs/decisions/`：影响长期维护的架构决策记录。

## 快速开始

前置环境：Node.js 20、npm、Docker Desktop，以及 Unity 6.0 中国版 `6000.0.28f1c1`。不要把生产账号或密码写入仓库。

```powershell
Copy-Item .env.example .env
.\scripts\bootstrap.ps1
npm test
docker compose up --build
```

另开终端查找并打开 Unity 工程：

```powershell
.\scripts\find-unity.ps1
```

卡牌内容流水线：

```powershell
# 从原型卡表生成完整定义、中文文本和稳定效果槽，并同步到 Unity Resources
.\scripts\sync-card-content.ps1

# 从本机 Minecraft Java JAR 按白名单提取临时卡图（生成物不提交 Git）
.\scripts\extract-minecraft-card-icons.ps1

# 检查 74 个定义/文本/卡图映射、7 套主题和文字对比度
.\scripts\validate-card-content.ps1

# 直接调用锁定版本的 Unity，编译并运行 EditMode 测试
.\scripts\validate-unity.ps1
```

卡面规范与七群系视觉样张见 [Card Face Design System](docs/design/Card_Face_Design_System_v0.1.md)，内容生成规则见 [Card Content Registry](docs/design/Card_Content_Registry_v0.1.md)。Minecraft 原版图标仅供本地原型验证，公开发布前需要重新核对当时有效的官方使用规范。

在 Unity Hub 中打开 `client-unity/`。若使用的编辑器补丁版本与 `ProjectVersion.txt` 不同，先在独立分支完成升级并提交由 Unity 产生的项目文件变化。

## 日常命令

| 命令 | 用途 |
|---|---|
| `.\scripts\bootstrap.ps1` | 检查工具并按锁文件安装 Node 依赖 |
| `npm test` | 编译并运行服务端纯规则测试 |
| `npm run build` | 构建 Nakama JavaScript 模块 |
| `.\scripts\validate.ps1` | 执行仓库级静态检查、测试和构建 |
| `.\scripts\validate.ps1 -WithUnity` | 在统一验证中追加 Unity 编译与 EditMode 测试 |
| `docker compose up --build` | 启动 PostgreSQL 与 Nakama |
| `docker compose down` | 停止本地服务并保留数据库卷 |

## 工程约束

1. `server-nakama/src/rules` 不得访问网络、文件、系统时间或未注入的随机数。
2. 客户端只发送命令；服务端事件和快照才是权威状态来源。
3. 表现代码不能修改规则状态；第三方动画库必须藏在 `ITweenService` 后面。
4. 共享协议通过 `protocolVersion` 演进，玩法规则通过 `rulesetVersion` 演进，两者不能混用。
5. 每次新增命令、事件或规则，都必须补测试与协议定义。

玩法规格见 [Minecraft_Biome_Rivals_GDD_v0.5.md](Minecraft_Biome_Rivals_GDD_v0.5.md)，首轮 56 张机制验证卡见 [原型卡池 v0.1](docs/design/Minecraft_Biome_Rivals_Prototype_Cards_v0.1.md)，首个架构决定见 [ADR-001](docs/decisions/ADR-001-technical-foundation.md)。

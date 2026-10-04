# ARENA-037 场地定义驱动的双排战场格位

更新时间：2026-10-03

最新：QA-056 完成 E2d 当前工作区功能门禁，Unity 500/500、服务端 244/244，来源匹配 r10 Player 的三容量 45 次拒绝及普通建筑四局退出 0/0，八图已看。E1、E2a/b/c/d 已过；独立提交树/发行仍待，整体目标不关闭。实图另发现北极熊姿态问题，已拆 UI-066，不以规则成功声称所有生物视觉正确。

## 发现与依据

QA-056 最新：E2d 当前工作区功能通过，Unity 500/500、服务端 244/244；658 项来源匹配 r10 Player 的三容量 45 次拒绝与普通建筑四局全部退出 0/0，双方冻结恢复/模型/八图已复核。原失败与证据边界见 [QA-056](QA-056.md)。E1/E2 功能门禁齐备，可以进入 UI-066；独立提交树/发行与整体目标未完成。

GDD v0.5 §14 已定义总格数为 7、同一局双方使用相同配比，并给出标准原野 4/3、洞穴深处 5/2、下界熔岩海与末地虚空 3/4 等布局；§30.1 要求单位排/建筑排组成双排 2.5D 战场。当前运行时代码尚未支持这些变化：Nakama 初始状态与不变量固定为 4 个单位格/3 个建筑格，快照 Schema 将两数组长度固定为 4/3，Unity 离线状态及 `DemoBattlefield3D.BuildSlotPads()` 也使用固定数量。

因此，“不同地形可以采用不同双排格位”目前只是设计文档能力，不是可玩的功能。不能只改 3D 场景，否则客户端会显示服务端无法接受的落点；也不能只改某个阵营半场，因为 GDD 要求同局双方格位配比一致。

## ARENA-037A 规则契约

场地来源冻结为**本局模式配置**，不从任一玩家的阵营、卡组或客户端请求推导。GDD §3.1 指定双方共用模式指定的同一张对称场地；GDD §14.2 将获取方式留待后续产品设计，但规定基础对局未指定时使用标准原野。当前原型只有标准 PvP 模式，故服务端/离线默认 `standard_meadow`；后续模式可显式指定下表已登记的场地。双方永远使用同一 `arenaId` 和布局。

`arenaId` 与格位配置（仅布局，不启用额外场地效果）：

| arenaId | GDD 名称 | 单位格 | 建筑格 | 来源 |
|---|---|---:|---:|---|
| `standard_meadow` | 标准原野 | 4 | 3 | §5.1、§14.1；原型缺省 |
| `plains_sunrise` | 平原日出 | 4 | 3 | §14.1 |
| `deep_caverns` | 洞穴深处 | 5 | 2 | §5.1、§14.1 |
| `nether_lava_sea` | 下界熔岩海 | 3 | 4 | §5.1、§14.1 |
| `end_void` | 末地虚空 | 3 | 4 | §14.1 |
| `deep_ocean` | 海洋深处 | 4 | 3 | §14.1 |
| `desert_storm` | 沙漠风暴 | 4 | 3 | §14.1 |

单方总格数始终为 7。场地布局只决定单位排/建筑排容量，不自动启用 §14.2 所列环境效果、阵营偏向、费用或状态改动。当前 PvP 没有玩家选场/随机机制；引入这些机制须另行产品决策，并校验牌组可用性与排位限制。

## 子任务拆分

### ARENA-037A 场地选择来源与布局契约

状态：**已完成设计冻结；未改运行时代码**。场地由模式配置给出，当前原型 PvP 模式默认 `standard_meadow`；允许的 7 个场地 ID 与 GDD 配比、双方对称约束和禁止从玩家阵营推导均已在本任务包冻结。

本阶段仅冻结基础字段与布局；当前不随机、不提供玩家场地选择、不启用场地附加效果。依据 GDD §3.1、§5.1、§14.1–14.2 及用户确认“不同地形布局可以不同”。后续运行时切片必须拒绝未知 ID、不匹配数组长度及客户端单方面改变布局。

### ARENA-037B 权威状态与协议承载场地布局

依赖 037A。将 `arenaId`/权威布局贯穿 match init、快照、Schema 与状态不变量；服务端按登记布局初始化数组并拒绝长度不符、缺失或不支持的场地。确保复制、事件、重连和双方投影使用同一布局，不信任客户端单方面提交的槽位数量。

状态：**已完成并通过服务端与 Unity CLI 验证**。服务端注册七种 GDD 布局，模式配置固定缺省 `standard_meadow`，权威状态、快照和事件批次携带 `arenaId`；match init 为双方按同一场地生成数组，状态不变量/schema 拒绝未知 ID、布局不符及不同步事件。Unity 协议 DTO 与快照仓库校验已接入同一注册表。

验证：`server-nakama` TypeScript 类型检查通过、规则/协议测试 **240/240**；Unity CLI `1.0.0-beta.8` 使用 Editor `6000.0.28f1c1` 在隔离副本运行全量 EditMode **363/363 Passed**，XML `Temp/CurrentUnityCli/arena037b-editmode.xml`，SHA-256 `790DAF55646FA23F81D3FD95619622B48E784EB42153C4F27C3F9105B3F9A601`。主工程图形 Editor 保持打开，测试未对其并发运行。该阶段只证明状态/协议闭环，不覆盖离线玩法和 3D 格位生成（037C、037D）。

### ARENA-037C Unity 核心/离线规则使用可变格位

依赖 037A、037B。移除离线 `DemoLocalMatch` 与 `MatchStateStore` 中对 4/3 的隐含假设；部署、移动、邻接、召唤、结构连续占位、死亡清格和满场拒绝均按当前场地布局计算。

状态：**已完成 Unity 核心投影与离线玩法的可变格位支持**。`DemoLocalMatch` 接受登记的 `arenaId`，双方分别创建该布局的单位/建筑格数组；权威 `IDemoMatchView` 暴露相同场地 ID，`MatchStateStore` 按快照 arena 校验容量。部署、山羊越位移动/邻格判定、林间集结自动召唤、两格结构边界、满排拒绝与死亡后多格清理均按数组实际长度工作。场景控制器尚默认标准原野，3D 场地坐标、格片、碰撞体、悬停和 HUD 适配留给 037D。

验证：Unity CLI `1.0.0-beta.8` / Editor `6000.0.28f1c1` 隔离副本 EditMode **376/376 Passed**，报告 `Temp/CurrentUnityCli/arena037c-editmode.xml`，SHA-256 `45362AE1D12293A4533F824CFBF447707CAC1932ABB0C12A2A81356E0BB2B98A`。Windows Development Player 以 CLI 从同一副本构建成功；当前 1920×1080 满手截图经来源清单、日志和退出码校验，SHA-256 `76587A4A1F38DA0D265105C44BB8228965B7D8314F11101E4F73A9F18B1B5DC8`（`Temp/CurrentUnityCli/arena037c-player-final/full-hand-1920.png`）。

### ARENA-037D 2.5D 地表与 UI 适配

依赖 037A、037C。按场地定义生成双方相同的双排格位坐标；对 5/2 与 3/4 布局复核格距、建筑多格占用、透视、模型遮挡、世界空间高亮、射线命中和卡牌/详情 HUD 净空。布局切换时清理旧格位高亮与 Collider，不遗留不可见交互区。

状态：**已完成**。`DemoBattlefield3D` 与 `DemoSceneController` 在构建场景前共享本局 `arenaId`，按布局容量居中生成双侧 2.5D 单位排/建筑排、地表高亮 Collider 与对应 UI 格；标准原野继续维持原有格距。格位拓扑在场地几何创建后不可变，避免旧 Collider/高亮与 UI 脱节。Development Player 新增 `-previewArena <arenaId>` 入口；`capture-demo-preview.ps1 -PreviewArena` 对七个已登记场地逐一运行，并把 `arenaId` 写进截图报告。

验证：Unity CLI `1.0.0-beta.8` / Editor `6000.0.28f1c1` 隔离副本全量 EditMode **383/383 Passed**，报告 `Temp/CurrentUnityCli/arena037d-editmode.xml`，SHA-256 `AD607014618BAE5A6B523B9372EA69A922FC134407CC40E1070AF792691D4816`。新增参数化回归覆盖七个场地的世界格数量、UI 格数量、居中坐标、全部 3D 射线目标、无额外默认格及构建后的拓扑锁定。Unity CLI 构建 Windows Development Player 成功，清单 632 项源码；七个场地均由真实 Player 生成 1920×1080 截图，退出码、源码清单、日志与画幅校验通过：`Temp/CurrentUnityCli/arena037d-player-final/{standard_meadow,plains_sunrise,deep_caverns,nether_lava_sea,end_void,deep_ocean,desert_storm}-1920.png`。另在同一当前源码 Player 对 5/2 `deep_caverns` 与 3/4 `nether_lava_sea` 实跑 1280×720 完整战斗交互、双格结构拖放、末影人+紫颂果回手，以及合成并部署双格沙漠神殿。复核修复了结构拖放场景漏配红石及两个预览脚本的旧经济期待；四条玩法链均有日志/退出码/画幅/清单验证，截图 SHA-256 依次为 `BF42B53FE49FB8D3CAD7F1E5A49C0BBC5EEB25CF50745461B2901C346E9F3883`、`511E052989CB3E33F0571E86C2DD1B07C1A08BED9DE8561CB5745926C8DD9CA5`、`AAB1A537DD855A1A215CCACE8D24BE285E43FAB189A5A31AC3C448D4ADE911DF`、`9F4DD4031725E4B5632739A8C1D32A09B9806CD8F56C8C0A649F1F38FFD97303`，位于 `Temp/CurrentUnityCli/arena037e-player-final/`。5/2 与 3/4 实图已目视确认容纳和交互格未超出场地；主 GUI Editor 未被 CLI 操作。

### ARENA-037E 联机/重连与验收收口

2026-10-03 当前状态：E1、E2a/b/c/d 当前工作区功能门禁通过，独立提交树/发行仍待。协议 40 / prototype-0.65；证据与 Match ID 见 QA-052/053/054/055 及 [QA-056](QA-056.md)，下方 Docker/旧协议记录为历史。

#### ARENA-037E1 权威场地与真实场景基础矩阵

已完成当前工作区功能验收。服务端仅在显式测试 gate 下按审查配置指定场地，不接受客户端/阵营选场，生产默认不变。当前 644 项来源匹配的 r2 Player 七场地均从本地默认布局转为权威布局，真实普通部署/攻击/结束回合/投降、A 端强制重连、全格透视射线/UI 容量与双方 1920×1080 输出通过，退出 0/0。Unity 451/451、服务端 242/242；旧 Collider 立即禁用、UI 同步且保持层级，同布局恢复不重建、断开恢复本地。专项服务已停止且数据保留，原服务健康。独立提交树仍未验证。

#### ARENA-037E2 移动/结构/边界网络纵向链

为控制上下文继续拆分，详见 [QA-053](QA-053.md)：

- **E2a 已完成当前工作区门禁**：真实 UI DeployAsync 参数修复、禁用山羊路径不发命令，22 项引擎集成回归；646 项来源匹配的双 Player 在默认 4/3、5/2、3/4 实跑 UI 末端单位格。5/2 双方均真实部署第五格；三局正常对局、A 重连及双方图形输出通过。不是实际移动/建筑网络验收。
- **E2b 当前工作区门禁通过**：3/4 第四建筑格、5/2 最后建筑格及两种布局的双格结构贴边，经正常卡组/UI/支付真实部署，双端完整占格/模型中心、mesh 跨格、详情 HUD 水平净空及双方重连通过。最终 478/478 与真实 r2 双 Player 证据见 QA-054；不代替 E2c/d。
- **E2c 已完成当前门禁**：普通雪原卡组，真实手牌/战吼动作按钮/友军目标模型/部署地表完成山羊邻格移动；3/4/5 单位格三局验证唯一 OBJECT_MOVED 的山羊来源/对象/坐标/递增事件 ID、原格清空与精确末端占格、目标属性不变/山羊临时攻击、完整战场 hash、双方模型/私有投影及冻结强制断线恢复。标准局使用非 fixture 服务；共同部署入口的双建筑回归继续通过。来源匹配的 r3 Player、484/484 与实图证据见 [QA-055](QA-055.md)。EditMode 按钮回调测试不是图形 raycast 或 Nakama 接受的替代。
- **E2d 当前工作区门禁通过**：真实普通 Session/手牌/Nakama 命令绕过 UI，3/4、5/2、默认非 fixture 4/3 共 45 次容量/结构边界/双向占用拒绝；commandId/code/revision、无事件/支付/消费、每次拒绝前后独立强制恢复完整查看者投影、合法 UI 部署与后续命令、双方最终冻结恢复/完整模型及普通建筑回归均通过。Unity 500/500、服务端全秘密状态回归 244/244；证据和分支边界见 QA-056，不等于独立树或任意长局调度保证。

关键落点需经手牌 UI/3D 指针输入；捕获 gateway 集成测试、基础探针 Session 部署或全格 raycast 均不能替代剩余专项。全部通过后再决定关闭 E。

#### 原始验收约束与历史记录

依赖 037B—D。通过 Docker/Nakama 双客户端验证同场地配比、部署/移动/结构、快照恢复及非法旧索引拒绝；使用 Unity CLI EditMode 与 Windows Development Player 对每种已登记布局做实际截图和输入链验收，并分别记录在线和离线证据。Docker 静态 Compose 检查不替代双端 smoke。

状态：**进行中，双客户端运行验收未完成**。核查发现 `smoke-nakama.mjs`、`smoke-end-return.mjs`、`smoke-nether-trigger.mjs` 与 `smoke-nether-status-summon.mjs` 仍断言旧协议 38 / `prototype-0.63`；已对齐当前协议 40 / `prototype-0.65`。基础双客户端 smoke 另外断言当前唯一 PvP 模式的快照为 `standard_meadow`，双方各有 4 单位格/3 建筑格，并将 arena 与格数写入成功报告。四个脚本均通过 `node --check`；server TypeScript typecheck 与规则/协议测试 **240/240 Passed**。但本机 Docker Desktop Engine 当前不可访问：`com.docker.service` 为 Stopped，CLI 的 `desktop-linux` 与 `default` context 均无对应 named pipe，当前用户也无权打开/启动该服务。因此 Docker/Nakama smoke 尚未运行，不能据此声称联机部署/重连通过；待引擎可用后继续运行本节双客户端验收。

### ARENA-037F 可变单位格空位查找补漏

状态：**已完成并经 Unity CLI EditMode / Development Player 复核**。037D 后续复核发现两处仍固定扫描 4 格：建筑召唤的 UI 就绪查询，以及回手地表脉冲的空格选择。统一改为按 `DemoBattlefield3D.GetSlotCount(Unit)` 传入当前容量，并将活着的多格单位占用范围视为连续占用。新增回归覆盖 5 格布局的第五格、标准 4 格满场、多格跨边界和待清理的死亡单位。2026-10-02 调用点复核澄清：这里的查询只驱动 UI/地表表现；规则引擎的自动召唤本就按动态容量查找空位，并不存在因该 UI 查询而跳过召唤的问题。

验证：Unity CLI `1.0.0-beta.8` / Editor `6000.0.28f1c1` 隔离副本全量 EditMode **385/385 Passed**，报告 `Temp/CurrentUnityCli/arena037f-unit-slot-availability-editmode-final.xml`，SHA-256 `6DA4B2DFCE54E42DE56038968B9DA4214F8BFEB09813B8C0521A67FECAA3B7A5`。回归不仅覆盖空位算法，也实例化 5 格 `DemoSceneController`，通过合法部署填满前四格，验证第五格仍被 UI 召唤就绪查询识别，再部署第五格后准确报告满场。Windows Development Player 由同一隔离副本构建；`deep_caverns` 5/2 布局回手地表脉冲预览通过退出码、源码清单、日志与 1280×720 画幅检查，截图 `Temp/CurrentUnityCli/arena037f-unit-slot-player/deep_caverns-ground-return-pulse-1280.png`，SHA-256 `05F8DACB934E646E414681ADE8D5BC4B5A676C160E1DC20AFFD46CD6BD0E26D5`。本任务只修复客户端离线辅助/表现对可变布局的错误假设，不替代 037E Docker/Nakama 双端验收。

### ARENA-037G 五格场地召唤链 Player 复核

状态：**已完成并经 Unity CLI 全量 EditMode 与 Windows Development Player 双分辨率实跑**。确定性 `-previewWoodlandRally` 按 `arenaId` 计算容量、保留中央唯一空位，通过当前场景配置的离线规则逐格部署单位并真实结算林间集结；`deep_caverns` 下验证单位格 1/2/4/5 合法部署、`tk_004` 在第 3 格召唤、下一步满场抽到 `pf_001`。结算后演示焦点切换到抽到的具体手牌实例，Inspector 不再保留已打出法术的过期手牌选择。

验证：全量 EditMode **385/385 Passed**，报告 `Temp/CurrentUnityCli/arena037g-woodland-rally-editmode-final.xml`，SHA-256 `6FB6C05F9CF45AEB69D09A90AFF0AFEB53BD17B6E97C50513EF88512E2FC8682`。Unity CLI 在隔离副本成功构建 632 项来源清单的 Windows Development Player；两份 `capture-demo-preview.ps1 -PreviewWoodlandRally` 报告均校验完整玩法日志、Player 退出码、构建来源与 PNG 尺寸，并经人工目视检查：1280×720 `Temp/CurrentUnityCli/arena037g-woodland-rally-player-verified/deep_caverns-woodland-rally-1280.png`，SHA-256 `9A881AE9A4D9096A5433F96A492F1123081C0ED5F39D2D42B285E520F419496E`；1920×1080 同目录，SHA-256 `A010B861D4FDA34677850D21429E71ED61F5072130764FB0F7FB032871F1BADF`。该预览验证本地规则/场景/Inspector，不替代服务端权威对局或 037E Docker/Nakama 双端验收。

## 完成定义

- 已批准场地配置与 GDD 一致，总格数规则受权威状态校验。
- 同一场对局双方使用同一 `arenaId` 和数组长度；服务端状态、快照、Unity 投影/离线模型与实际 3D 格位逐项一致。
- 所有格位相关规则按布局动态工作，尤其是连续建筑位、邻格移动、自动召唤和满场边界。
- 每个布局均由 Unity CLI EditMode 覆盖，并由当前源码 Development Player 实际运行；有截图、源码清单、画幅与退出码记录。
- 真实双客户端重连通过后才关闭 037E。

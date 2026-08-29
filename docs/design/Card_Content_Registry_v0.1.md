# 卡牌内容注册流程 v0.1

## 唯一编辑入口

策划当前只编辑 `Minecraft_Biome_Rivals_Prototype_Cards_v0.1.md`。执行 `scripts/sync-card-content.ps1` 后，脚本生成并同步以下注册表；不要手工修改生成的 JSON：

| 注册表 | 内容 |
|---|---|
| `card-definition-registry.v1.json` | 稳定 ID、阵营、主题、稀有度、类型、费用、属性、标签、通用关键词、卡图键和效果槽 |
| `card-name-registry.zh-CN.v1.json` | 卡名与本地化键 |
| `card-text-registry.zh-CN.v1.json` | 中文描述、类型、稀有度、标签和设计备注 |
| `card-art-registry.v1.json` | 卡牌到 Minecraft 本地原型纹理的映射 |
| `card-theme-registry.v1.json` | 七群系卡面主题令牌 |
| `implemented-effect-registry.v1.json` | 已接入权威规则处理器的稳定 `effectId` 白名单 |

数据流为：

`原型卡表 → 同步脚本 → shared-schema → Unity Resources → CardContentLoader → CardFaceView.RenderRegistered`

## 当前注册规模

- 56 张可收集牌。
- 18 张不可收集衍生物。
- 69 张有规则文本的牌预留 `effect.<cardId>.01`；其中 12 张为 `IMPLEMENTED`，其余 57 张为 `PENDING`。
- 5 张无规则文本衍生物状态为 `NONE`。
- 卡牌定义 Schema v3 / 内容版本 v6 注册通用 `keywords`、二元支付配方与 DB-003 考古选择效果；当前注册 4 张 `TAUNT`，并预留 `CHARGE`。首个完整材料循环为 `DB-002 → TK-006 → DB-007`。

## 效果实现约束

`PENDING` 表示卡牌身份、数值和展示文本已注册，但服务端权威规则尚未接入。实现效果时保留现有 `cardId` 和 `effectId`，将其加入 `implemented-effect-registry.v1.json`，同步脚本会生成 `IMPLEMENTED` 状态；同时必须补充服务端规则测试、协议事件与客户端表现映射。禁止客户端卡面组件直接改变对局状态。

`keywords` 与卡牌专属 `effectId` 分开执行。例如铁傀儡的 `TAUNT` 已按通用规则生效，但其“控制建筑时获得 +1/+1”仍可保持 `PENDING`；不得为了启用一个通用关键词而把尚未实现的整段专属效果标记为 `IMPLEMENTED`。

新增或修改卡牌后依次执行：

```powershell
.\scripts\sync-card-content.ps1
.\scripts\validate-card-content.ps1
.\scripts\validate-unity.ps1
```

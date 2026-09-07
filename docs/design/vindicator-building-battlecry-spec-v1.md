# 林地卫道士建筑战吼规范 v1

本规范对应 `protocolVersion 25`、`rulesetVersion prototype-0.39` 与内容版本 31。

## CD-005 林地卫道士

- 基础属性为 3 费 4/3 生物，稳定效果 ID 为 `effect.cd_005.01`。
- 卡牌部署、付款和占位完成后，检查其控制者是否控制至少一个存活的建筑或结构。
- 条件满足时，林地卫道士在当前行动回合获得 +2 攻击；多座建筑不会重复叠加同一战吼。
- 条件不满足时仍正常部署，但不发送属性变化事件。
- 敌方建筑不计入条件；零生命或已离场对象不计入条件。

## 权威事件与生命周期

- `CARD_DEPLOYED` 先公开基础 4/3 与稳定实例 ID。
- 触发战吼后发送独立 `OBJECT_STATS_CHANGED`，其中 `reason = TEMPORARY_ATTACK_MODIFIER`、`sourceCardId` 与 `sourceInstanceId` 均指向林地卫道士。
- 临时攻击累加到通用 `temporaryAttackModifier`，过期回合绑定当前 `turn`；行动者结束回合时发送 `TEMPORARY_EXPIRED` 并只撤销累计临时修正。
- 本效果不新增协议字段；客户端通过既有事件回放得到与权威快照一致的 6/3，再在回合结束恢复为 4/3。

## Unity 表现

- 场上实体使用本机 Minecraft Java JAR 的 `entity/illager/vindicator.png`，由 `DemoMinecraftModelFactory` 构造分件体素模型；提取资源继续位于 Git 忽略目录。
- 触发事件显示“建筑伏击”像素横幅并脉冲场上实体，不增加独立 Web 风格浮层。
- `-previewVindicator` 固定使用深暗主场，先部署幽匿感测体，再部署林地卫道士，以实机画面验证建筑条件、6/3 铭牌、阵营材质和模型皮肤。

## 验收

- 己方建筑或结构可触发 +2 攻击，敌方建筑不能触发。
- `CARD_DEPLOYED → OBJECT_STATS_CHANGED` 顺序稳定并通过共享事件 Schema。
- 回合结束后攻击从 6 精确恢复至 4，生命保持 3。
- 本地规则、权威规则、内容注册、模型纹理映射与 Windows Demo 预览均有自动化或可视证据。

# 改动日志

本文件按时间倒序记录影响视觉表现、资源管线或运行时架构的改动。

## 2026-10-05 源码提交整理

- 用户明确授权提交源码及所需文件：以完整当前工程源码快照纳入相互依赖的Unity/服务端/Schema/测试/资源提取与验证脚本、工程配置及文档；不是声称将历史混合改动拆成可独立编译的每个功能提交，也不是发布二进制。
- 新增根Temp忽略，保留本地证据/构建/素材与个人配置，不纳入仓库；过时的一次性scripts/add_units.py保留本地，不作为构建或运行依赖。MC生成资源仍服从DO_NOT_COMMIT_EXTRACTED_ASSETS。
- 提交前统一校验发现幼羊增量来源账本未被合并读取；只读校验器现在逐账本核对固定来源/策略并统一检查文件hash、登记、重复与完整性。合成主账本/增量正例及重复/错commit/缺一配对三负例通过，实际66实体资产跨两账本通过；没有改写原素材或账本。
- 统一validate.ps1 -WithDockerConfig通过，服务端245/245、typecheck/build、内容/资源/报告控制/Compose配置通过。Unity源码未改变，最近实际引擎705/705及两个698来源Player仍匹配。正常快进推送，不强推/改写历史，不重启服务。
- 暂存区导出干净源码并npm ci验证：初轮发现Git LF规范与两个生成器CRLF逐字比较冲突；修正为LF生成、检查只兼容换行而不忽略内容漂移，新增LF/CRLF只读及目录内容漂移负例。r2无本地素材/迁移脚本仍完整统一验证通过（23秒、服务端245），不是只验证脏工作目录。
- 本轮实际Unity再次705/705，XML source-submission-20261005-r1.xml SHA `0FFE1DC4C7FE82646A95574D49A80E0B2E012B3499D859EE6F27701FA2BEBD7C`；Unity验证使用本机提取素材，不冒称干净源码无需提取即可重现全部视觉测试。npm audit另报告ajv/fast-uri共2项moderate、0high/critical，记录后续依赖维护，不自动改变锁定版本或称完整安全审查。

## 2026-10-04 AI-086 全玩家操作接口与基础人机接入

- 新IPlayerOperations观察/动作提交、不可变玩家投影JSON、完整8命令payload、对局/版本门禁与ACK结果；DemoOnlineMatchSession复用原dispatcher/服务端校验。隐藏敌方手牌/私有选择，DTO和数组复制不影响实时状态，不增协议/RPC或新规则引擎。
- 可替换IMatchAgentPolicy/MatchAgentRunner、公开SetAgentPolicy与-aiPlayer生产入口；基础策略部署/准备度与嘲讽攻击/选择/回合，复杂法术规划仍后续。修复观察JSON显式null及缺失nullable字段的空对象问题，失败过程完整保留。
- 实际Unity **705/705**；双模式各698来源/74图包/20诊断边界与新增生产AI接口通过。两个独立账号的人机权威对局通过，None AI分别收到6/4 ACK（含建筑/蜜蜂部署），玩家攻击/终局、两张1920图已看。不是所有AI效果实战，也不声称AI自然退出或人工游玩。
- [接口文档](ai-player-operations.md) 含全部操作参数/策略替换/启动；[AI-086](task-packets/AI-086.md) 含命令、hash与限制。未重启/部署服务、未改数据管理状态/协议/资源，未提交或覆盖用户改动。

## 2026-10-04 UI-085 基础原型联合收口审查

- 逐原需求建立实现/证据/限制矩阵；保留UI-084生产代码，实际Unity引擎重新 **687/687**，双模式各690来源hash/74卡图/20诊断边界复验通过。
- 默认17350实际health版本40/prototype-0.65/42/48；当前Unity双端真实末端格UI部署和普通对局各一局、退出0/0，四张双方原尺寸图已看。两局A部署/攻击/结束回合/认输/恢复、B回合/同步，不冒称B部署或双方恢复。
- 两尺寸响应式只读手牌图鉴真实开关/翻页、长正文完整且状态不变；两图已看。紧凑手牌省略摘要不是丢失正文。服务未重启/部署，未改代码/资源/规则/数据库管理状态、未提交。
- [UI-085](task-packets/UI-085.md) 给出准确命令/hash/需求矩阵。UI优化与基础对局阶段收口，不宣称整个GDD、完整发行或未填效果完成；REL-043及新增内容为用户选择的后续任务，不自动扩张为完整MC渲染引擎。

## 2026-10-04 UI-084 独立原幼羊模型/皮肤

- 原Mojang当前client/controller选择独立baby geometry与32x32 TGA，旧baby大头动画已弃用；tk_003撤下成人缩小/羊毛叠层，换六原cube的大头短腿模型，保留羊专用alpha染色mask、项目待机/落地/.9可读高度。成人资源hash不变；源配置成对登记，OnlyKeys新目录提取及增量来源sidecar，不覆盖旧完整账本/原素材。
- Unity真实失败到 **687/687**；最新双模式r2各690来源hash/74卡图/20诊断边界通过。144图仅幼羊4张变、140完全一致，四原尺寸/双侧真实手牌部署和原格攻击、None普通成人羊场景与三负控制通过。
- 初轮测试编译错误、旧登记期望失败和错误成人羊攻击靶导致Player退出2均保留记录，不掩盖为绿色。详情/hash见 [UI-084](task-packets/UI-084.md)。下一 [UI-085](task-packets/UI-085.md) 按原需求联合收口审查；非完整Molang/PBR/权威联机重测。未改服务/协议/效果/数据库、未重启或提交，完整目标仍active。

## 2026-10-04 UI-083 六面box UV/镜像映射

- 参考固定Blockbench物理顶点/box UV及PrismarineJS，独立fixture复现U方向、mirror东西面region和底面V错误；仅修共用UV，保留几何/法线/ZYX/binding/原素材。24项新增四角/GPU用例，旧北面测试遗漏X反射的期望也明确纠正。
- Unity从667通过/18失败到 **685/685**；当前双模式r1各684来源/74卡图/20诊断隔离通过。144实图全部变化且全部原512尺寸查看，源geometry hash全部不变；guardian真实部署攻击/原格射线与None普通雪傀儡/流浪者战场、三负控制通过。
- 证据/hash与初轮审计路径错误见 [UI-083](task-packets/UI-083.md)。下一 [UI-084](task-packets/UI-084.md) 原幼羊资源；per-face对象/完整Molang及整体目标仍开放。未改服务/规则/协议/数据库、未重启或提交。

## 2026-10-04 UI-082 骨骼默认mirror继承

- 对照固定Blockbench现代/旧格式导入，ParseCube缺少mirror时改取所属bone默认，显式false/true优先，child bone不继承parent。仅修改解析数据；原MC资产、几何/法线/绕序、Builder现有UV算法不变。7项新增测试覆盖两格式矩阵/原guardian/非方atlas北面UV及几何不变。
- 失败656/5到全量Unity **661/661**；最新双模式r1各684来源/74卡图/20诊断隔离通过。144图94不变、50变化（13登记变体），50张原尺寸全部查看；guardian真实手牌部署/原格位攻击与None普通流浪者战场通过，3负控制通过。不是全部六面UV或权威联机验收。
- [UI-082](task-packets/UI-082.md) 记录来源/证据/hash；下一 [UI-083](task-packets/UI-083.md) 逐六面box UV与镜像映射，再独立原幼羊。未改规则/协议/服务/数据库，未重启/提交，完整目标仍开放。

## 2026-10-04 UI-081 烈焰人三层棒环位置轨道

- 原pivot覆盖不能展开网格，十二棒仍在中央重叠；按固定Mojangmove动画登记源position三层半径/角速度/相位与上下浮动。保留13原cube/pivot/UV、原材质和头待机，旧24条杆近似替换为12条显式源位置轨道；t=0展开再fit，运行时相对t=0采样不累积漂移。不是静态占位，也不是通用Molang/完整原版粒子或双材质实现。
- 独立位置失败fixture到Unity **654/654**；最新Development/None r1各684来源/74卡图/20诊断隔离通过。144图仅烈焰人4张变、140完全不变，四张原尺寸与真实双烈焰人部署/原地块攻击、None普通下界场景已看。3负控制通过；不冒称None烈焰人专项、回合按钮或权威联机。
- [UI-081](task-packets/UI-081.md) 记录原公式、clock边界、失败/成功hash；下一 [UI-082](task-packets/UI-082.md) 骨骼默认mirror/cube覆盖。原幼羊、六面UV/完整动画与总体外观仍待，未改源资源/效果/协议/服务/数据库、未重启/提交。

## 2026-10-04 UI-080 守卫者连接姿态与多轴组合顺序

- 按原Mojang默认动画数据登记守卫者十二刺、眼睛和三段尾连接，保留22原cube/源贴图/4待机；明确静态展开适配，不冒称完整刺伸缩/Molang/官方出生姿态。补定位后测试继续失败，对照Blockbench格式ZYX与预览契约修共用Rz*Ry*Rx，保留既有(-X,-Y,+Z)符号及mesh-only binding分离，避免单模型角度补丁。
- 记录red-r1与名为green-r2但实际失败、独立三轴red-r3；当前全量Unity **650/650**、最新Development/None r1各684来源/74卡图/20诊断隔离通过。144图128不变、16变更（守卫者/蜘蛛/海豚/蜜蜂）均原尺寸查看。真实guardian双侧姿态/手牌部署/原格位选择攻击，以及蜘蛛部署攻击/中毒和None普通海洋战场通过；不是回合按钮或权威联机重测。
- [UI-080](task-packets/UI-080.md) 记录来源、hash、静态边界和3负控制；下一 [UI-081](task-packets/UI-081.md) 烈焰人棒环。幼羊原资产、骨骼默认mirror/cube覆盖六面UV、完整动画和总体外观验收仍待。未改原MC资源/协议/规则/服务/数据库、未重跑服务器测试/重启服务/提交，大目标保持开放。

## 2026-10-04 UI-079 蜘蛛默认腿姿态及共用坐标转换

- 原默认八腿动画未被静态工厂应用，原摆腿3度不能展开姿态；明确登记源角度并保留八腿待机/朝向/地表落脚。对照固定Mojang/Blockbench并以独立解析式证明共用Y/Z符号错误，修骨骼及两类cube旋转，避免蜘蛛局部反号特例。20个原低alpha红眼复用已验证的发光近似，不改原MC资源。
- Unity失败634/8到当前646/646；最新双模式r1各684来源/74卡图/20诊断隔离通过。144图中123不变、21变更均逐图已看，含蜘蛛/鱼鳍/毛刺/蜂翼，龙仅1个RGBA像素变化。实际Spider手牌/原格位部署攻击/中毒和None普通敌方正面战场通过；夹具回合为本地规则调用，不冒称真实按钮或联机验证。
- [UI-079](task-packets/UI-079.md) 记录全部证据和一次夹具编译失败修正、3负控制；下一 [UI-080](task-packets/UI-080.md) 守卫者部件。烈焰人等仍待，原服务/数据库/规则未改、未重跑权威联机/提交，大目标开放。

## 2026-10-04 UI-078 羊毛源外层与颜色遮罩

- 复用原Bedrock专用羊毛cube/UV/inflate，共用原骨骼；撤下整只剪毛羊膨胀叠Java羊毛贴图的错误组合。保留绑定/落地/待机和独立atlas能力，精确overlay登记/缺失拒绝/图册元数据可追溯，不通用猜解继承。
- 首轮实图仍缺脸，查原RLE TGA与官方材质文档后修羊专用alpha染色遮罩：脸/裸露腿不再被透明裁剪，正常面向明暗保留，其他材质不变。Unity634/634含4项新增GPU实读；两模式r2各684来源/74卡图/20诊断边界通过，144图仅羊8张变、136张SHA一致。八图/None敌方普通战场与己方真实部署攻击已看，详见 [UI-078](task-packets/UI-078.md)。
- 首轮白脸图与mask编译失败保留，不作完成证据；tk_003仍旧缩小成人羊，不冒称完整官方幼羊。下一 [UI-079](task-packets/UI-079.md) 蜘蛛姿态，守卫者/烈焰人仍待。原资产/服务/数据库/规则未改、未重跑联机/提交，大目标开放。

## 2026-10-04 UI-077 末影人站姿与紫眼材质

- 对照固定Mojang原始文件和PrismarineJS，核查原TGA：六个紫眼像素alpha=3/255被统一cutoff剔除，黑块也含真实不透明源像素。仅末影人启用低alpha发光近似与明确静态头部/内层偏移，保留原资产、UV、骨骼层级、朝向/待机；不是完整Bedrock材质或Molang解释器。
- Unity621/621含4项真实GPU读取；Development/None r3各684来源匹配、74卡图/20诊断边界通过。当前144图仅末影人4图改变，其他140张SHA一致，四图及None普通战场实际查看，头部/紫眼恢复；真实部署/选择/回手通过。中间alpha r2胸口眼睛不作完成证据。
- 证据见 [UI-077](task-packets/UI-077.md)，下一 [UI-078](task-packets/UI-078.md) 羊毛裸露部件。原服务/数据库/规则未改，未重跑联机、未提交，大目标未关闭。

## 2026-10-04 UI-076 村民基础皮肤/沙漠服装

- 对照固定官方client entity及其实际v3渲染控制器，确认沙漠贴图仅为服装层。工厂登记基础皮肤，Entity材质在原网格UV上合成两张原纹理，不复制膨胀网格、不改素材alpha/卡名/规则；普通材质原路径保持。
- Unity611/611；当前Development/None r2各684来源匹配、74卡图包/20诊断边界通过。重建144图仅村民4图改变，其余140张SHA完全一致；四个村民视图头脸恢复、服装保留。
- 双村民战场/原格射线/待机通过且实际图已看。首轮考古弹窗遮挡不作外观证据，fixture通过既有合法选择结算后重拍。None普通场景/预览3负控制通过，详见 [UI-076](task-packets/UI-076.md)；下一 [UI-077](task-packets/UI-077.md) 末影人异常，其他模型与大目标未关闭。原资产/服务/数据库未改，未提交。

## 2026-10-04 UI-075 实际生物图册与 Shader 保留

- 新增当前 Unity Player 的36登记/双側/固定待机时刻图册，144实图与六页全部查看，带身份/geometry/贴图尺寸/相机/图 SHA，不能用占位块冒充成功。
- 真实Player首轮发现 Entity Shader被构建剥离、普通provider退回Standard；显式加入现有Shader的Always Included引用。打包回归旧设置608中仅该项失败，修复后608/608；新Development/None各684项来源匹配、74图包、20诊断类型/采样方法边界通过。
- 图册发现沙漠村民缺脸、末影人破碎透明、羊毛与部分部件仍异常；不宣称所有生物正确。优先续接 [UI-076](task-packets/UI-076.md) 村民分层，图册/失败/当前证据见 [UI-075](task-packets/UI-075.md)。MC原资产/服务端/数据库未改，未暂存/提交。

## 2026-10-04 UI-074 精确模型选择

- 修指定geometry缺失时静默回退、legacy忽略ID及现代重复/缺ID；明确拒绝未实现的派生请求。新增精确geometry元数据及只读登记快照，36登记全部核验。旧代码13项失败，最终Unity606/606；保留一次NUnit属性编译失败记录，未升级依赖。
- 当前Development/None两种682项来源匹配Player、74卡图包/None诊断隔离通过；双側海龟、羊真实战斗、None普通预览三图已看。详见 [UI-074](task-packets/UI-074.md)。旧UI-073 Player已stale；不宣称当前新源码重跑联机。
- 下一UI-075全36登记双側真实渲染图册，不以精确映射/非空mesh代替全部外观验收。原素材/服务器/数据库不改，没有暂存或提交。

## 2026-10-04 UI-073 通用源绑定姿态

- 对照固定PrismarineJS网格/绑定结构，将source bind_pose_rotation与普通rotation分离，源绑定自动烘焙到网格/叠层、子骨骼不继承；移除羊角度特例，海龟无需新增角度补丁。没有复制开源代码或引入依赖，原素材/服务器规则不改。
- 新4例在旧代码失败，最终Unity587/587；Development/None两种682项来源匹配Player、74卡图包/None边界通过。双側海龟两尺寸/北极熊双側/羊真实战斗四图已看，海龟原格射线/动画保留，预览3负控制拒绝。详见 [UI-073](task-packets/UI-073.md)。
- 下一UI-074严格geometry选择及随后全模型图册，当前不称全生物/全联机重验通过。无服务重启、暂存或提交。

## 2026-10-04 UI-072 绵羊姿态与羊毛修复

- 两绵羊body绑定姿态改为mesh-only，四腿不再继承90度；羊毛64x32独立UV，基础64x64保持；仅两绵羊启用膨胀网格落地。原MC素材/场地/规则不改。新4例原失败、首修发现落地偏差，最终Unity583/583。
- 新Development/None Player各682项来源匹配，74卡图包/None编译边界通过，己/敌/None三图已看；默认17350新源码真实UI双端final12/退出0/0/A恢复，两图已看。详见 [UI-072](task-packets/UI-072.md)，旧UI-070 Player已stale。
- 用户追加生物模型渲染开源对照；已读取PrismarineJS(MIT)/Blockbench(GPLv3)原始代码与许可，固定参考commit，尚未复制/安装第三方代码。通用binding语义、海龟、geometry选择及全模型图册继续UI-073，不宣称本轮所有生物已修复。

## 2026-10-04 QA-071 默认联机UI出牌门禁

- 当前682项Unity Player默认17350经真实UGUI选稳定手牌实例、3D射线点击第4单位格，双方部署/一次扣费/实例消费/模型落点及权威接受通过，final12、退出0/0；A攻击/认输/恢复、双方回合与投影一致。74卡图包检查通过，两终局截图已看，详见 [QA-071](task-packets/QA-071.md)。
- 截图发现绵羊模型外观异常，下一项UI-072只查该MC模型，不宣称本轮视觉全部通过。无源码/服务配置改动、无暂存或提交；总体目标继续开放。

## 2026-10-04 DEV-068 默认联机服务恢复

- 获用户明确许可，仅重启原 `biome-rivals-local-nakama-1`，未动数据库/其他服务/Docker/socket/卷。默认17350实际health RPC已与客户端匹配：40 / prototype-0.65 / 42 / 48。
- 当前682项Unity Player默认双端基础对局通过，退出0/0、final13；部署、攻击、英雄30→29、结束回合、认输、A断线恢复及双方投影一致，双方终局图已看。报告见 `Temp/CurrentUnityCli/dev068-default-positive-r1/validation.json`；本轮为正式命令驱动，非鼠标部署门禁。
- DEV-068关闭，旧版本/待许可记载为历史。仅更新任务文档，无游戏源码改动、无暂存/提交；完整发行和独立树未验收。

## 2026-10-04 UI-070 非开发版MC卡图/头像恢复

- 两种本机Player统一携带74张已注册侧车卡图；构建预检源记录/hash，包manifest和逐项校验拒绝缺图/篡改/额外图。Player仅读自身包目录，Editor才读源码，修复禁用开关被缓存绕过；不改图像/卡框/群系材质，不发布本机素材。
- Unity **579/579**，新两种 **682** 项来源匹配Player；74图片hash/编译18类型隔离/无源码回退通过，3包负控制通过。仓库外cwd实跑非开发版两组群系×双尺寸4图均恢复MC卡图、详情和头像；新开发版正常双端与默认负向回归通过，7张最终图已看。共享 **24.8秒 / 服务端245/245**。细节、首轮shell路径错误和最终证据见 [UI-070](task-packets/UI-070.md)。
- 审查服务已停止、卷保留；原默认服务仍旧协议且未重启，等待单独许可。当前工作区本项门禁通过不等于默认联机/独立树/完整发行或整体目标完成。

## 2026-10-04 QA-069 非 Development 编译隔离

- 新增明确非开发构建入口/实际构建模式字段及编译DLL边界检查。首轮真实检查发现四个探针及报告泄漏，补条件编译和回手入口拒绝；最终指定18开发类型非开发版全无、开发版全有，正式联网/卡牌/图鉴仍在，两个模式互换与stale Player负控制通过。
- Unity **563/563**，两种新 **680** 项来源匹配Player构建成功；新开发版默认预期失败和独立20350正常双端回归通过，非开发版本地战斗通过，四张实际图已看。共享 **22.5秒 / 服务端245/245**，证据和范围见 [QA-069](task-packets/QA-069.md)。
- 实际非开发版卡图/头像仍为占位图形，另拆 [UI-070](task-packets/UI-070.md) 处理本机构建素材一致性，未宣称可发行。默认服务未重启，整体目标/独立树/发行仍开放。

## 2026-10-04 DEV-068 兼容服务器成功路径回归

- 当前 680 项来源匹配 Player 在独立 20350、fixture=false 的普通双端对局通过：双方部署/结束回合、A 攻击造成英雄伤害/恢复/认输，final revision 11、退出 0/0。双方终局图已看，协议/Assets 无新修改。不是鼠标部署专项或默认 17350 修复；证据和范围见 [DEV-068](task-packets/DEV-068.md)。
- 审查服务 stop、卷保留，原四服务未动。安全下一切片拆为 [QA-069 非 Development 编译隔离](task-packets/QA-069.md)，尚未实施；默认服务重启仍待单独授权，完整目标/独立树/发行未完成。

## 2026-10-04 DEV-068 默认版本不兼容诊断

- 首连/重连失败增加结构化两端版本和目标地址诊断；顶栏改为中性的“版本不兼容 / 检查两端”，底部三行给出目标、差异和更新提示，日志保留完整版本。不降级、不改默认端口、不改变游戏权限。
- Unity 引擎 **563/563**，新 **680** 项来源匹配 Development Player；默认 17350 实际 **39/.64 对 40/.65** 预期失败审查退出 0，无 socket/匹配/match/权威状态，本地 revision 0→0，1920×1080 图已看。共享 **23.6 秒 / 服务端 245/245**。证据和边界见 [DEV-068](task-packets/DEV-068.md)。
- 默认成功双端对局/恢复尚未完成；未重启原 Nakama/PostgreSQL/Docker 或移动 socket。仍待仅原 Nakama 的明确重启授权，完整目标开放。

## 2026-10-04 UI-067 C 非 16:9 / resize 修复

- Canvas 改完整 Expand 适配，与 16:9 世界视口居中一致，修复 1424×714 查看手牌入口越界；保留原素材/作者坐标/PPU/Pixel Perfect 和游戏权限。新增七个契约/旧算法负控制、真实 Screen.SetResolution 循环审查。
- Unity **550/550**、服务端 **245/245**、共享 **19.7 秒**。新 **674** 项来源匹配 Player 两次五阶段 resize/实际图鉴点击/14 格射线及两种非 16:9 普通 Nakama 双端等待回归通过，六图已看；详见 [UI-067](task-packets/UI-067.md)。当前 UI-067 功能门禁通过，不等于 release/完整目标已完成。
- 收尾发现默认 localhost:17350 health 实际仍 **39 / prototype-0.64**，与当前 **40 / prototype-0.65** 不兼容；独立审查端口成功不能冒充默认启动成功。转 [DEV-068](task-packets/DEV-068.md)，已询问仅重启原 Nakama 的授权，未操作原容器/数据库/socket。

## 2026-10-04 UI-067 B 顶栏语义分行

- 保持石砖面板及列布局、固定 15px 文字，将“权威对局 / 已连接”等长状态明确按语义分行，移除 best-fit，解决孤立末字；账户/卡组/断开按钮和权限不变。新增实际 UGUI 字宽/行数/高度门禁。
- 修正新测试的编辑态账户依赖隔离及旧 best-fit 断言，新增 12 状态 + 2 负控制；Unity 最终 **543/543**。新 **670** 项来源匹配 Player 在 1280/1920 两个普通联机等待场景均退出 0/0，四张顶栏实际图已看、原等待/ACK 门禁保持通过。
- 证据见 [UI-067](task-packets/UI-067.md)。下一项 C 非 16:9/窗口 resize 安全边界，尚未修改，不关闭整体目标或独立树/发行。

## 2026-10-04 UI-067 A2 普通权威命令等待画面

- 新开发双端探针使用普通换牌/结束回合自然抽满七张，再双方各检查一次真实响应暂存期间的 UGUI 图鉴点击、长文、输入锁与截图；释放后同一 commandId ACK/revision +1，正常认输收尾。服务器精确 fixture=false，未注入牌局状态。
- Unity 最终 **529/529**；新 **666** 项来源匹配 Player 的 1280/1920 两局均退出 0/0、四图已看。等待报告 **2 正 / 27 负**纳入共享验证；失败首轮与实际窗口尺寸/raycast 诊断保留。证据见 [UI-067](task-packets/UI-067.md)。
- A2 当前画面门禁补齐，下一项 B 顶栏末字换行；新发现 C 非 16:9 查看入口越界仍待真实 resize 修复，不能把指定审查窗口尺寸称为产品修复。完整目标、独立树和发行继续开放。

## 2026-10-04 UI-067 A2 接收延迟审查工具

- 增加 Editor/Development 专用、显式作用域的原始网络消息 FIFO 暂存，命令仍正常发送；限制缓冲容量，处理断线/故障/换 match/销毁，保护新旧范围与重入消息顺序，不改变八秒命令超时、生产规则或协议。
- Unity 引擎 r2 **529/529**，撤回 generation 保护的隔离负控 **0/1**；新 Development Player 构建成功，**664** 项来源匹配。最终恢复后的全量结果见 [UI-067](task-packets/UI-067.md)。未把 FakeTransport 测试称为真实 Nakama 等待画面，A2 双分辨率实跑及 B 顶栏换行仍待，完整目标开放。

## 2026-10-04 UI-067 A2 权威满手平局

- 保留原平局探针，新增精确测试开关下的满手变体；服务端注入私有预条件、原 CD-006 消费/抽牌与终局规则结算，客户端只回放。两端重连前后均实际 UI 只读查看长文/关闭，并验证七张材质、透明度和输入锁。
- Unity 518/518、服务端 245/245、共享 18.8 秒；新 660 项来源匹配 Player 在 1280/1920 两局均退出 0/0，平局、事件次序、七实例和恢复一致；四张真图已看、hash 再验。详见 [UI-067](task-packets/UI-067.md)，不称作普通随机牌局达成的平局。
- 专项容器已停止、卷保留，原四服务健康，无活跃审查进程，未暂存/提交。真实联机等待、顶栏最后一字换行、独立树/发行及整体目标仍开放。

## 2026-10-04 UI-067 A2 本地状态画面与对手指针锁

- 补对手/胜/负满手受控预览，经正式结束回合、英雄攻击、正常疲劳结算建立状态，未强制修改终局标志。实际 UI 打开/翻页/关闭只读图鉴后，状态/实例不变且操作锁保留。
- 引擎红测发现关闭图鉴后对手回合指针被重启，补回合归属门禁；旧终局清理夹具改为先证明回合锁，再显式注入残留状态，保留清理断言。最终 Unity 518/518、服务端 244/244，共享 18.9 秒；660 项来源一致新 Player 六张双分辨率实际图已看并退出 0，详见 [UI-067](task-packets/UI-067.md)。
- 平局/真实联机等待画面、顶栏及独立提交树/发行仍开放；没有以本地画面关闭联机验收。原服务健康、无审查进程，未暂存/提交；隔离失败 Editor 退出挂起处理如实留档。

## 2026-10-04 UI-067 A3 只读手牌图鉴

- 添加独立材质化只读窗口，复用 CardUI 和稳定手牌实例/费用修正；翻页不改变出牌选中项、不发命令，关闭不解除终局或权威 pending 锁。实现拆出独立 partial，场内及关键操作回调有浏览防护。
- Unity 515/515（含四状态实例/全文/锁回归与原 pending Session 测试）；服务端 244/244，共享 18.8 秒。660 项来源一致新 Player 的两个分辨率均退出 0，实际 UI 点击和翻页、长文布局、状态无变化/场内输入锁通过，截图已看，证据见 [UI-067](task-packets/UI-067.md)。
- A2 对手回合/权威等待/胜负平局实际画面及 B 顶栏仍开放；未声明全部卡牌视觉或整个目标完成。原服务健康，审查进程已退出，未暂存/提交。

## 2026-10-04 UI-067 A1 手牌禁用透明度（当前工作区部分门禁通过）

- 手牌输入锁与信息透明度分离：父组 alpha=1，材质卡框禁用时保持不透明，仅轻微 RGB 反馈；输入条件/射线门禁、费用不足检查及规则未改。
- Unity 实际红测复现 .52/.42，最终 511/511；共享校验 21.6 秒，服务端 244/244。新来源匹配 Player 的正常/实际战斗满手、1280/1920 四图退出 0 并已查看，证据及 hash 见 [UI-067](task-packets/UI-067.md)。
- UI-067 仍实施中：其他状态实际画面、禁用时长说明只读入口、顶栏换行待处理，不以透明度修复关闭整个可读性任务。未暂存/提交，无活跃审查 Editor/Player，原服务健康。

## 2026-10-04 UI-066 北极熊绑定姿态与 idle 朝向

- 对照 Mojang 官方旧版相同几何的 body bind_pose_rotation，仅为北极熊补 90° 网格绑定姿态；不旋转骨骼子树、不改头/四腿/UV/纹理/源文件。Builder 增加可复用 mesh-only pose，Factory 单模型登记。
- 实图另发现场景 Floater 每帧覆盖初始 root 方向；改为捕获 BaseRotation 并叠加原 sway。新增几何/贴地/idle 与 0/180/35° 注册朝向红→绿回归，不关闭动画掩盖问题。
- Unity CLI / Editor 全量 505/505、服务端 244/244，共享 21.8 秒；658 项来源匹配 r4 Player 构建退出 0。1280/1920 两次实际 Player 均退出 0，两侧模型图已看，实际原格投影/Physics raycast 和运行时初始朝向门禁通过。证据和隔离 red Editor 退出挂起处理见 [UI-066](task-packets/UI-066.md)。
- UI-066 当前门禁完成，下一项 UI-067 禁用手牌信息/顶栏可读性。未暂存或提交，独立树/发行/整体目标仍开放；原服务健康，无活跃审查进程。

## 2026-10-03 QA-056 非法部署原子拒绝专项（当前工作区门禁通过）

- r10 最终 Unity 引擎 500/500、Windows 构建退出 0，658 项源码来源匹配；同 Player 的 3/4、5/2、默认非 fixture 4/3 各 15 次正式拒绝与普通建筑回归四局全部退出 0/0，45 次拒绝的完整查看者快照/支付/真实实例/无事件及恢复一致，八张真 Player 图已看。专项容器停止、原四容器健康、无活跃 Editor/Player，未暂存或提交。E2d 当前功能通过，下一项 UI-066，再 UI-067；独立树/发行/整体目标开放。本轮未发生准备攻击或材料使用，不将零事件声明成这些分支的运行时覆盖；详见 QA-056 最终证据。

- 续接 r8：单位占用前移和双端前缀 revision 汇合已实现，恢复普通 CD-003 消费；Unity 497/497，3/4、5/2 与普通建筑真实回归退出 0/0。默认 4/3 仍失败，正式手牌投影揭示正常骨头/圆石产物未被消费，满手烧掉关键牌后 revision 166 疲劳结束。新增正式 targeted PlayCard 的材料准备与三容量引擎回归，不改生产掉落/牌序/RNG。r9 的新夹具因 hand/handCards 不一致被状态校验拦截（497 通过、3 失败），已同步修正，r10 全量引擎/新构建进行中；不以部分绿色关闭 E2d。共享门禁最新 19.4 秒、服务端 244/244 通过。

- 最终本轮复核：r7 Unity 496/496、构建成功，但首局正常准备仍因 CD-007 满手爆牌/疲劳失败，脚本退出 1；没有完整矩阵成功证据，不关闭 E2d。下一步单独改审查时序：单位占用拒绝提前、双端单位前缀完成再放开普通随机亡语准备战斗，详见 QA-056 续接段。专项容器已停止、数据保留，原服务健康；无活跃 Player/Editor，index 为空。

- 增加现有建筑探针的可选非法部署审查：正式 Session/Nakama 命令、真实手牌 ID，逐条拒绝关联/无事件/完整查看者快照及强制重连一致，7/8 次双向占用/容量/结构边界拒绝；合法单位/建筑仍经实际 UI，后续命令须可接受。不注入卡牌、红石或牌序，不改生产规则/协议。
- 实跑发现恢复快照中断提示动画后合法 UI 等待锁死；补快照后的 banner sequence/alpha/resting pose 复位，保留游戏状态/选择。另修审查准备策略满单位排导致爆牌/疲劳，加入保护关键末格的普通准备战斗；占用测试使用结构撞单格与单格撞结构两方向，不要求第二份同名结构。各轮失败日志均保留，不作为成功。
- r4 的 3/4、5/2 与普通建筑回归通过、六图已看，默认局暴露手牌重建后的渲染注册时序；增加等待精确手牌 Graphic 可用，再执行真实 raycast。r5 实际苦力怕随机亡语杀死关键单位后，空格部署被服务器正确接受；排除危险准备单位，增加原实例占位前置检查并修正 cardId/instanceId 混用。生产规则/随机伤害未改。
- 当前 Unity CLI / Editor 6000.0.28f1c1 全量 496/496，服务端 244/244，共享校验 22.0 秒通过；报告 7 正/32 负控制。r7 Player 最终三容量真实矩阵及普通建筑回归仍待验证，尚不能关闭 E2d。范围、失败与证据见 [QA-056](task-packets/QA-056.md)；终局手牌/顶部状态可读性另拆 UI-067，在 UI-066 后推进。未暂存或提交，整体目标/发行/独立树仍开放。

## 2026-10-03 QA-055 山羊真实 UI 联机移动与双方恢复

- 新增普通雪原卡组山羊专项：精确手牌 UI、战吼动作按钮、3D 友军目标及地表部署，单次支付；双方验证部署/移动事件因果、稳定实例、原格清空、末格占用、目标属性/山羊临时攻击、公开战场 hash、模型位置、隐私与冻结强制重连。
- 修正诊断 UI 动作适配器固定查找法术 Cast 的问题，按卡牌类型查找 BattlecryTarget；按钮查找与真实 raycast 点击分层。新增六项引擎回归，EditMode 只声明按钮映射/监听器/3D 输入，完整 UGUI 射线与服务器接受由图形 Player 实跑。
- Unity CLI 全量 484/484、服务端 242/242；654 项来源匹配的 r3 Player 在 3/4/5 单位格及建筑回归四局退出 0/0，八张 1920 实图/哈希已复核。新报告 7 正/51 负控制进入共享门禁；失败准备命令、旧动作按钮和 EditMode 夹具尝试证据均保留，详见 [QA-055](task-packets/QA-055.md)。在线脚本增加提前非零退出处理，第二轮失败已验证只清理本次进程。
- ARENA-037E2c 当前通过，下一项仅 E2d 服务端非法部署原子拒绝；实图北极熊竖直姿态另拆 [UI-066](task-packets/UI-066.md)，终局手牌/顶部状态可读性仍有后续范围。整体 E/目标/独立提交树不关闭。审查容器停止、数据保留，原服务未重启；未暂存或提交。

## 2026-10-03 QA-054 建筑贴边、双端重连与 HUD 净空

- 新增正常洞穴卡组的建筑专项：真实手牌 UI/3D 指针部署末端 CD-004 与双格 CD-007，精确支付/实例消费、连续占格、稳定 ID、公开战场 hash、mesh 中心/跨度及双方冻结断线恢复均验证。不是离线图/捕获 gateway。
- 首轮实图发现四格建筑排过宽，最右模型被详情 HUD 遮挡。四格排外锚点改为与标准三格排一致，保留地表间隙；标准布局不改。新增两项红→绿布局/点击回归及模型八角投影不侵入右侧详情框门禁。
- 最终 Unity CLI EditMode 478/478、服务端 242/242；650 项来源匹配的 r2 Player 在 3/4、5/2 双建筑专项及标准单位 UI 回归均退出 0/0，六张 1920 实图已复核。报告 3 正/37 负控制已进入共享校验，证据见 [QA-054](task-packets/QA-054.md)。
- 另发现共享 build clean 与审查服务启动并行会导致 Nakama 健康却无游戏 RPC；失败日志保留。新增启动时 index.js 非空检查，真实 Docker 缺失/空/非空三例通过，审查部署必须在 build 完成后串行启动。原服务未重启，专项容器停止、数据保留。
- ARENA-037E2b 功能门禁通过，下一项 E2c 移动恢复、再 E2d 服务端非法命令原子拒绝；整体任务、独立提交树和发行仍未完成，未暂存或提交。

## 2026-10-03 QA-053 修复真实 UI 在线部署并验证末端单位格

- Unity 引擎红测发现真实点击/拖放把 REDSTONE 作为手牌实例 ID 发出，基础 Session 探针未覆盖此入口。Scene 在线部署改为具名参数，修复支付/目标/手牌字段错位；点击/拖放复用地表预览的山羊路径检查，禁用落点不会发送命令。
- 新增 22 项 Scene/Pointer 到捕获 gateway 的集成回归，全量 CLI EditMode 473/473；服务端 242/242，共享验证与 UI 报告 15 正/35 负控制通过。测试捕获 gateway 不等于服务器接受。
- 646 项来源匹配的新 Development Player，在默认 4/3、5/2、3/4 真实双客户端从 GraphicRaycaster 手牌到 3D 指针部署末端单位格；验证单次扣费、精确消费实例、模型落位、公开投影、A 重连与双方 1920 截图，三局退出 0/0。5/2 双方都实际部署第五格，六图已查看；证据见 [QA-053](task-packets/QA-053.md)。
- ARENA-037E2 拆为 a 末端单位格（本轮通过）、b 建筑贴边、c 移动恢复、d 服务端非法命令原子拒绝；后三项、独立提交树与整体目标不关闭。专项容器停止、数据保留，未重启原服务、未主动关闭主 GUI Editor；最终复核无活跃 Unity/Player 进程。未暂存或提交。

## 2026-10-03 QA-052 修复在线权威场地与实际 UI/3D 拓扑不同步

- 新真实 Scene 红测揭示在线 5/2、3/4 快照刷新时 IndexOutOfRangeException：Core 接受场地而旧地表/UI 仍为本地 4/3。新增显式权威场地重建，立即禁用旧 Collider，整组同步世界格/UI、释放旧材质/网格缓存、更新对象坐标；同布局重连保持实例，断开恢复本地。首轮图形检查发现层级重排遮挡状态，最终改为在原格 sibling 位置替换并断言其他 HUD 顺序不变。
- 服务端新增 exact-true 测试 gate 下的 BIOME_RIVALS_TEST_ARENA_ID，仅用于隔离模式配置，拒绝未知 ID、不接受客户端选场，根默认 false/空。普通线上动作不使用 opcode 255 注入。新增场地报告 7 正例/18 负例，纳入共享验证。
- Unity CLI / Editor 6000.0.28f1c1 最终 451/451 Passed，XML qa052-final3-editmode.xml SHA-256 4E18D5EDD5E1DB2FA0D6F5960CEB9C1AB3A618AEE87AA2E4BEC21361EBCECB85；r2 Windows Development Player 644 项来源匹配。共享验证 17.7 秒通过、服务端 242/242，模块 SHA-256 6003B576A0BDDE164F2CCC2B01840CC6626C1F75EB11AD373DD935F5D2F30FA4。
- 七个登记场地真实双 Player 基础矩阵全部通过、退出 0/0：普通部署/攻击/结束回合/投降、同终局、强制重连、全部格位射线/UI、双方 1920 图形输出。实际查看七场地代表图及关键 5/2、3/4 双方图；矩阵 Temp/CurrentUnityCli/qa052-arena-matrix-final/matrix.json SHA-256 8615FED374A57279C7DEE17018D33887DF4623004D931A428BC769D13E13F704。默认非夹具服务回归通过；期待值错配负控制两端退出 1/1，证明客户端期待参数不能改场地。
- E1 功能通过，但 E2 末端落位、移动、多格结构及非法索引仍待专项，不关闭 037E 或整体目标。只停止新 arena 测试项目两容器、保留 volume；原四容器健康、主 GUI Editor 保持运行、index 为空。完整证据/命令/下一切片见 [QA-052](task-packets/QA-052.md)。

## 2026-10-03 QA-051 双 Unity Player 在线平局 UI 与输入恢复验收

- 新 Development/Editor-only 平局诊断入口与独立探针，经真实 Nakama 夹具验证双方权威结果，实际检查 Canvas 金色“平局”、地表射线/指针与 UI/会话终局拒绝；双方强制重连后重复 UI/输入门禁，revision 保持 3。网关新增 8 项测试；报告校验 2 正例/26 负例加入共享验证。
- Unity CLI / Editor 6000.0.28f1c1 全量 449/449 Passed，XML Temp/CurrentUnityCli/qa051-final-editmode.xml，SHA-256 C0C39062E8791D34DDE68ACF40F71BAFCE9B3384F13683448B74E2412AB4E4E9；新 Windows Development Player 644 项源码逐项匹配。共享验证 17.1 秒通过、服务端 241/241；主 GUI Editor 保持打开。
- 实际双 Player 无图形诊断与图形渲染均退出 0/0；图形 Match 0252fec4-4e26-4aa1-bb8c-6360fb6c6719.biome-rivals，双方 FINISHED、生命 0、无赢家、同事件顺序/恢复 revision。在线双方 1920×1080 PNG 已查看，SHA-256 D4DE28F1E21DE68E2043A5C4EF830D827967176C171C0D7181372D986F1DEABE / 67C9D6A95D50628C0AA08EE351EEDC5CA444FE6A54C2C73B7952BE96FF7A4074。
- 基础对局部署/攻击/结束回合/投降/重连回归通过，Match f6858dcc-34ef-49a2-b286-6e00b3e8496d.biome-rivals，终局 11，退出 0/0。DB-007 离线当前 Player 双杀仍显示“胜利”，双方生命均 0；1920 图 SHA-256 D538F9E617C9A24AAAA76B496110ECE15112B8ADAEB5B355F6CC3A82F7200A78。
- RULE-035C 当前工作区功能验收补齐，不代表独立提交树或整个项目完成；本轮未更改生产规则/注册版本/美术。隔离 fixture 两容器已停止、volume 保留，原服务仍健康。Docker 已恢复，socket 授权未执行任何移动/删除。完整范围、命令和证据见 [QA-051](task-packets/QA-051.md)；待办仍按 REL-043、ARENA-037E 及后续规则/QA 任务推进。

## 2026-10-03 QA-050 平局实测夹具修复与真实消息 Unity 回放

- 首轮提交边界盘点确认 HEAD 协议 37 与工作区 40 的回手/平局/场地/内容预检共享多个文件；JSON 语义检查区分卡名仅格式、定义仅 ED-002/ED-005 + 版本依赖字段、独立 nt_008 文本。不能整文件混提；依据见 [REL-043](task-packets/REL-043.md)，index 保持为空。
- 新建 loopback 19350 的独立 fixture 服务，实际发现 OS 开关未映射到 Nakama ctx.env；Compose 补 runtime.env，默认仍 false。首次 JS 平局通过但 Unity 对同一真实 wire 的正式网关/仓库回放因手牌计数失败。夹具现在仅在规则结果接受后先发布脱敏前置快照，再发正式批次；不放宽客户端校验，不更改生产玩法。坏轨迹与服务端同步红测保留。
- 新 Editor-only wire 审查及包装脚本验证捕获原始消息、源 SHA、双方前置状态、一次终局及权威无赢家视图。最终 Match 055e9db2-e993-4f1d-a162-6e2db93296fa.biome-rivals，平局/重连 revision 3；Unity 审查两人共 11 条消息通过，旧坏轨迹仍被拒绝。模块 SHA-256 18D1C45DDB427A1BEE2EA2AFD840458E7FD482CCE8106E49B301E477D44F6997，与容器一致。
- Unity CLI / Editor 6000.0.28f1c1 全量 441/441 Passed，XML Temp/CurrentUnityCli/qa050-final-full-editmode.xml，SHA-256 0FF4CFB0CDFE52E6C55C79E2FC26F2237E6E9B5A9195B29F2B985D2EF0DAFF1E，主/隔离工程 640 项来源匹配。共享验证 17.2 秒通过，服务端 241/241、类型/构建/内容/Compose 门禁通过；主 GUI Editor 保持运行。
- 已停止本轮新建的两项 fixture 容器，保留数据 volume；原两套服务未重启。尚未完成双 Unity Player 在线平局 UI/输入锁定/恢复，因此 RULE-035C 与整体目标均不关闭，下一步先重建 Player，再独立完成该 UI 专项。详细边界与命令见 [QA-050](task-packets/QA-050.md)。

## 2026-10-03 QA-049 真实 Unity 双端回手与 UI/3D 交互验收

- 新增独立在线回手探针，复用正式网络会话与权威仓库；ED-002/ED-005 出牌实际通过精确手牌按钮、详情 Cast 和 3D 战场指针，验证支付、实例折扣、事件因果、私密投影、格位清空、双方强制重连、到期一次及终局收敛。报告正/负控制纳入共享验证入口；没有更改游戏规则/协议/注册或服务端夹具。
- 最终 Unity CLI / Editor 6000.0.28f1c1 全量 EditMode 441/441 Passed，退出 0，XML Temp/CurrentUnityCli/qa049-return-probe-final-editmode.xml，SHA-256 AF385124E1537531CE013C0063A1606B660BD5089939354B5512913786D0F957。新 Windows Player 构建成功，638 项源码逐项匹配。
- 真实双 Unity Player 连接隔离 18350 服务，fixture=false：ED-002 Match e82b9d14-5f72-41ad-afd7-724e54e81aef.biome-rivals，revision 10/恢复10→到期12→终局13，1费-1截零；ED-005 Match ea1dd197-f65e-42db-aaba-b782539b80aa.biome-rivals，18/恢复18→20→21，3费-2为1。两场双方退出均 0/0。普通基础对局/重连回归也通过，Match 7ae25923-8653-4119-b67d-fb2ab9c48fac.biome-rivals，终局11。
- 同源码离线图形 Player 1920×1080 回手图实际复核，SHA-256 F041C289AEB19EF755E3119991913410DCEA75CC12317539AF793B3750708B5E；不将离线图冒充在线图。首次探针误用未定义目标字段的失败证据保留，已按实际协议来源关联修正。完整命令、报告哈希及边界见 [QA-049](task-packets/QA-049.md)。
- Unity 在线回手专项缺口已补齐；C 仍待共享文件审查/提交分组，不提前启动 033D，不关闭其他在线门禁或整体目标。主 GUI Editor 保持打开，原 Docker 服务与数据未重启/改动，未暂存或提交。
- scripts/validate.ps1 -WithDockerConfig 实跑 18.1 秒全绿：内容/资源来源、报告控制、TypeScript、服务端 240/240、构建与 Compose 配置；Unity 441/441 来自单独指定引擎的全量运行。相关脚本语法、目标 diff --check 和最后 638 项来源复核均通过。

## 2026-10-03 QA-048 修复 Unity 网络 null 并验证真实双 Player

- 真实双 Unity Player 暴露 JsonUtility 将显式 null 手牌占位/可选引用实例化的问题，导致两端初始快照被 StateStore 拒绝。新增红测先复现 2/2 失败；新 MatchWireJson 按实际 JSON 结构恢复明确引用 null，保留泄露拒绝与原 DTO 默认行为，统一三个收件 opcode，限制尺寸/嵌套并移除 pendingChoice 字符串特例。没有更改服务端协议、数值或内容注册。
- Unity CLI / Editor 6000.0.28f1c1 全量 EditMode 432/432 Passed，0 failed/skipped，退出 0；XML Temp/CurrentUnityCli/qa048-wire-null-full.xml，SHA-256 504D53E48474E65CE22E5E9BA95BD4CA2EE3733D8FC7ECE9B55BDE3279726E00。同一源码 Windows Development Player 构建成功，634 项来源匹配。
- 在线与截图脚本共用来源检查；在线入口新增隔离 endpoint/输出目录，拒绝覆写旧报告，检查两端实际退出与公共状态一致，并精确恢复进程环境。真实双 Unity Player 在 18350 完成独立认证、匹配、调度、部署、攻击、结束回合、投降和强制重连，两端同 FINISHED revision 12，退出 0/0，外层环境检查通过。初始失败和中间环境检查失败产物均保留。
- 同一 Player 的离线回手 UI/3D 输入预览 1280×720 校验并目视通过，图 SHA-256 DFA1EA9DCB3939FE19EBC5ECE4B8B76CC79CA21ECF5FDBF2FCFCBE4631536346。网络基础动作不冒充 Unity 在线回手或在线视觉验收；C 的回手专项 Player 验证另行补齐，未提交/暂存混合工作区。完整命令、报告哈希和范围见 [QA-048](task-packets/QA-048.md)。

## 2026-10-03 QA-047 Docker 恢复与即时回手双客户端验收

- Docker Engine 外部恢复后，首轮探针发现旧 Nakama 仍加载协议 39 / `prototype-0.64`，按断言退出 1；没有降低版本要求。重新构建当前服务端，另启独立 Compose 项目、数据库卷和 18350 端口，未重启或覆盖原服务，也未删除/重置 socket。容器模块与宿主构建哈希一致。
- 独立服务上真实 Nakama JavaScript 双客户端分别完成 ED-002/ED-005 回手、精确 -1/-2 折扣、双方事件/修订收敛、私密投影、双方重连及到期清零，均第 1 次尝试通过；报告 `Temp/CurrentUnityCli/rule033c-online-isolated-20261003.json`，SHA-256 `60B6356BD38A10A6DC4ABD8A03C9C17DCB40F4FBCC00DD7181F84EE4BBE6E0B7`。这是网络客户端 smoke，不是两个 Unity Player 的画面验收。
- 服务端 typecheck、构建及 **240/240** 测试通过；再次实际调用 Unity 引擎全量 EditMode **423/423 Passed**，0 failed/skipped，退出 0，632 项源码一致。XML `Temp/CurrentUnityCli/rule033c-online-resumed-20261003-editmode.xml`，SHA-256 `D2B2C71426BE14FCA54FCD3B3B01555E11BDF0204009C4ACC3C4FA5A009460CC`。RULE-033C 本节在线门禁已通过，提交分组/收口待推进；ARENA-037E、RULE-035C 未一并关闭。命令、Match ID 和 revision 见 [`QA-047`](task-packets/QA-047.md)，此进展取代 QA-046 的当前环境阻塞结论；整体目标未完成。

## 2026-10-03 QA-046 联机恢复尝试与阻塞审计

- Docker 正常 CLI 启动超时并退出 1；最新日志确认 `sailor-ingest.sock` 重命名失败导致后端崩溃。用户仅授权备份/移动该条目，PowerShell 同目录备份重命名也因“系统无法访问此文件”失败；已停止，原条目仍在、没有产生备份，没有删除或在失败后重启。日志另有 GUI 恢复出厂设置动作，代理未调用，操作者与数据影响无法确认。
- 当前源码 632 项与隔离工程逐项哈希匹配，再次实际调用 Unity CLI / Editor `6000.0.28f1c1`，全量 EditMode **423/423 Passed**（0 failed / 0 skipped），CLI 退出 0；XML `Temp/CurrentUnityCli/goal-blocked-audit-20261003-editmode.xml`，SHA-256 `9F5172870B9C350F5B7696C00EC733B0B6EB59D2882890BFBB49FA8BB78E1DDB`。主 GUI Editor 保持运行。本轮没有新增 Player 或在线证据。
- 核对依赖后，RULE-033C、ARENA-037E、RULE-035C 在线门禁仍未完成；后续悬置/奖励切片不可提前启动，交易规则仍需用户确认。整体目标未完成，等待 Docker 恢复或新的明确方向；详细恢复入口见 [`QA-046`](task-packets/QA-046.md)。未提交、暂存或改动游戏代码。

## 2026-10-03 UI-065 终局场景提示与交互清理

- 将 12 种效果就绪判断集中到 `ResolveSlotEngineReadyKind`，终局/缺失/死亡来源统一不再亮起；海底神殿结束阶段威胁与效果铭牌的待触发/监听提示也停止。终局清除卡牌、手牌副本、攻击者、部署/法术目标选择，关闭场景指针并清理全部格位（含多格范围）的悬停、按下、拒绝与优先目标状态。被动状态、最终棋子/生命及有限时长事件反馈仍保留。
- 新增只读场景诊断 `HasActiveGameplayHighlights` 与 `-previewTerminalWorld`：合法部署珊瑚丛、装备三叉戟、进入战斗并执行致死英雄攻击；反应建筑仍留场，但终局场景不得保留 gameplay highlights，指针必须禁用。旧 `PreviewMatchOutcome` 复用同一英雄致死路径，原日志/阵营约定不变；正式经济与服务端协议不变。
- Unity CLI `1.0.0-beta.8` / Editor `6000.0.28f1c1` 隔离全量 EditMode **423/423 Passed**（0 failed / 0 skipped），新增 15 项回归，13 个参数化场景各覆盖双方真实相机射线、ACTIVE→FINISHED、多格输入/选择/就绪清理，另验被动状态保留与合法致死预览。XML `Temp/CurrentUnityCli/ui065-terminal-world-editmode-final.xml`，SHA-256 `4F1A4F6FF843B9CDCA69D0214CCDCB3DB85C84CBEDEAAA926FB93A505F2A8ABE`。同一源码 Windows Development Player 构建成功，632 项来源与主工程逐项匹配；实际运行日志、退出码、画幅与截图校验通过，且已目视复核：1280×720 SHA-256 `328AC5A9297C33CD1ACAB5B7DB1C295B2410F8F4B2A381E4F1790FB9E5BED8F3`，1920×1080 SHA-256 `AA9EAB3C39E0862A48B73C5AF5A17ECE42D2408694FFAB6F44E5422EA6A85FEF`（`Temp/CurrentUnityCli/ui065-terminal-world-player/`）。主 GUI Editor 保持打开。任务入口：[`UI-065`](task-packets/UI-065.md)。
- Docker/Nakama 在线门禁不变：本轮只读检查仍缺少 `dockerDesktopLinuxEngine` pipe，RULE-033C 与 ARENA-037E 在线双客户端/重连验收未执行，不用快照/离线 Player 代替。

## 2026-10-02 UI-064 双方建筑召唤就绪与铭牌可读性

- 府邸与要塞的铭牌/3D 地表统一使用只读公开状态：拥有者回合、当前动态单位排空位、要塞可用总红石（含临时池）、终局。修复敌方要塞永不就绪，以及非拥有者回合的府邸误报满场；状态分为等待回合、满场、缺红石、已结束和当前就绪。展示不改经济或提前召唤，最终效果仍由规则引擎的结束阶段顺序决定。
- 实际 Player 初审发现长句铭牌被缩小，改为名称/生命与状态两行、18 画布单位字号（720p 约 12px），底板只向上扩展以保留手牌净空。确定性 `-previewSummonReadiness` 合法部署府邸、结束回合生成卫道士新兵，再展示敌方要塞就绪；右侧详情绑定真实剩余蜜蜂手牌，避免未选牌时误显空手。
- Unity CLI `1.0.0-beta.8` / Editor `6000.0.28f1c1` 隔离源码全量 EditMode **408/408 Passed**（0 failed / 0 skipped），新增 23 项权威快照/地表、真实本地回合和预览回归；XML `Temp/CurrentUnityCli/ui064-summon-readiness-readable-editmode.xml`，SHA-256 `4D0DB969E80D5C2859E9199AB28CEA7A5C218621A3C0DB7374726FD1A8137902`。Windows Development Player 构建成功，632 项来源与当前主工程逐项匹配；两种画幅均通过结算日志、退出码、来源清单和尺寸检查，并已目视复核：1280×720 SHA-256 `9770A4A10D356D5657ACAD62D9D7ECCA3A108445B7E6C4E66BDD93F9D7E48073`，1920×1080 SHA-256 `97DFE2832C038891DBD13095575E26C501D538E53CB97719F377854CAFCAB5CF`（`Temp/CurrentUnityCli/ui064-summon-readiness-player-readable/`）。主 GUI Editor 保持打开。任务入口：[`UI-064`](task-packets/UI-064.md)。
- 更正 ARENA-037F 历史描述：四格空位遗漏只影响客户端就绪/预览，不阻塞规则引擎自动召唤；原先“跳过召唤效果”的表述已修正。Docker Engine 当前仍因缺少 `dockerDesktopLinuxEngine` pipe 不可达，RULE-033C/ARENA-037E 在线验收未执行，不把权威快照测试或离线 Player 作为双端在线证据。

## 2026-10-02 ARENA-037E 联机 Smoke 版本对齐（运行验收待 Docker）

- 核查发现四个 Nakama 双客户端 smoke 脚本仍使用已过期的协议 38 / `prototype-0.63`，而当前 Unity/服务端协议已升至 40 / `prototype-0.65`。已更新基础、末影回手、下界触发与下界状态召唤 smoke 的版本断言；基础 smoke 额外锁定双方快照共享 `standard_meadow` 且各自为 4/3，并在报告中输出场地 ID 与格数。
- 四个脚本通过 `node --check`，`server-nakama` typecheck 与规则/协议测试 240/240 通过。**没有运行 Docker/Nakama smoke**：本机 Docker Desktop Engine 服务为停止状态，两个 CLI context 的 Engine pipe 均不可访问，当前会话无权限启动服务。ARENA-037E 在线部署/重连验收保持未完成，不能用静态检查替代实机双端验证。

## 2026-10-02 ARENA-037D 2.5D 战场格位与 UI 按场地布局生成

- 3D 战场不再假设固定的 4 单位格/3 建筑格。构建前从本局 `arenaId` 取得同一份场地定义，双侧单位排/建筑排使用对称居中坐标，并据对应容量创建地表格片、碰撞/射线目标、悬停反馈与 Canvas UI 格。`deep_caverns` 可生成 5/2，`nether_lava_sea`/`end_void` 可生成 3/4；标准原野坐标保持原布局。构建后拒绝更改格位拓扑，避免残留交互 Collider。
- Development Player 支持 `-previewArena <arenaId>`。`scripts/capture-demo-preview.ps1 -PreviewArena` 可对注册场地逐一截图，校验构建源码清单、Player 退出码、日志与 PNG 尺寸，并在 JSON 报告登记 `arenaId`。
- Unity CLI `1.0.0-beta.8` / Editor `6000.0.28f1c1` 隔离副本 EditMode **383/383 Passed**（0 failed / 0 skipped），XML SHA-256 `AD607014618BAE5A6B523B9372EA69A922FC134407CC40E1070AF792691D4816`：`Temp/CurrentUnityCli/arena037d-editmode.xml`。新增七场地参数化测试锁定实际场景格、UI 格数量、对称坐标、所有玩家/敌方格位世界空间射线及构建后布局锁定。
- 同一 Unity CLI 构建 Windows Development Player（清单 632 项），七种场地均实跑并通过截图校验。SHA-256：`standard_meadow` `D5B249952469EFC089B9BFBECA81565BC3D4546884C6145FC389A53480365FDD`、`plains_sunrise` `F402F1951AEC8B11BD6A003D987E4ED9E479825D58416209E2BA4DBAE4E3F3C1`、`deep_caverns` `6B31BE6CE43EAC79F96A360C6A6CD7AB0716D1F86E463B76EF814F3ED06D067F`、`nether_lava_sea` `71DE58FAADDD2E35506C99766B59876F221D723CAF60848BDCE06DC6DE6906F4`、`end_void` `3571C5FC50728B293663FC3F39FE3AC0DF6E52BD95FF589E84BA821F9D04E592`、`deep_ocean` `4A211830DC06214619A68CEDB84FA154E6B35A795D9D33A0E5E68F94C6C3F413`、`desert_storm` `247512049FB1B5C31F0E85A3787324C9357AF5F660201DD8C7D7C02DEBC15CFB`。文件均位于 `Temp/CurrentUnityCli/arena037d-player-final/`。主图形 Editor 保持运行，CLI 测试/构建/实跑均使用隔离副本。ARENA-037E 的 Docker/Nakama 双客户端验收仍未完成。
- 额外 1280×720 Player 审查发现结构拖放预览仍继承正式首回合 1/1 红石，无法支付 5 费沙漠神殿；现由该场景明确准备等于注册卡牌费用的 5/5，仅改变验收夹具、不改正式经济。截图验收脚本同步将预期结算能量修正为 0。修复后 Player 在 `deep_caverns` 5/2 布局通过完整 `PreviewCombatInteraction`，在 `nether_lava_sea` 3/4 布局通过真实 UI 拖放、3D 射线与两格结构部署；两项 1280×720 均由清单、运行日志、退出码与画幅检查通过。SHA-256：5/2 战斗 `BF42B53FE49FB8D3CAD7F1E5A49C0BBC5EEB25CF50745461B2901C346E9F3883`、3/4 结构 `511E052989CB3E33F0571E86C2DD1B07C1A08BED9DE8561CB5745926C8DD9CA5`（`Temp/CurrentUnityCli/arena037e-player-final/`）。当前 Unity 全量 EditMode **383/383 Passed**，XML `Temp/CurrentUnityCli/arena037e-structure-preview-editmode.xml`，SHA-256 `13DFCBD4FBC363837677D451A14EF7C8F5D505D8630116B2671800B23F63EDB6`。
- 追加跨场地规则链验证：Player 在 `deep_caverns` 5/2 布局完成末影人部署、紫颂果回手与 `-1` 费用修正（1280×720，SHA-256 `AAB1A537DD855A1A215CCACE8D24BE285E43FAB189A5A31AC3C448D4ADE911DF`）；在 `nether_lava_sea` 3/4 布局通过 GraphicRaycaster 选合成付款、3D 指针部署两格 10/10 神殿、材料去向检查并保持正式初始红石 1/1（1280×720，SHA-256 `9F4DD4031725E4B5632739A8C1D32A09B9806CD8F56C8C0A649F1F38FFD97303`）。两项 Player 运行日志均明确记录流程结算成功，截图/构建清单/画幅脚本校验通过。
- 再次用 Unity CLI 在隔离副本运行全量 EditMode **383/383 Passed**（0 failed / 0 skipped），NUnit XML `Temp/CurrentUnityCli/arena037e-ui-gameplay-editmode.xml`，SHA-256 `05CC199708B82CE528747BA3BFBEF1443BBC0DF3C7EED22E9DBED3A87870DD23`。真实 Player 复核发现终局预览没有给 3 费三叉戟准备资源，导致预览停在未知结局；现只在终局验收夹具按注册卡费设为 3/3，正式首回合资源不变。随后 Unity CLI 成功构建 632 项源码 Development Player（Unity `6000.0.28f1c1`），Ocean vs Desert 实际攻击并结算胜利：我方 30、对手 0、操作锁定；1280×720 截图 SHA-256 `A868C58FCF961E270BD3FF3FB94C126A069FAB3ED79602B4D0E410F61FD30AA7`，Player SHA-256 `C2F154B2BA4763E348D3BDEF0DDF8C4971A58A2EF4DE28942BDB329D715D6BC0`（`Temp/CurrentUnityCli/arena037e-ui-gameplay-player/`）。CLI 用法核实：`unity test <project> --mode EditMode --output <report.xml> --editor-path <Unity.exe> --timeout 900 -- -nographics`；Player 使用 `unity build <project> --target StandaloneWindows64 --execute-method BiomeRivals.Demo.Editor.DemoBuildAutomation.BuildWindowsFromCommandLine --output-path <player.exe> --editor-path <Unity.exe> --timeout 900 --allow-dirty-build`。GUI 主 Editor PID 484 保持运行，CLI 只操作隔离副本。
- 可变格位复查发现两处遗留 4 格假设：回手地表脉冲只在前四格中找空位；UI 空位查询也会让 5 单位格洞穴在前四格满、第五格空时误报满场，错误熄灭建筑召唤就绪提示。两处现在统一使用当前场地容量，并正确按多格占用、死亡对象和无效容量计算。回归含空位算法边界与 `DemoSceneController` 实际 5 格容量检查（前四格通过合法部署占满、第五格可部署后再确认为满场）：Unity CLI 全量 EditMode **385/385 Passed**（0 failed / 0 skipped），XML `Temp/CurrentUnityCli/arena037f-unit-slot-availability-editmode-final.xml`，SHA-256 `6DA4B2DFCE54E42DE56038968B9DA4214F8BFEB09813B8C0521A67FECAA3B7A5`；同一隔离源码构建 632 项 Windows Development Player，并在 `deep_caverns` 5/2 布局实跑地表回手脉冲，1280×720 截图脚本校验通过，SHA-256 `05F8DACB934E646E414681ADE8D5BC4B5A676C160E1DC20AFFD46CD6BD0E26D5`（`Temp/CurrentUnityCli/arena037f-unit-slot-player/`）。主 GUI Editor 仍保持打开。调用点复核更正：规则引擎的自动召唤已使用动态容量，原记录“跳过召唤效果”不成立。
- 扩展 `-previewWoodlandRally` 作为真实 5 格对局链的确定性验收入口：按当前布局用注册单位逐格合法部署，仅留中央空位，再结算 `pf_007`；5 格洞穴实跑确认 `tk_004` 进入第 3 格，第二次召唤因满场改为抽取 `pf_001`。截图审查还发现演示原先停留在已打出的法术详情，现结算后改选实际抽到的手牌副本，并断言详情不再是过期手牌提示。全量 Unity CLI EditMode **385/385 Passed**，NUnit XML `Temp/CurrentUnityCli/arena037g-woodland-rally-editmode-final.xml`，SHA-256 `6FB6C05F9CF45AEB69D09A90AFF0AFEB53BD17B6E97C50513EF88512E2FC8682`。隔离副本由 Unity CLI 构建 632 项来源清单的 Windows Development Player；两种画幅均通过截图脚本的来源清单、结算日志、退出码和尺寸检查，并已人工查看：1280×720 SHA-256 `9A881AE9A4D9096A5433F96A492F1123081C0ED5F39D2D42B285E520F419496E`、1920×1080 SHA-256 `A010B861D4FDA34677850D21429E71ED61F5072130764FB0F7FB032871F1BADF`；产物位于 `Temp/CurrentUnityCli/arena037g-woodland-rally-player-verified/`。该本地 Player 证据不替代服务端/Nakama 双客户端验收。

## 2026-10-02 ARENA-037C Unity 核心与离线规则支持可变格位

- `DemoLocalMatch` 按登记 arena 创建双方独立的单位/建筑格数组并拒绝未知场地；权威离线视图暴露 arena ID，规则投影沿用快照中的真实布局。规则入口依数组长度处理部署、越位移动/邻接、自动召唤、结构连续占位、满排拒绝与死亡清格；标准原野仍为当前场景默认值。
- **验证**：Unity CLI 全量 EditMode **376/376 Passed**，报告 `Temp/CurrentUnityCli/arena037c-editmode.xml`，SHA-256 `45362AE1D12293A4533F824CFBF447707CAC1932ABB0C12A2A81356E0BB2B98A`。Windows Development Player 由 Unity CLI 在隔离副本构建；`PreviewFullHand` 1920×1080 截图经来源清单/运行日志/退出码/PNG 画幅验证，SHA-256 `76587A4A1F38DA0D265105C44BB8228965B7D8314F11101E4F73A9F18B1B5DC8`，实图确认现有卡框、石砖容器与满手布局未受本次规则改动破坏。037D 仍需实现真实可变 2.5D 格位与逐布局 Player 验收。

## 2026-10-02 ARENA-037B 权威状态与协议承载场地布局

- 服务端集中登记 GDD 七种场地布局，match init 从模式配置固定缺省 `standard_meadow`，双方以同一 `arenaId` 初始化对应单位/建筑格数组；权威状态不变量校验场地 ID 及双方长度，快照/事件批次保持场地身份，防止跨场地回放。协议快照 Schema 对七种具体长度逐项约束，命令与事件槽位索引上限容纳最多 5 个单位格。
- Unity 协议版本升至 40 / `prototype-0.65`；新增场地布局注册表，快照接收校验场地和双方数组长度，事件批次不得切换当前场地。此阶段不声称离线 Demo 已按可变布局部署/移动，也不声称 3D 场景、Docker/Nakama 双客户端已完成。
- **验证**：服务端 TypeScript 类型检查通过、测试 240/240；Unity CLI `1.0.0-beta.8` + Editor `6000.0.28f1c1` 在与当前 Unity 源文件哈希一致的隔离副本全量 EditMode 363/363 Passed，报告 `Temp/CurrentUnityCli/arena037b-editmode.xml`，SHA-256 `790DAF55646FA23F81D3FD95619622B48E784EB42153C4F27C3F9105B3F9A601`。主工程 GUI Editor 保持打开，未与 CLI 测试并发写入同一工程。

## 2026-10-02 UI-063 欠费手牌降低压暗幅度

- 1280×720 欠费卡实图显示整张卡的 CanvasGroup 透明度 0.62 会同时洗淡卡图、规则字和费用槽。保留整体压暗作为支付能力提示，将透明度提高到 0.8；可交互、可射线命中、可选中查看和实际支付判定均不变。
- 更新 EditMode 回归，锁定欠费透明度 0.8 并继续验证射线/交互保留、恢复可支付时透明度回到 1、再次欠费可以正常压暗。Unity CLI `1.0.0-beta.8` / Editor `6000.0.28f1c1` 隔离工程全量 EditMode **361/361 Passed**（0 failed / 0 skipped），NUnit XML SHA-256 `BDF45ABD9E9BB49BF151A744F3CDD673CA020E6EE987A7671F6769CF7D14C17B`：`Temp/CurrentUnityCli/ui063-editmode.xml`。
- 隔离副本 Windows Development Player 构建清单含 **630** 项源码，欠费羊群卡经真实 UI 点击打开详情；脚本核对 Player 退出码、运行日志、源码清单和画幅，目视确认 0.8 保留欠费辨识度且卡图/文字更清楚。1280×720 SHA-256 `3F51BE4DC9AB3E56FB79845065F9008A7A7E6138D9451393733454801AE3D5B2` [截图](../../Temp/CurrentUnityCli/ui063-unaffordable-card/unaffordable-1280.png)；1920×1080 SHA-256 `5ED470AA22E6C3C64A967E48F2A71F6E9C7B4F9288986E1594A8F57084C5DF7C` [截图](../../Temp/CurrentUnityCli/ui063-unaffordable-card/unaffordable-1920.png)。

## 2026-10-02 ARENA-037A 场地 ID 与布局来源冻结

- 结合 GDD v0.5 §3.1、§5.1、§14.1–14.2 和用户确认的“不同地形布局可以不同”，冻结 `arenaId` 来自本局模式配置；当前唯一原型 PvP 模式默认 `standard_meadow`。七种 GDD 场地 ID/格位配比列入 ARENA-037 契约，双方始终共享同一对称布局；不按任一玩家阵营推导、不随机分配、不顺带启用未定义的场地环境效果。
- 本阶段仅修改设计任务包和路线图，运行时仍为 4/3；037B 服务端/Schema、037C Unity 核心及离线规则、037D 2.5D 格位和 037E 在线验收仍未完成。验证为文档与 GDD 条款逐项核对，尚无运行时代码验收。

## 2026-10-02 UI-062 战斗操作提示保持最小屏幕字号

- 1280×720 Player 实图中，战斗指令正文随根 Canvas 缩放到约 10 屏幕像素，明显小于卡牌规则文字。现在只对右侧战斗操作说明设置 **12 px 屏幕最小字号**；参考分辨率仍为原字号 15，Canvas 放大时不额外变大。公共 `DemoUiMetrics.GetScreenReadableFontSize` 按实时 Canvas `scaleFactor` 向上取整，并有零/极小缩放保护。
- Unity EditMode 覆盖参考尺寸 1.0、1280×720 对应的 2/3 与更小的 0.5 缩放，分别验证 15/18/24 个设计单位；全量 **359/359 Passed**（0 failed / 0 skipped），报告 SHA-256 `29EC5C14657E6335D02836D12860392FD9E2B5558ECFEB0160E74A8DD574BC60`：`Temp/CurrentUnityCli/ui062-combat-hint-editmode.xml`。
- Unity CLI `1.0.0-beta.8` / Editor `6000.0.28f1c1` 从 630 项源码构建 Windows Development Player。Plains vs Desert `-PreviewCombatInteraction` 在 1280×720 / 1920×1080 均通过 Player 退出码、日志、清单和画幅检查；实图确认 1280 提示放大后仍完整换行于石砖面板，1920 维持原字号。SHA-256：1280×720 `807C0F2E827DE0B77048A5F3DD650EC862C447B7642B3EFC9EA8BE601EE470D3` [截图](../../Temp/CurrentUnityCli/ui062-combat-hint/combat-interaction-1280.png)，1920×1080 `500EF8AC7D1F5C72FACACA2DF8AC3FF3A1FCEF75B72671C9C3CA8AA6535A0A0A` [截图](../../Temp/CurrentUnityCli/ui062-combat-hint/combat-interaction-1920.png)。

## 2026-10-02 RULE-037 离线英雄攻击先结算敌方护甲

- 离线 `DemoLocalMatch` 中，英雄直攻敌方英雄曾绕过护甲；其他普通伤害入口又分别重复手写护甲扣减。新增统一 `ApplyHeroDamage`：普通伤害先消耗护甲，真实伤害绕过护甲，随后共用生命损失观察器；攻击、反击、猪灵岩浆、末影水晶脉冲及自伤/疲劳均通过该入口。终局检查、卡牌效果先后次序和武器耐久保持不变。
- Unity EditMode 参数回归覆盖装备英雄攻击对护甲 1/3、单位攻击对护甲全吸收/无护甲，以及普通伤害/真实伤害与 Piglin 生命损失触发。单位攻击被完全吸收时不增长 Piglin；实际扣血时按规则成长。隔离项目 Unity CLI `1.0.0-beta.8` / Editor `6000.0.28f1c1` 当前全量 EditMode **361/361 Passed**（0 failed / 0 skipped），报告 SHA-256 `D93D9FF1428E226E8662889A9BDD0CB4D06F5D1EB480432603B97BC023284637`：`Temp/CurrentUnityCli/goal-hero-attack-parity-editmode.xml`。
- Unity CLI `unity build` 调用 `BiomeRivals.Demo.Editor.DemoBuildAutomation.BuildWindowsFromCommandLine` 后成功构建 Windows Development Player：`Temp/CurrentUnityCli/goal-normalized-hero-damage/BiomeRivalsDemo.exe`；Unity `6000.0.28f1c1`，构建来源清单 630 项。协议和服务端规则不变，服务端类型检查及 **239/239** 规则测试通过。
- 使用新构建 Player 再实跑 `-PreviewCombatInteraction -PlayerFaction plains_forest -OpponentFaction desert_badlands`，UI/3D 指针部署、无效落点保护、回合推进和攻击结算链通过；1920×1080、退出码 0、来源清单 630 项，截图 SHA-256 `8A5FF21E615686BFA608B92A52069665230D497F01DAEF002C48D8A8782E1521`：[新构建 Player 交互复核](../../Temp/CurrentUnityCli/goal-normalized-hero-damage/combat-interaction-1920.png)。该实图为完整对局交互回归；护甲边界由前述 Unity EditMode 直接断言。

## 2026-10-02 UI-061 联机期间锁定双方阵营预览控件

- 匹配、连接、权威对局、重连和 pending choice 等锁定状态下，己方群系按钮会禁用；此前敌方阵营的左右切换箭头仍呈可点击状态，实际点击才提示无法修改。现让敌方箭头与己方按钮共享同一锁定条件；箭头与阵营文字一并变为中性灰，回到离线预览后恢复原色。
- 新增 Unity EditMode 回归：覆盖本地可切换、Matchmaking 同时锁定双方选择器及标签色、排队回调不能改写任一阵营、返回 Offline 后恢复。隔离 Unity CLI 全量 EditMode **356/356 Passed**（0 failed / 0 skipped），报告 SHA-256 `DEF20A3FEE863679C9D8B7E90514ECDE884B89F30D128BC5FB4FB33CBD7F34B0`：`Temp/CurrentUnityCli/ui061-preview-lock.xml`。
- `-previewOnlineStatus` 现在走正式连接状态 UI 刷新链，双方群系控件及标签同步禁用/置灰，标题标明这是预览而非真实联机；脚本断言两组控件确实锁定。Unity CLI 构建 Windows Development Player（630 项源码清单），Player 退出码 0；1280×720 / 1920×1080 均通过截图、运行日志和画幅校验。SHA-256：`E0CFF267B0646101998420BFFAD78AB760F0383AAB5FDB89929DC4FC89CAA996` [1280×720](../../Temp/CurrentUnityCli/ui061-reconnect-lock-1280.png)，`461C3CB52D698C23A65DA0A1D46932B15C910E6F0B1F361F0CAB15BCEB2AE4ED` [1920×1080](../../Temp/CurrentUnityCli/ui061-reconnect-lock-1920.png)。这验证本地 UI 状态预览，不代表 Docker/Nakama 双客户端验收。
- 同一 Player 另运行 `-PreviewCombatInteraction`，实际经过手牌点击选择、无效拖放不扣费、有效拖放部署、回合按钮及战场攻击者/目标射线；场景日志确认第 2 回合、7/7 能量、攻击单位剩余 1 点生命，Player 退出码 0。截图 SHA-256 `A2CD72362E51094453302EB99561F11F4799C15C54A0800A6A9950831BF43446`：[1920×1080 对局交互](../../Temp/CurrentUnityCli/ui061-combat-interaction-1920.png)。
- 2026-10-02 当前源码复核：隔离工程与工作区的 `Assets` / `Packages` / `ProjectSettings` 共 **630 个文件逐项 SHA-256 一致**；Unity CLI `1.0.0-beta.8` / Editor `6000.0.28f1c1` 全量 EditMode **356/356 Passed**（0 failed / 0 skipped），报告 SHA-256 `2A2DC083B9A97A531F0D97EBEF306ED94BDBF8A6D805FC79F868F34DADEE9793`：`Temp/CurrentUnityCli/goal-continuation-editmode.xml`。服务端 `npm run typecheck` 与 `npm test` **239/239** 通过。当前源码对应的 630 项清单 Windows Development Player 再次实际执行 `-PreviewCombatInteraction -PlayerFaction plains_forest -OpponentFaction desert_badlands`，部署、无效落点保护、回合推进和攻击射线链通过；Player 退出码 0，1920×1080 截图 SHA-256 `1A9BB0A02EB7D3020BD45282512CB9DEA62038D5718BFDF1F830E437383183E8`：[当前源码战斗交互复核](../../Temp/CurrentUnityCli/goal-continuation-combat-1920.png)。

## 2026-10-02 UI-060 费用修正徽记按屏幕字号缩放

- 末影回手预览暴露出 `-1` 费用修正徽记在紧凑手牌上过小：原字号随 Canvas 缩小，1280×720 下显著弱于卡牌规则文字。现将紧凑/详情徽记加宽并加高，字号随根 Canvas 动态换算，屏幕空间维持 10–12px；折扣仍绑定该手牌实例，不改变支付规则。
- 新增缩放回归：`2/3`、`1/2` 与 `1.0` Canvas 下徽记分别保持 10–12px 屏幕字号，并验证 `-1` 文案及徽记留白。Unity CLI `1.0.0-beta.8` / Editor `6000.0.28f1c1` 隔离副本全量 EditMode **355/355 Passed**（0 failed / 0 skipped），NUnit XML SHA-256 `6F74A988E549A076572D9C9C18ADB16EB36F67BE8728AD83931EA0BFC33D67FC`：`Temp/CurrentUnityCli/ui060-editmode.xml`。
- 同一源码构建 Windows Development Player，630 项源码清单通过。`-PreviewEndReturnInteraction` 实际完成末影人部署及紫颂果回手；1280×720 / 1920×1080 均退出码 0。截图 SHA-256：`4CE7F938A5330E60A9F751D567F0439893DE801487F2678B3C31C9B5281AED67` [1280×720](../../Temp/CurrentUnityCli/ui060-end-return-1280.png)，`3E20CFA12F0ABE4E7146EC57422EB5CCF79671383BE0DA76B31ABA722200B7AE` [1920×1080](../../Temp/CurrentUnityCli/ui060-end-return-1920.png)。
- `scripts/validate.ps1 -WithDockerConfig` **21.4 秒通过**：74 张卡牌和 Minecraft 来源门禁、服务端 **239/239**、TypeScript 类型检查/构建与 Compose 静态配置；本次没有启动 Docker/Nakama，因此不把静态配置结果当作在线双端验收。

## 2026-10-02 基准卡牌蜜蜂战吼的本地/权威生命上限回归

- 本地 Demo 新增 `pf_001` 蜜蜂战吼边界用例：生命 29 时正常支付 1 点红石并部署，生命恢复到 30；生命已满时仍正常部署，但不超过上限并明确显示实际恢复 0 点。Nakama 权威规则新增对应满生命回归，确认仍发出可回放的 `HERO_HEALED` 事件（`healing: 0, life: 30`），与受伤时恢复 1 点的现有测试成对覆盖。
- `server-nakama` 测试 **239/239 Passed**、TypeScript 类型检查通过；Unity CLI `1.0.0-beta.8` / Editor `6000.0.28f1c1` 专项 EditMode **2/2**、全量 **353/353 Passed**（0 failed / 0 skipped），全量报告 SHA-256 `4B98CAA3AC8C67E8DB32382DAC11311319F34E4B9CA8BF0D4A3F3723DB0B6D20`：`Temp/GoalContinue/CurrentAudit/editmode-full-bee-battlecry-parity.xml`。本切片只补规则边界回归，不改玩法、协议或版本。

## 2026-10-02 UI-059 权威治疗事件播报实际恢复量

- 在线事件呈现不再从 `CARD_DEPLOYED` 载荷猜测蜜蜂战吼治疗值；改为消费后续权威 `HERO_HEALED` 的来源卡牌、实际恢复量与结算后生命。恢复 0 点时明确提示英雄生命已满，不播放误导性的治疗脉冲；本地满生命提示也避免“恢复 0 点”的生硬表述。
- 新增在线 Presenter 集成回归：验证部署事件不伪报治疗、权威治疗事件播报 1 点与当前生命、满生命 0 点时给出正确提示。Unity CLI `1.0.0-beta.8` / Editor `6000.0.28f1c1` 在与本次修复相关的四个 C# 源文件哈希一致的隔离工程中运行全量 EditMode，**354/354 Passed**（0 failed / 0 skipped）；NUnit XML SHA-256 `310AB234BA2B5C32FD15D01F6547C7975479845357349A7175DC4F2E17253522`：`Temp/GoalContinue/CurrentAudit/editmode-goal-continuation-full.xml`。
- 同一源码另构建 Windows Development Player，并通过 `-PreviewCombatInteraction` 运行部署、回合与攻击流程；1920×1080 Player 退出码为 0，源码清单校验通过，截图 SHA-256 `ABDD7B98BF67B2404F01C07436CB46172380D19CC26B27FEB2D998CD4D7BEBF7`：[战斗交互复核](../../Temp/GoalContinue/CurrentAudit/combat-interaction-heal-feedback-1920.png)。该截图验证整体交互未回归；治疗播报文本由上述 Presenter 集成测试断言，尚未宣称通过真实 Nakama 双端在线验收。

## 2026-10-02 QA-042 末影回手 Player 预览适配 1/1 正式开局

- Unity Player 实际复核发现，`RULE-036` 将正式首回合红石改为 1/1 后，`-PreviewEndReturnInteraction` 仍假设旧的宽裕起手资源，导致 3 费末影人部署被正式规则正确拒绝，预览退出码 2。该问题在 EditMode 全量测试中未覆盖到确定性 Player 预览入口。
- 仅为验收场景显式配置 4/4，不改变正式 1/1 开局；预览现通过真实手牌 UI 点击、3D 格位射线/按下抬起、部署回调、法术目标选择、回手结算与 UI 遮挡检查，确认末影人回手、费用精确 -1、格位清空及结束回合按钮仍阻挡棋盘输入。
- Unity CLI `1.0.0-beta.8` / Editor `6000.0.28f1c1` 在隔离工程全量 EditMode **354/354 Passed**，NUnit XML SHA-256 `BF786410358F4BFF19A6D18359844E775D0AACA63760DEA2475ACF6D9A79CEB4`：`Temp/GoalContinue/CurrentAudit/editmode-end-return-preview-resource-fix.xml`。重建 Windows Development Player 的 630 项源码清单校验通过；1920×1080 实际运行退出码 0，截图 SHA-256 `EF05433B4BECD830FCA9A8DB4B236840395ED57014B2C9B35148FB7D49949858`：[末影回手 3D 交互预览](../../Temp/GoalContinue/CurrentAudit/arena-037-target-hover-1920-fixed.png)。

## 2026-10-02 UI-058 回合切换横幅加入像素吸附滑入动效

- 回合/阶段提示横幅此前只做线性淡入淡出。现改为缓入缓出的淡入与 14 个整数设计像素的下滑，停留后上滑 12 个整数设计像素并淡出；保持缩放为 1，避免像素边框被非整数缩放模糊。新提示会取消旧动画，并始终以构建时记录的静止锚点开始，避免中途替换造成横幅位置漂移。
- 新增 EditMode 回归验证初始整数偏移及新横幅替换旧动画。Unity CLI `1.0.0-beta.8` / Editor `6000.0.28f1c1` 在隔离工程全量 EditMode **351/351 Passed**：`Temp/GoalContinue/CurrentAudit/editmode-ui058-final.xml`。后续为继续审查又逐项比较当前工作区与隔离副本的 `Assets`、`Packages`、`ProjectSettings` SHA-256，确认完全一致；同一 Unity CLI/Editor 对该当前源码复跑全量 EditMode，仍为 **351/351 Passed、0 failed、0 skipped**：`Temp/GoalContinue/CurrentAudit/editmode-current-goal-continuation.xml`。
- `scripts/capture-demo-preview.ps1 -PreviewTurnBanner` 新增确定性中段帧检查：必须实测横幅 alpha ≥ 0.58、仍在滑入、垂直偏移为整数设计像素。Unity CLI 生成的 Windows Development Player（630 项源码哈希清单）在 1280×720、1920×1080 均由真实 Player 截图通过，运行日志均为 alpha 0.58 / 垂直偏移 −6px / 退出码 0。SHA-256：`BF53CE568F602F6799A632A0CE10614C3E23A80D9E3B8AEE36F33AE6B220D432` [1280×720](../../Temp/GoalContinue/CurrentAudit/ui058-banner-motion-verified-1280.png)，`40C4D51B3853CF2B7C6E0FEB555A190F0DC4CCB6095AF6EB3F1AAA0734F2041E` [1920×1080](../../Temp/GoalContinue/CurrentAudit/ui058-banner-motion-verified-1920.png)。目视确认动效帧中的群系金色文字仍可读。
- 后续用同一最终 Player 扩展到 4:3（1600×1200）和 21:9（2560×1080）：两个场景均以 alpha 0.58 / −6px 捕获滑入帧、退出码 0、源码清单 630 项，目视确认留边布局下侧栏/手牌/详情面板未被裁切或压缩。SHA-256：`BF0A7545269A34E0AEE290E046F02BC8F315893EE581204B2E69FA8086D73A13` [4:3](../../Temp/GoalContinue/CurrentAudit/ui058-banner-motion-4x3-1600x1200.png)，`FF87E83C3F0D6F774E5FF4E5B12ABDC96C339F47F3085950DFF5B11E2273D0A5` [21:9](../../Temp/GoalContinue/CurrentAudit/ui058-banner-motion-21x9-2560x1080.png)。
- 最终代码另以 `-PreviewCombatInteraction` 重新运行部署、进入战斗、攻击和回合流，Player 退出码为 0，630 项源码清单通过；1920×1080 截图 SHA-256 `4388590BBEAF19746051452436879D3DD71D0646CAA1F46026776E9E1F32B947`：[完整战斗交互复核](../../Temp/GoalContinue/CurrentAudit/ui058-combat-final-1920.png)。该复核确保动效收尾后常规战场操作仍正常。

## 2026-10-02 UI-057 回合阶段徽牌跟随当前行动阵营

- 顶部回合/阶段徽牌此前使用固定中性色，阵营切换后不再与卡框和战场主题对应。现在起手调度/玩家回合读取己方群系 Accent，对方回合读取对方群系 Accent；注册缺失回退为中性浅色。平局/胜利继续使用金色、战败继续使用危险色，不覆盖终局语义。
- 新增七群系回归：分别验证玩家回合主题色、结束回合后的对方主题色及下一轮恢复己方主题色。Unity CLI `1.0.0-beta.8` / Editor `6000.0.28f1c1` 在隔离工程全量 EditMode **350/350 Passed**：`Temp/GoalContinue/CurrentAudit/editmode-ui057-full.xml`。
- 使用项目自定义 `DemoBuildAutomation.BuildWindowsFromCommandLine` 构建 Windows Development Player（630 项源码哈希清单）。实际运行 `-PreviewCombatInteraction` 与 `-PreviewOpponentEnergy`，平原玩家/沙漠对手在 1920×1080 的己方回合与对方回合截图分别通过 Player 退出码、运行日志、源码清单和画幅校验，并目视确认徽牌从平原金色切换为沙漠青绿色：SHA-256 `627061DF6C474CC55E02FE854794C8491D0D0CFA8A24ABA5EC28FF30200AB4B2` [己方回合](../../Temp/GoalContinue/CurrentAudit/ui057-player-main-1920.png)，`29F71409E11E19DCFF6D2017C944F5081B608820F894450E0DF9AD9447646E7A` [对方回合](../../Temp/GoalContinue/CurrentAudit/ui057-opponent-turn-1920.png)。构建与检查在隔离副本完成，主图形 Editor 未受影响。
- 后续回归补齐终局颜色断言：先切换到青绿色的沙漠阵营，再验证胜利/平局仍为金色、战败仍为危险红色，避免主题 Accent 恰好掩盖颜色覆盖问题。专项 EditMode **1/1 Passed**；最新隔离全量 EditMode **351/351 Passed**：`Temp/GoalContinue/CurrentAudit/editmode-ui057-terminal-colors-full.xml`。

## 2026-10-02 RULE-035C1 通用平局在线验收夹具与双端探针

- 为通用同时败北补上独立验收入口：Nakama 预留 opcode `255` 仅在 `BIOME_RIVALS_ENABLE_TEST_FIXTURES=true` 时受理；Compose 默认值为 `false`。夹具在局部副本上注入双方归零前置条件，再经正式 `PLAY_CARD` 命令/规则引擎结算，并复用相同的权威事件投影与双方广播。普通关闭状态不会改写对局。
- 新增 `npm run smoke:simultaneous-draw`，准备覆盖两端共同终局 revision/事件顺序及断线重连后的权威平局快照；成功确认使用测试专用 opcode `5`。本子任务没有改动通用/DB-007 胜负规则、卡牌内容或生产协议版本。
- `scripts/validate.ps1 -WithDockerConfig` 全链静态验证 **21.4 秒通过**：卡牌内容、Minecraft 来源和卡框漂移门禁、服务端类型检查/构建、**238/238** 规则/Match Handler 测试及 Compose 配置均通过；`node --check scripts/smoke-simultaneous-draw.mjs` 通过。随后重新调用 Unity CLI `1.0.0-beta.8` / Editor `6000.0.28f1c1`，隔离工程 EditMode **349/349 Passed**：`Temp/GoalContinue/CurrentAudit/editmode-continuation-final.xml`。
- Docker Engine 当前不可用，本次没有运行 Nakama 双客户端探针，也没有启动/重置 Docker 或接触其内部 socket、容器和卷。RULE-035C 在线验收保持未完成；探针报告的 Match ID、revision 与重连证据需在隔离服务恢复后产生。

## 2026-10-02 UI-056 战斗指令跟随玩家群系主题

- 右侧战斗阶段主提示此前固定为青色，玩家切换阵营后仍不变化，与卡牌/头像已按群系更新主题的规则不一致。现按己方当前阵营 ID 读取已注册主题 Accent 着色；无对应注册时才回退到中性青色。敌方目标状态色、危险提示和金色优先目标均保持各自语义，不受此处影响。
- 新增回归遍历七个阵营，验证战斗阶段标题颜色与各自主题注册一致。Unity CLI `1.0.0-beta.8` / Editor `6000.0.28f1c1` 在隔离工程全量 EditMode **349/349 Passed**：`Temp/GoalContinue/CurrentAudit/editmode-themed-combat-title.xml`。
- 通过项目自定义 `DemoBuildAutomation.BuildWindowsFromCommandLine` 构建 Development Player（生成 630 项源码哈希清单），实际运行 `-PreviewCombatInteraction -PlayerFaction plains_forest -OpponentFaction desert_badlands`；1920×1080 截图验证 Player 退出码 0、场景运行报告与画幅，SHA-256 `4E759C3059660445155091AAEE7CF181ADCF626D66DB0E15DF0BD29FA2399A02`：[themed-combat-title-1920.png](../../Temp/GoalContinue/CurrentAudit/themed-combat-title-1920.png)。实图确认战斗提示使用平原阵营注册主题色；主图形 Editor 保持运行，检查在隔离项目完成。

## 2026-10-02 UI-055 战场合法格改用群系协调色

- 战场合法目标格此前以偏青的高亮色渲染，和 Minecraft 草地、方块地表的自然色相冲突。现将己方可部署/可操作格调整为草绿至淡黄绿，将敌方目标格调整为土块棕至火把琥珀；阵营语义一眼可辨，同时保留金色优先目标、悬停/按压反馈、拒绝态红色反馈与地表脉冲。
- 高亮继续由战场地表材质按世界坐标绘制，覆盖实际地表格，不回退为 Canvas 叠层；原有格位射线、抬升与透视贴合行为不变。
- 回归断言验证友方目标为绿相、敌方目标为暖琥珀相。Unity CLI `1.0.0-beta.8` / Editor `6000.0.28f1c1` 在隔离工程全量 EditMode **348/348 Passed**：`Temp/GoalContinue/CurrentAudit/editmode-worldspace-highlight-colors.xml`；Windows Development Player 构建日志为 `Build Finished, Result: Success.`。
- 同一 Player 以 1280×720、1920×1080 实际运行并分别预览敌方与友方目标格，四份报告均记录退出码 0、630 项源码清单和期望画幅；1280×720 截图 SHA-256：敌方 `92F88FCC2BF0111DAB84A15607D99CE2CD02D1EBE3AC6C2F6BAD0E3377AF9129` [预览](../../Temp/GoalContinue/CurrentAudit/ground-target-colors-1280.png)，友方 `73939DD20F254499D69F2CF2CDE63DAA01D37F39E0F6B96EABBBA8300B186129` [预览](../../Temp/GoalContinue/CurrentAudit/friendly-ground-target-1280.png)；1920×1080 预览见同目录 `ground-target-colors-1920.png` 与 `friendly-ground-target-1920.png`。主项目图形 Editor 保持打开，验证均在隔离副本完成。

## 2026-10-02 QA-049 战斗交互预览显式设置资源夹具

- Unity Player 实审发现 `-PreviewCombatInteraction` 仍依赖旧的 6/6 起手资源：真实规则已改为 1/1 后，预览先部署 1 费蜜蜂再拖放 2 费绵羊，格位射线命中但规则正确拒绝部署，导致验收脚本以错误码退出。
- 该预览的目标是覆盖两次部署、无效落点保护、回合循环与战斗指针链，不是验证开局经济；现显式将该场景的玩家资源夹具设为 6/6，使两张牌可部署且回合后资源断言 7/7 保持稳定，没有改变正式对局经济。
- Unity CLI 重新构建 Windows Development Player 后，`-PreviewCombatInteraction -PlayerFaction plains_forest -OpponentFaction desert_badlands` 实际完成卡牌点击部署、非法落点不扣牌/资源、手牌拖放部署、主行动→战斗→结束回合→下一回合、攻击者和目标射线选择、攻击结算与阵亡反馈。1280×720、1920×1080 实图均通过退出码、确定性日志、630 项源码清单与画幅检查：1280 SHA-256 `AA519177B2EC9FBCC0A8D7FEC4705F5BF708CAF28C989E169F4643ED6BC68193`：[combat-interaction-review-1280-v3.png](../../Temp/GoalContinue/CurrentAudit/combat-interaction-review-1280-v3.png)；1920 SHA-256 `3EB564148CA03862480C215AF019FF38075BB158CD59780F4D7194C1EF413FD5`：[combat-interaction-review-1920-v3.png](../../Temp/GoalContinue/CurrentAudit/combat-interaction-review-1920-v3.png)。
- Unity CLI Editor `6000.0.28f1c1` 当前完整 EditMode **348/348 Passed**：`Temp/GoalContinue/CurrentAudit/editmode-combat-preview-guard.xml`。该回归修复的是 QA 预览夹具，不替代 RULE-036 正式 1/1 开局规则或 Docker/Nakama 双端验收。

## 2026-10-02 UI-054 手牌支付可行性反馈

- 手牌此前只展示红石费用，没有视觉区分“当前可支付”和“资源不足”的牌。现按有效费用与当前可用红石判断；拥有已注册配方的单位/建筑/结构牌，在材料齐备时也视作可支付。不可支付牌轻度压暗至 62% 透明度，但不禁用 Button、CanvasGroup 交互或射线，因此仍能点选查看详情，也保留拖放入口；选择另一支付方式不会改变权威规则判断。
- 1280×720 实图审查又发现详情区单行欠费文案太细小；现改为加粗居中的两行“红石不足 / 需要 N · 当前 M”，字号由 15 提至 17，并在确定性预览中强制断言文案换行，避免只靠人工目测。
- 手牌支付路径现收敛到 `DemoDeploymentRules.CanPayWithRedstoneOrCrafting`，并直接供手牌可支付视觉调用；回归覆盖临时红石不重复计入、合成材料齐全的零红石可部署、缺料不可部署。压暗仍保留 Button/CanvasGroup 交互与射线，可选牌并拖放。
- 新增 EditMode 回归验证压暗状态保留可交互/射线且可恢复亮度，以及上述支付边界。Unity CLI `1.0.0-beta.8` / Editor `6000.0.28f1c1` 在隔离副本执行全量 EditMode **348/348 Passed**：`Temp/GoalContinue/Affordability/editmode-affordability-contract.xml`。主项目仍由 PID 484 的图形 Editor 打开，没有并发操作该项目。
- 隔离副本 Windows Development Player 以最终源码重建成功；确定性预览经实际 `GraphicRaycaster` 命中并派发指针按下/抬起/点击，选择了 2 费放牧绵羊，打开详情并显示两行欠费提示；日志同时验证手牌实例仍在手牌、资源未扣、提示确实换行。1280×720 实图 SHA-256 `C9A783D169B0596F6A0218AA0EBA8EF5119040AF43BA190A25C2AC856E53EEED`：[unaffordable-card-click-1280-v2.png](../../Temp/GoalContinue/Affordability/unaffordable-card-click-1280-v2.png)；1920×1080 SHA-256 `EBB2702CAC22E91C6D3CF1FD4EBE93B27CE6B97AFD3FC25F68D3C8F3F20CECE0`：[unaffordable-card-click-1920-v2.png](../../Temp/GoalContinue/Affordability/unaffordable-card-click-1920-v2.png)。两图均校验 Player 退出码、运行日志、630 项源码清单与画幅，且目视确认提示字号/换行和卡面可读。

## 2026-10-02 RULE-036 离线对局红石起始规则对齐

- 离线 `DemoLocalMatch` 曾从 6/6 起手，权威 Nakama 和 GDD v0.5 §3.5/§4.1 则从 1/1 起手。现在本地玩家与对手均为 1/1；玩家每次进入后续自己的回合容量 +1、恢复至容量（上限 10），对手回合公开资源也按同一轮次增长。敌方资源 HUD 和玩家能量 HUD 的初始占位文案同步改为 1/1；脚本化场景预览单独显式设置资源，不再依赖真实对局起始值。
- 规则回归覆盖真实离线部署/施法/回合循环：首回合 1 点部署蜜蜂，第二回合 2 点部署苗圃，第三回合 3 点施放护甲；另验证对手在第 1/2 回合分别显示 1/1 与 2/2。其它卡牌效果测试改用明示的稳定资源场景夹具，避免把卡牌能力测试误当成经济曲线测试。Unity CLI `1.0.0-beta.8` / Editor `6000.0.28f1c1` 隔离工程全量 EditMode **345/345 Passed**：`Temp/GoalContinue/CurrentAudit/editmode-redstone-parity-full.xml`。主图形 Editor 保持打开。
- 隔离 Windows Development Player 由 Unity CLI 构建成功。最终源码对应的默认首回合 1920×1080 实图确认玩家 1/1、敌方下回合预告 1/1，SHA-256 `02D3487406E67DC0C3D52C2D9AC201D02E1668C2BA6A8148C3C11D79756AAD2C`：[default-start-1-1-final-1920.png](../../Temp/GoalContinue/RedstoneParity/default-start-1-1-final-1920.png)。同一 Player 通过 `-PreviewOpponentEnergy` 实际推进回合，确认第 2 回合敌方当前红石 2/2，SHA-256 `9333607C66237C91B292F17D9F776E536997EF7A057036312C677B98657BD672`：[opponent-energy-1-to-2-final-1920.png](../../Temp/GoalContinue/RedstoneParity/opponent-energy-1-to-2-final-1920.png)。两图均经截图脚本验证进程退出码、场景日志、源码清单与画幅尺寸，并目视确认。
- 集成校验 `scripts/validate.ps1 -WithDockerConfig` **19.0 秒通过**：卡牌/美术来源门禁、服务端 **236/236**、TypeScript 类型检查/构建及 Compose 静态配置均通过。没有启动 Docker Engine；真实 Nakama 双客户端联机验收仍需单独执行。

## 2026-10-02 UI-053 对手红石资源提示

- 对手 HUD 之前只有生命/护甲与阵营信息，玩家回合中看不到对手将有多少资源、对手行动中也看不到剩余资源。新增紧贴对手阵营栏的同主题石砖资源牌：己方行动时从当前容量按 GDD 回合增量规则预测对手下回合容量（首回合不增长，此后 +1、上限 10）；对手行动时读取当前公开红石、容量与临时红石。服务端视图直接读取权威快照，不从卡牌动作或动画猜测消耗；本地视图读取同一回合模型。信息牌与文字均关闭 Raycast，不拦截战场/按钮事件。离线起始规则差异由 RULE-036 对齐。
- Unity CLI `1.0.0-beta.8` / Editor `6000.0.28f1c1` 隔离项目全量 EditMode **345/345 Passed**：`Temp/GoalContinue/CurrentAudit/editmode-opponent-energy-final-final.xml`。新增本地和权威视图回归，包括对手 6 点基础红石 +2 临时红石映射为 `6/7 · 临时 +2`，以及权威视图首回合容量 1→下一回合 2 的预告。Windows Development Player 构建成功；`-PreviewOpponentEnergy` 真实推进两轮本地流程并验证第 2 回合对手资源为 **7/7**。1920×1080 截图通过 Player 退出码、确定性日志、630 项源码清单和画幅校验，SHA-256 `2527C9FFB5E230356EFAF0A620778E24654ACC5E78639D825983AE045A14D0B8`：[opponent-energy-1920-verified.png](../../Temp/GoalContinue/CurrentAudit/opponent-energy-1920-verified.png)。

## 2026-10-02 UI-052 石砖按钮按下反馈

- 普通阵营/面板按钮此前只有很短的颜色渐变；只有少数操作按钮单独挂了缩放组件，交互反馈不一致。统一复用 `DemoHoverScale`：所有样式化按钮共享轻微悬停强调和按下压缩，手牌/选项卡沿用现有的放大、抬升与层级行为；失效或禁用按钮不响应过期指针事件。结束回合、确认、战吼/法术按钮重用已有组件，避免重复添加两个竞争缩放器。
- 新增 EditMode 覆盖禁用按钮忽略悬停/按下、正常状态进入 hover→pressed→release 时尺度/位置目标的变化；Unity Player 预览通过 EventSystem 派发按钮指针进入/按下事件。
- Unity CLI `1.0.0-beta.8` / Editor `6000.0.28f1c1` 隔离副本全量 EditMode **344/344 Passed**：`Temp/GoalContinue/CurrentAudit/editmode-button-feedback-fix1-20261002.xml`；Windows Development Player 构建成功，1920×1080 按钮按下预览通过 Player 退出码、`Button feedback preview pressed state: True` 日志、630 项源码清单和截图尺寸校验。实图 SHA-256 `6F3C7B2032FEA4933CA19D02ECB467460EA415C994B22B1A331D66E844E1B885`：[button-feedback-1920-r2.png](../../Temp/GoalContinue/CurrentAudit/button-feedback-1920-r2.png)。
- 2026-10-02 当前源码复核再次通过实际 Player 按下事件与确定性日志，使用 630 项来源清单、退出码 0 和 1920×1080 画幅校验；截图 SHA-256 `4C7E3BB2B474DA47ABF8547767B447C7C97F434D4BD4FD7B97FB192A6CA28508`：[button-feedback-current-source-1920.png](../../Temp/GoalContinue/CurrentAudit/button-feedback-current-source-1920.png)。

## 2026-10-02 QA-048 非 16:9 视口群系天空延续

- 4:3 满手预览中，16:9 主相机视口外的上下留边为纯黑，和视口内的群系天空断开。新增无几何绘制的全屏背景相机先清屏到玩家/敌方共同主题天空色，战场相机仍只渲染居中的 16:9 视口；任一方切换群系时同步更新两台相机。
- 命令行截图流程同步先渲染背景相机、再渲染战场与 UI，避免 Player 实际运行和截图 RenderTexture 的画面不一致。EditMode 增加背景相机满屏、零 culling mask、绘制顺序及切换敌方阵营后颜色同步断言。
- Unity CLI `1.0.0-beta.8` / Editor `6000.0.28f1c1` 隔离副本全量 EditMode **343/343 Passed**：`Temp/GoalContinue/CurrentAudit/editmode-letterbox-sky-20261002.xml`（SHA-256 `38B7488338447027A6506744DFD0FC156907EF8141DA350FD4D50C16E993DFD3`）；Windows Development Player 构建成功。`-PreviewFullHand -PlayerFaction desert_badlands -OpponentFaction end` 在 1600×1200 与 2560×1080 均通过退出码、运行日志、630 项源码清单和画幅校验；实图目视确认上下/左右留边由纯黑变为与场景天空连续的暗色群系氛围，16:9 构图与七张手牌保持完整。4:3 SHA-256 `B52EED47538736E0D63DDCAB26DCDA2C3C939729D274761F273D8349504D97EA`：[letterbox-sky-4x3-1600x1200.png](../../Temp/GoalContinue/CurrentAudit/letterbox-sky-4x3-1600x1200.png)；21:9 SHA-256 `A70D1A6FC8A4ED5F0D43588A77A922A155D285309E5EF26F1572C78A36B4DE70`：[letterbox-sky-21x9-2560x1080.png](../../Temp/GoalContinue/CurrentAudit/letterbox-sky-21x9-2560x1080.png)。
- 同一工作区 `scripts/validate.ps1 -WithDockerConfig` **19.1 秒通过**：74 张卡定义/文本/美术映射、40 个世界纹理来源、64 个实体资源来源、62 个已实现效果校验；服务端 **236/236**、TypeScript 类型检查与构建、Compose 静态配置均通过。Docker Engine named pipe 缺失且 `com.docker.service` 停止，本次未启动服务；RULE-033C 双端在线回手/隐私/重连 smoke 仍未验收。

## 2026-10-02 QA-046 双阵营地表切换与透视复核

- 当前源码 Windows Development Player 使用 `-PreviewAttackFeedback -PlayerFaction plains_forest -OpponentFaction desert_badlands` 实际运行攻击预览。目视确认己方近端为平原草地、敌方远端为沙漠红砂岩，双方群系切换独立生效；战场格与目标高亮沿用 2.5D 相机透视，没有 Overlay 正方形网格或两半拼成直角的问题。
- 1280×720 截图 SHA-256 `6AD97822B26FCBD475119646EE86A381D0DF05831AEA6B2E3DEA5E9BC08BD635`：[faction-plains-desert-attack-1280.png](../../Temp/GoalContinue/CurrentAudit/faction-plains-desert-attack-1280.png)；1920×1080 SHA-256 `EE46FDEB582E800110DA55F824911C702BE122053B1DDB038E633B7549736616`：[faction-plains-desert-attack-1920.png](../../Temp/GoalContinue/CurrentAudit/faction-plains-desert-attack-1920.png)。两张均由 `scripts/capture-demo-preview.ps1` 检查 Player 退出码、确定性日志、630 项源码清单和画幅尺寸。
- 交叉检查发现满手预览虽然接收 `-PlayerFaction` 参数，`SetupFullHandPreview()` 却再次硬编码选择 `end`，静默覆盖了命令行设置。现移除该覆盖：预览继续固定使用七张末地卡验证长标题/满手排版，同时保留显式选择的战场阵营；EditMode 回归断言沙漠地表材质仍为 `red_sandstone` 且七张牌仍为末地卡。全量 Unity CLI EditMode **343/343 Passed**（`Temp/GoalContinue/CurrentAudit/editmode-current-final.xml`）。
- 重建隔离 Windows Development Player 后，以 `-PreviewFullHand -PlayerFaction plains_forest -OpponentFaction desert_badlands` 复核 1280×720 与 1920×1080；两张实图均确认近端平原、远端沙漠，手牌排版通过。1280×720 SHA-256 `0FD0813162FD71B381D97FC78263A06174CB189C1B8AC40B330DD72D2259A87A`：[full-hand-faction-preserved-1280.png](../../Temp/GoalContinue/CurrentAudit/full-hand-faction-preserved-1280.png)；1920×1080 SHA-256 `B786FD06F4C332A43D64801BDA8AC21F84615197682A78A55838E3014A3E4AF9`：[full-hand-faction-preserved-1920.png](../../Temp/GoalContinue/CurrentAudit/full-hand-faction-preserved-1920.png)。

## 2026-10-02 QA-047 非 16:9 满手牌边界复核

- 将满手卡牌边界验收扩展到 4:3 与 21:9 时，4:3 Player 因投影角点的浮点误差把恰好位于允许安全边界 `-656` 的卡牌判为越界。没有改变卡牌位置或安全间距；运行时自检与 EditMode 断言统一增加 **0.5 个设计单位**舍入容差。
- Unity CLI `1.0.0-beta.8` / Editor `6000.0.28f1c1` 隔离工程全量 EditMode **343/343 Passed**：`Temp/GoalContinue/CurrentAudit/editmode-aspect-final.xml`；Unity CLI 重建 Development Player 后，`-PreviewFullHand -PlayerFaction plains_forest -OpponentFaction desert_badlands` 的 1600×1200 与 2560×1080 均通过 Player 自检、退出码、确定性日志、630 项源码清单与截图画幅校验。非 16:9 画幅在中心保留 16:9 构图并留黑边，7 张卡片、右侧详情和行动区没有碰撞。
- 1600×1200 SHA-256 `DD9248B96641A0A026803F400A3E66762B2F4A1F3E621EBC5BE36209BF36CA41`：[full-hand-aspect-4x3-1600x1200.png](../../Temp/GoalContinue/CurrentAudit/full-hand-aspect-4x3-1600x1200.png)；2560×1080 SHA-256 `B7A1948A0DFB541BA6961E5C83F2B3DD341A71558B7774900DBE7A859EBDF1C8`：[full-hand-aspect-ultrawide-2560x1080.png](../../Temp/GoalContinue/CurrentAudit/full-hand-aspect-ultrawide-2560x1080.png)。

## 2026-10-02 UI-051 离线玩家提示隐藏内部修订号

- Unity Player 攻击反馈实图发现本地战斗状态提示会附加内部“状态 rN”，在 1280×720 下挤占伤害/反击/阵亡信息。所有离线命令结果（目标选择、部署、战斗与卡牌效果）现只展示玩家可读文案；失败文案仍保留，空消息使用明确回退。在线“服务器已确认 · 状态 rN”诊断保持不变。
- 新增 EditMode 回归，使用带 revision 的本地接受/拒绝命令检查提示只显示面向玩家的信息。Unity CLI `1.0.0-beta.8` / Editor `6000.0.28f1c1` 隔离工程全量 **343/343 Passed**：`Temp/GoalContinue/CurrentAudit/editmode-local-status.xml`。隔离工程 Windows Development Player 构建成功，`-PreviewAttackFeedback` 由真实离线攻击结算并验证无修订号提示；两档截图通过 Player 退出码、运行日志、630 项源码清单与 PNG 尺寸检查：1280×720 SHA-256 `73776277B1EE4C2412482695600B4ED041CB54488F385C55EB8E2DF3C7270BEA`：[attack-status-clean-1280.png](../../Temp/GoalContinue/CurrentAudit/attack-status-clean-1280.png)；1920×1080 SHA-256 `60DA21C0ADA947F96176E0B830779E6A21257D5F4DBE04D96330F7C15D2EFEFA`：[attack-status-clean-1920.png](../../Temp/GoalContinue/CurrentAudit/attack-status-clean-1920.png)。

## 2026-10-02 UI-050 终局横幅三态一致性

- 终局复核发现在线和离线横幅各有二态缺陷：权威 `MATCH_ENDED` 的空赢家被误报“战败”；本地双方英雄同为 0 时，详情面板显示“平局”但横幅显示“胜利”。现统一使用胜利/战败/平局三态判定、文案与颜色映射；本地战斗、疲劳结束以及权威事件消费共用同一呈现规则。另保留 DB-007 冻结特例：双方同归零时显式指定出土方获胜，避免通用平局投影覆盖卡牌规则。
- 新增 EditMode 回归覆盖当前玩家获胜、对手获胜、null/空赢家及离线胜/负/平四种生命状态；另通过在线事件 Presenter 与横幅 UI 组件验证权威空赢家实际显示金色“平局”。Unity CLI `1.0.0-beta.8` / Editor `6000.0.28f1c1` 隔离副本全量 **342/342 Passed**：`Temp/GoalContinue/SimultaneousDraw/editmode-authoritative-banner.xml`。
- 同一隔离副本由 Unity CLI 重建 Windows Development Player 成功；`-PreviewMatchOutcome` 真实离线英雄攻击胜利回归及渲染检查通过。1280×720 SHA-256 `04F1933B3A5CCF021E3137E20D732814CFB354AAC30A5AC7C58F31DD06C156B9`：[local-terminal-final-1280.png](../../Temp/GoalContinue/SimultaneousDraw/local-terminal-final-1280.png)；1920×1080 SHA-256 `2D0299F9799F1F51DD2BCBFF38DBEC1E80495FE31A77D051F315CD007B47CE4C`：[local-terminal-final-1920.png](../../Temp/GoalContinue/SimultaneousDraw/local-terminal-final-1920.png)。Player 实图回归真实胜利布局；平局逻辑由 Unity EditMode 测试覆盖，本轮未伪造当前规则集不可达的线上平局触发。
- DB-007 双杀回归补充：修复离线结算只由英雄生命推断胜负、从而丢失“出土方获胜”规则特例的问题；Unity CLI 全量 EditMode 最终 **342/342 Passed**（`Temp/GoalContinue/SimultaneousDraw/editmode-db007-owner-wins-final.xml`）。隔离 Windows Player 通过 `-PreviewTntTrapOwnerWins` 真实触发双方归零，确认玩家（出土方）获胜；1280×720 SHA-256 `21E54A43A15E063C4B70E6A54F305B2CE5D5F413E2F2457F903387E91E1F75F3`：[db007-owner-wins-1280.png](../../Temp/GoalContinue/SimultaneousDraw/db007-owner-wins-1280.png)，1920×1080 SHA-256 `17311F63F05D8CC14FE2AA9C299981EFC9D0A19D6B33D36187B3299E6CC5C2FA`：[db007-owner-wins-1920.png](../../Temp/GoalContinue/SimultaneousDraw/db007-owner-wins-1920.png)。Player 日志、源码清单、退出码及图像尺寸均由截图脚本校验；图片目视确认画面显示“胜利”。

## 2026-10-02 RULE-035 通用同时败北平局结算

- GDD v0.5 §6.5、§11 要求双方英雄在同一次胜负检查中均为 0 生命时判平。服务端通用自伤/疲劳终局和英雄战斗结算现在发出 `MATCH_ENDED { winnerPlayerId: null, reason: SIMULTANEOUS_DEFEAT }`；终局不变量、命令/事件/快照 Schema 同步校验空赢家只对应双方归零。协议升至 39、规则集升至 `prototype-0.64`。
- Unity 状态存储验证并投影该权威事件/快照；在线视图明确不把任一玩家判为胜者。本地终局 UI 已有平局结果投影。`DB-007` 炸药机关继续遵守冻结规范 §14 的特例：先结算敌方 3 点伤害、再结算己方 1 点真实伤害，双归零时由出土牌拥有者获胜，不套用通用平局。
- `scripts/validate.ps1 -WithDockerConfig` **20.4 秒通过**：74 张卡牌和 Minecraft 来源门禁、服务端 **236/236**、TypeScript 类型检查/构建与 Compose 静态配置；Unity CLI `1.0.0-beta.8` / Editor `6000.0.28f1c1` 在新隔离副本全量 EditMode **339/339 Passed**：`Temp/GoalContinue/SimultaneousDraw/editmode-protocol39.xml`。真实 Nakama 双客户端平局 smoke 尚未执行；Docker Engine 管道不可用，该闭环保留在 RULE-035C。

## 2026-10-02 UI-049 终局阶段状态同步

- 胜负面板下方的右下状态条曾保留阵营切换等过期提示；现在终局时改为显示本局结果与“所有操作已锁定”。结束按钮保持不可交互，并明确标为“本局已结束”。顶部状态同步显示“对局结束 · 胜利/战败/平局”，右侧标题改为“对局结果”；在线胜负沿用服务端权威赢家投影，不改变对局规则。
- 扩展终局 Inspector 回归，验证胜/负/平三种情况的顶部阶段、结果标题、状态条文案一致，并检查结束按钮已禁用。Unity CLI `1.0.0-beta.8` / Editor `6000.0.28f1c1` 隔离副本全量 EditMode **336/336 Passed**：`Temp/GoalContinue/TerminalPhase/editmode-final.xml`。
- Windows Development Player 通过真实规则链再次触发英雄攻击胜利；1280×720 SHA-256 `ED511B318F9746D574793F5AD6D894660F21A66A96EE8833CA0F2EF045B3CE81`：[match-outcome-final-1280.png](../../Temp/GoalContinue/TerminalPhase/match-outcome-final-1280.png)；1920×1080 SHA-256 `1BE2B28BEB6BFC9C41AC2025585E4F65249FEA5E3502ABB111014C5C35B2F851`：[match-outcome-final-1920.png](../../Temp/GoalContinue/TerminalPhase/match-outcome-final-1920.png)。两份截图均验证 Player 退出码、确定性结果日志、630 项构建源码清单和画幅尺寸。

## 2026-10-02 QA-045 当前工作区分层集成复核

- `scripts/validate.ps1 -WithDockerConfig` **25.8 秒通过**：74 张卡牌与 62 个已实现效果的注册/文本校验、Minecraft 卡图 74 项/世界纹理 40 项/实体资源 64 项来源门禁、卡框漂移负向控制、服务端 **234/234**、TypeScript 类型检查与构建，以及 Compose 静态配置。协议 39 的增量回归另见 RULE-035。
- 同一工作区源码的 Unity CLI `1.0.0-beta.8` / Editor `6000.0.28f1c1` 隔离副本 EditMode **336/336 Passed**，Windows Development Player 构建成功；终局预览两分辨率均通过源码清单、运行日志和 PNG 尺寸验证（详见 UI-049）。由于图形 Editor 正在使用主项目，Unity CLI 测试/构建在隔离副本执行，主 Editor 未关闭或重启。
- Docker Compose 文件可静态解析，但 Engine named pipe `dockerDesktopLinuxEngine` 不存在且 `com.docker.service` 为 Stopped；本次没有启动/重置 Docker，也未执行 Nakama 双客户端 smoke。因此 RULE-033C 的在线回手、隐私与重连门禁仍未通过。

## 2026-10-02 UI-048 持久化对局终局结果

- 胜负横幅结束后，详情区域此前只显示“对局已经结束 / 所有操作已锁定”，玩家无法回看结果或最终英雄生命。现改为固定展示胜利、战败或平局，并保留己方/对手最终生命与输入锁定说明；线上结果读取权威快照 `winnerPlayerId` 与 `viewerPlayerId`，离线结果由本地终局生命状态映射，不改变胜负规则。
- 新增 EditMode 回归覆盖离线胜/负/平局结果面板与最终生命，以及权威视角分别面对 Alice/Bob 获胜时的胜负映射。Unity CLI `1.0.0-beta.8` / Editor `6000.0.28f1c1` 在隔离副本运行全量 EditMode **336/336 Passed**：`Temp/GoalContinue/MatchOutcome/editmode.xml`。
- Development Player 通过实际规则链装备激流三叉戟、进入战斗并以英雄攻击结束对局；截图脚本同时确认胜者投影、玩家 30 / 对手 0、Player 退出码、确定性日志、源码清单和画幅。1280×720 SHA-256 `088ED85A4818E5D821EF6E7020E768B92A2729389E4738E105EE673189D95101`：[match-outcome-1280.png](../../Temp/GoalContinue/MatchOutcome/match-outcome-1280.png)；1920×1080 SHA-256 `002AEFB4D7F2692EE37F2E3A8FE03B86CFE5A53D3FA2F22E7DB0187D7D606C3B`：[match-outcome-1920.png](../../Temp/GoalContinue/MatchOutcome/match-outcome-1920.png)。
- 在线认输结果尚未做 Docker/Nakama 双端画面实测；该面板对在线赢家 ID 的客户端映射由 EditMode 覆盖，真实联机视觉验收仍在在线门禁内。

## 2026-10-02 QA-044 合成支付与双格建筑 Player 交互复核

- 将确定性 Player 预览扩展为真实输入链：通过 UI `GraphicRaycaster` 点击合成支付，再由手牌 UI 事件与 3D 战场射线拖放 `db_007`。运行断言确认 `db_002`、`tk_006` 进入弃牌堆，红石不扣（能量保持 6/6），沙漠神殿以 10/10 部署并占用连续两个建筑格，`tk_007`、`tk_008` 留在牌库且计入掩埋数量。
- Player 首轮验收发现拖动开始时重复选择同一手牌实例会把 `CRAFTING` 支付重置为 `REDSTONE`，造成合成成功但错误扣费。现仅在切换到另一手牌实例时重置支付方式；同一实例的拖动重选保持当前支付，并加入场景回归覆盖两种路径。
- Unity CLI `1.0.0-beta.8` 全量 EditMode **335/335 Passed**：`Temp/GoalContinue/CraftingInteraction/editmode-final.xml`。CLI 使用 Editor `6000.0.28f1c1` 构建 Development Player；两张截图都通过进程退出码、玩法日志、630 项源码清单和 PNG 尺寸验证。1280×720 SHA-256 `022D0A69951D0162AC698A0CC05258534B607AD61944BAF981894A64C5FE3C44`：[crafting-settled-1280.png](../../Temp/GoalContinue/CraftingInteraction/crafting-settled-1280.png)；1920×1080 SHA-256 `60F39E34CC3116BF5DC33C6003610951FDE73BAC525356D13E15B103AFFABDC0`：[crafting-settled-1920.png](../../Temp/GoalContinue/CraftingInteraction/crafting-settled-1920.png)。
- 此为确定性离线 Player 交互验收，不替代 Docker/Nakama 双客户端权威逻辑与隐私验证。

## 2026-10-02 UI-047 详情卡规则文字可读性与面板净空

- 实机满手画面中，右侧详情卡虽保留了完整规则，但 1280×720 下长规则字仅 10px，扫读困难。详情卡从 250×350 扩为 250×430 并上移，标题同步上移；部署、支付、选目标与状态提示整体下移 20 设计单位，避免卡片扩展侵占交互区。详情规则最小屏幕字号从 10px 提高到 12px，手牌紧凑预览维持原字号/省略逻辑。
- Unity EditMode 新增/扩展全部 74 张注册卡规则全文容纳检查、详情卡与标题/部署提示净空，以及合成支付按钮与卡面/提示的非重叠断言。Unity CLI `1.0.0-beta.8` / Editor `6000.0.28f1c1` 全量 EditMode **335/335 Passed**（`Temp/GoalContinue/DetailRulesReadability/layout-r3-editmode.xml`）；Windows Development Player 构建成功，源码清单由截图脚本复核。
- Player `-PreviewFullHand` 1280×720 SHA-256 `28BFDD2EA017F1388C59620DC8F01F4DC7D5E86B9EEEB58085EBE7B55D9655FE`：[final-full-hand-1280.png](../../Temp/GoalContinue/DetailRulesReadability/final-full-hand-1280.png)；1920×1080 SHA-256 `B4288D54982AE3574984F086BD3FA2F8EF591F99BD34D931BAEF13ED06E6F5E7`：[final-full-hand-1920.png](../../Temp/GoalContinue/DetailRulesReadability/final-full-hand-1920.png)。两档均验证玩家进程、预期日志、630 项源码清单与 PNG 尺寸；实图确认规则文字、标题、手牌和操作区互不遮挡。
- 另以同一 Player 实际走完 `-PreviewEndReturnInteraction`：卡牌回手、费用 3→2 和单位格清空均满足运行断言；1280×720 SHA-256 `5673A6F6D9FA995C10AB0A1B80B9101D91CF44C019027FC40E577093AF8DF239`：[final-end-return-1280.png](../../Temp/GoalContinue/DetailRulesReadability/final-end-return-1280.png)。真实选牌/目标/说明 UI 在扩高后仍能同屏读清且未遮挡。

## 2026-10-02 UI-046 满手与详情面板净空

- 满手扇形实图复核确认，七张手牌最右投影曾越过详情面板左边缘 1 个设计单位。为保留卡片间距和 16:9 视口，在不缩小 250×350 详情卡的前提下，将 `CardDetailsPanel` 容器调整为 286×715、中心位置 x=803；内容区仍可容纳详情卡，面板右缘留有画布安全区。
- 扩展 `SevenCardHandLayoutKeepsEveryCardInsideStoneHandPlate`：按实际 Canvas 世界角点验证末张手牌与详情面板至少 8 个设计单位净空，且面板距设计画布右边至少 8 单位。初次断言暴露原布局为 1 个设计单位侵入；调整后 Unity CLI EditMode **335/335 Passed**（`Temp/GoalContinue/MainUiAudit/inspector-clearance-final-editmode.xml`）。
- `scripts/validate.ps1 -WithUnity -WithDockerConfig` **44.8 秒全链通过**：74 张卡牌与 Minecraft 资源来源/漂移控制、服务端 **234/234**、TypeScript 检查/构建、Unity EditMode **335/335** 与 Compose 静态配置。
- 当前源码 Windows Development Player `-PreviewFullHand` 在 1280×720 与 1920×1080 均通过退出码、630 项源码清单、运行日志及 PNG 尺寸验证。SHA-256：1280 `CC514DA3F39F6F32C4B0CB516CA8FF264980F9F66DFA2B0274D39F893A939EF5`：[full-hand-1280.png](../../Temp/GoalContinue/InspectorGap/full-hand-1280.png)；1920 `7E0CDC331DF84C9B26298547CD58BCF404480D1DD6F2C3BC5442464D96EDDB45`：[full-hand-1920.png](../../Temp/GoalContinue/InspectorGap/full-hand-1920.png)。实图确认卡名、费用、详情规则和操作区均可见，手牌不再贴到详情容器。

## 2026-10-02 UI-045 手牌标题避让费用徽章

- 主行动满手 Player 截图审查发现，卡牌标题 RectTransform 横向覆盖费用徽章区域；短卡名不明显，末地长卡名存在压住费用数字/徽章的风险。现在紧凑手牌和详情卡的标题矩形都从费用徽章右缘留出明确间距，并限制在卡框右内缘；标题字号分别自适应 14–15、18–20 个设计单位，完整显示卡名且不侵占徽章。
- 新增 Unity EditMode `RegisteredCardTitlesAvoidCostSocketsAndFitCompactAndDetailFrames`，逐张验证 **74 张注册卡**在 166×216 手牌与 250×350 详情卡下标题安全距离、完整本地化文本和最小字号的行高；全量 EditMode **335/335 Passed**（`Temp/GoalContinue/MainUiAudit/editmode.xml`）。`-previewFullHand` 改用七张末地卡（含最长注册标题“末地传送门框架”），运行时继续断言所有牌位于石砖托盘内。
- Unity CLI `1.0.0-beta.8` / Editor `6000.0.28f1c1` 构建 Windows Development Player 后，`capture-demo-preview.ps1 -PreviewFullHand` 两档运行、630 项源码清单、日志及 PNG 尺寸检查通过。1280×720 SHA-256 `C204D018856A9EEBADBBB2BD6D5B85916774B55A3D4D64EADE4614B6E813B70D`：[full-hand-after-1280.png](../../Temp/GoalContinue/MainUiAudit/full-hand-after-1280.png)；1920×1080 SHA-256 `BA3B74EDFE482E487D131F15BF510AA3AD5D12C5FDFFFB6A98BA1DE53387F8CE`：[full-hand-after-1920.png](../../Temp/GoalContinue/MainUiAudit/full-hand-after-1920.png)。

## 2026-09-29 QA-043 Unity CLI 与 Editor GUI 诊断

- 核对本机 Unity CLI `1.0.0-beta.8` 的 `test/build/run/open/license` 参数，并将可复现命令补入 `client-unity/README.md`。`scripts/validate.ps1 -WithUnity -WithDockerConfig` 当前工作区 **44.4 秒全链通过**：74 张卡牌及 Minecraft 来源/漂移控制、服务端 **234/234**、TypeScript 检查/构建、Unity `6000.0.28f1c1` EditMode **334/334** 与 Compose 静态配置。
- 通过 `unity open .\client-unity --editor-path 'D:\Unity 6000.0.28f1c1\Editor\Unity.exe'` 实际启动图形 Editor；`unity editors running` 确认项目、版本和进程，Editor.log 记录项目在 **14.2 秒**完成加载，脚本/程序集编译无错误。CLI 许可证状态为 active，当前未登录 Unity 账户；Editor 仍成功更新并解析本机许可证。日志中唯一持续网络错误是 Unity Editor 更新检查 URL 返回 404，随后回退，不影响项目加载。未重置布局、删除缓存或更改许可/账户状态。
- 截图脚本以当前 Windows Development Player 实际启动 `-PreviewCombatInteraction`，退出码、确定性战斗日志、630 项来源清单及 1280×720 PNG 检查通过；SHA-256 `292DAF1D1747BDC3649042231B3A635268D2663336E31DF2B1AAFFDCB2102292`：[combat-1280x720.png](../../Temp/GoalContinue/CliRunbookValidation/combat-1280x720.png)。真实 Docker/Nakama 双端验收仍不可运行：`dockerDesktopLinuxEngine` 与 `docker_engine` named pipe 均不存在。

## 2026-09-29 QA-042 Unity Player 基础对局交互复核

- 使用当前 Windows Development Player 对两条离线玩法链做双分辨率实测，确认 UI-043/044 的手牌托盘、状态面板变化没有影响基本操作。`-PreviewCombatInteraction` 经 UI/3D 射线完成无效落点原子保留、手牌拖放部署、回合阶段切换、攻击者/目标选择及反击结算，最终第 2 回合、能量 7、攻击者生命 1；`-PreviewEndReturnInteraction` 经真实场景回调完成末地回手，费用修正为 2、目标格清空。
- 四次 Player 运行均正常退出，日志达到预期结算断言，截图与 Unity 构建记录的 **630 项源码清单**一致；1280×720、1920×1080 两档均通过 PNG 尺寸校验。战斗 SHA-256：1280 `818F0D4F6F2294326B04A80742E2C8DF8895BBE793E152D17E7B7590FEF96AAF`、1920 `55E22686018D9505FF0B6A23F254336A9A59502667D478B3FFDEC3BE6B1E35B8`；回手 SHA-256：1280 `FC86084C7DA01CA6C56CB6A9FA5F2CBC1F71E073E8FE804C650F847F2124A477`、1920 `E9AFB9C9EA5F2936C6B32C66C256B0BC2C0AFDDDEDCB819B0B236426C4BAA89F`。截图、日志与 JSON 报告在 `Temp/GoalContinue/BasicMatchInteractionReview/`。
- 这只是本地规则/表现与 Player 输入链的证据，不替代双客户端权威服务器验收；Docker 两个 context 的 Engine named pipe 当前均不存在。

## 2026-09-29 UI-044 满手状态条避让

- 满手 Player 实图发现右下状态面板遮住最右侧卡牌。状态面板从 315×94 调整为 260×94 并右移，文本区域同步收窄；在设计画幅右边保留 20 单位边距，与第七张卡之间留出净空。状态文案继续自动换行，最小字号保持 16。
- 扩展 `SevenCardHandLayoutKeepsEveryCardInsideStoneHandPlate`：检查状态牌不与任何卡牌投影边界重叠、画幅内边距不少于 8，并用部署失败/联机等待/布局提示文案验证最小字号下不截断。Player 1280×720 SHA-256 `97B09106A286E24FDC968E43812D030B3C842F6D2B12FFC3B3CD5EEA9591C6A4`：[full-hand-1280x720.png](../../Temp/GoalContinue/StatusPlateHandClearance/full-hand-1280x720.png)；1920×1080 SHA-256 `BE50D2456104623817B7F3B313B6A9C60D94BF1C9EBAEDF8DB9F8E4972C30720`：[full-hand-1920x1080.png](../../Temp/GoalContinue/StatusPlateHandClearance/full-hand-1920x1080.png)。两张截图均通过 Player 日志、源码清单和 PNG 画幅验收。随后运行 `scripts/validate.ps1 -WithUnity -WithDockerConfig`，**186.3 秒全链通过**：卡牌与 Minecraft 素材来源/漂移控制、服务端 **234/234**、TypeScript 检查/构建、Unity CLI **334/334** 和 Compose 静态配置；该命令没有启动 Docker，故不代表 Nakama 在线 smoke。
- 可复现 Unity CLI：`unity test .\client-unity --mode EditMode --timeout 900 --editor-path "D:\Unity 6000.0.28f1c1\Editor\Unity.exe" --output .\Temp\GoalContinue\StatusPlateHandClearance\full-editmode.xml`；Player 构建和满手截图命令同 UI-043，输出目录为 `Temp/GoalContinue/StatusPlateHandClearance/`。

## 2026-09-29 UI-043 七张手牌展开与避让

- Unity CLI Player 实图发现满手时卡牌扇形投影相互压住文字，左侧能量面板还会盖住第一张牌。现在卡牌按外侧旋转后的实际包围宽度计算共同间距；七张牌采用约 183 个设计单位间距、最大 1.5°旋转，边缘牌上抬形成浅弧线。能量面板压缩至 150×34 并下移；手牌绘制区上移，确保底部能量值完整且不遮挡卡面。
- `SevenCardHandLayoutKeepsEveryCardInsideStoneHandPlate` 验证七张 166×216 卡片均在石砖托盘 24 单位安全边界内、投影相邻间距至少 8 单位，且卡牌不与能量面板相交；满手预览在 1280×720 和 1920×1080 Development Player 均通过运行日志、源码哈希清单、退出码及 PNG 画幅检查。全量 Unity CLI EditMode **334/334 Passed**。
- 1280×720 SHA-256 `DAF98710285F934CCF6769CBD4542E06131D37FC446D8CB8A8037E4E4C3B93AB`：[full-hand-1280x720.png](../../Temp/GoalContinue/HandFanReadabilityFinal2/full-hand-1280x720.png)；1920×1080 SHA-256 `372E1D320778542B1DF3F90DDAAD581574AB24393DB04D78CB7F81658865FC1F`：[full-hand-1920x1080.png](../../Temp/GoalContinue/HandFanReadabilityFinal2/full-hand-1920x1080.png)。
- 可复现：`unity test .\client-unity --mode EditMode --timeout 900 --editor-path "D:\Unity 6000.0.28f1c1\Editor\Unity.exe" --output .\Temp\GoalContinue\HandFanReadabilityR3\full-editmode.xml`；使用 `unity build` 构建 `Temp/GoalContinue/HandFanReadabilityR3/Build/BiomeRivalsDemo.exe`，再执行 `scripts/capture-demo-preview.ps1 -PreviewFullHand` 并分别指定 1280×720 / 1920×1080 与独立截图路径。

## 2026-09-29 UI-042 卡牌详情规则可读性

- 1280×720 Player 检查显示，右侧详情卡的短规则字号偏保守。卡面宽度由 238 增至 250（保持原高度、垂直位置、标题及操作区间距）；规则字号上限由 14px 提至 15px，详情长文本最低仍保留 10px，避免为了字号导致规则截断。
- 新增 `EveryRegisteredDetailCardRuleFitsAt1280ReadableMinimum`，用 Unity `TextGenerator` 按 1280×720 的 2/3 Canvas 比例逐一检查 74 张注册卡的完整详情规则，全部在 10px 屏幕字号下限内完整容纳。布局断言覆盖卡面尺寸/位置及卡面与标题分离。全量 `scripts/validate.ps1 -WithUnity -WithDockerConfig` **58.2 秒通过**：服务端 **234/234**、TypeScript 与构建、Unity `6000.0.28f1c1` EditMode **333/333**、卡牌/资产注册门禁和 Compose 静态配置。
- 当前源码 Windows Development Player 构建成功；`-PreviewEndReturnInteraction` 在 1280×720 SHA-256 `9B481DE8A1307C356F1C76E5892D3B31667C947F0B4EDC4C57C9925E0ECA5F32`：[final-1280x720.png](../../Temp/GoalContinue/CardDetailRulesAudit/final-1280x720.png)；1920×1080 SHA-256 `DE4E07D022D6348D14461E1B82BFFB2930C8194B47C34BB5BDE0CF3ABC7DE89E`：[final-1920x1080.png](../../Temp/GoalContinue/CardDetailRulesAudit/final-1920x1080.png)。Player 退出码、预期交互日志、PNG 画幅及当前 630 项源码清单均通过；实图确认标题可见、详情卡未侵入操作区，回手状态仍可读。
- 可复现构建：`unity build client-unity --target StandaloneWindows64 --execute-method BiomeRivals.Demo.Editor.DemoBuildAutomation.BuildWindowsFromCommandLine --output-path Temp/GoalContinue/CardDetailRulesAudit/Build/BiomeRivalsDemo.exe --editor-path "D:\Unity 6000.0.28f1c1\Editor\Unity.exe" --allow-dirty-build --timeout 1200 --no-tail`；捕获使用 `scripts/capture-demo-preview.ps1 -PreviewEndReturnInteraction -PlayerFaction end -OpponentFaction nether`，分别设置画幅及独立 `-CapturePath`。

## 2026-09-29 RULE-033C 当前工作区离线复核

- 对当前未提交工作区运行 `scripts/validate.ps1 -WithUnity -WithDockerConfig`，全链 **48.4 秒通过**：卡牌内容/资源来源注册校验，服务端 **234/234**、TypeScript 类型检查和构建，Unity `6000.0.28f1c1` EditMode **332/332**，以及 Docker Compose 静态配置。Unity 报告为 `Temp/RULE-033B-validation-editmode.xml`。
- 由当前源码构建 Windows Development Player（Unity CLI `1.0.0-beta.8`，dirty revision `c942129`），运行 `-PreviewEndReturnInteraction`，实际经 UI GraphicRaycaster、PointerDown/Up/Click 与 3D 战场射线完成回手；预期的费用 `-1` 与格位清空均由 Player 日志断言。1920×1080 PNG 与当前 630 项源码清单、Player 退出码和运行日志校验通过，SHA-256 `D3FE3508B2746716A699C1B6E98F33113C14AD87F8B8C249352C9D4E7D968D49`：[end-return-1920x1080.png](../../Temp/GoalContinue/CurrentWorktreeReview/end-return-1920x1080.png)；1280×720 SHA-256 `50A099B19BC652568A8EFC5777614F7CD797342DC7428992539C8D92B9393EB2`：[end-return-1280x720.png](../../Temp/GoalContinue/CurrentWorktreeReview/end-return-1280x720.png)。双分辨率检查了卡牌详情、结束回合/回手提示与末地棋盘布局；未启动 Docker，因此本证据不替代 RULE-033C 双端在线验收。
- 可复现 Player 构建：`unity build client-unity --target StandaloneWindows64 --execute-method BiomeRivals.Demo.Editor.DemoBuildAutomation.BuildWindowsFromCommandLine --output-path Temp/GoalContinue/CurrentWorktreeReview/BiomeRivalsDemo.exe --editor-path "D:\Unity 6000.0.28f1c1\Editor\Unity.exe" --allow-dirty-build --timeout 1200 --no-tail`；随后运行 `scripts/capture-demo-preview.ps1 -ExecutablePath Temp/GoalContinue/CurrentWorktreeReview/BiomeRivalsDemo.exe -ProjectPath client-unity -PreviewEndReturnInteraction -PlayerFaction end -OpponentFaction nether -CaptureWidth 1920 -CaptureHeight 1080 -CapturePath Temp/GoalContinue/CurrentWorktreeReview/end-return-1920x1080.png`。

## 2026-09-29 UI-041 战场单位铭牌可读性

- Player 实图显示 1280×720 下战场单位名称与攻/血标签字号偏小。字号由 12 提至 15，并启用 12–15 的 Best Fit；常规铭牌高度由 30 提至 38，含多行修饰的铭牌高度由 54 提至 64，文本内边距同步调整。长状态仍可缩字适配，普通单位保持更醒目的名称与数值。
- `GeneratedSceneAndRuntimeHierarchyExist` 增加常规铭牌断言，覆盖 15 号字号、自适应范围与最小垂直空间；新增 `PolarBearWoolPreviewConsumesOneExactInstanceAndRendersTheThreeLineModifierNameplate`，实际结算同名羊毛中的精确副本、北极熊 4/7、嘲讽及三行铭牌。随后新增 `DarknessPreviewUsesTwoExactSandInstancesBeforeSelectingSnowballTarget`，覆盖两张同名可疑沙子精确消费后进入 DARK 回合，并选择准确的雪球实例进入目标选择。无筛选 Unity CLI EditMode 全量 **332/332 Passed** 并正常退出（`Temp/GoalContinue/PreviewHandInstanceAudit/full-editmode.xml`）。额外复核发现 `unity test --filter ...` 无论命中场景测试还是轻量纯逻辑测试，都会生成通过 XML、Editor 日志记录 `Shut down`，但 CLI 仍等到 timeout 后终止进程；因此本项目当前以无筛选全量命令作为可靠回归入口，过滤模式异常单独跟踪。`unity build` Windows Development Player 构建成功，源码清单 630 项。实际 Player 预览均由日志、退出码、当前构建源码清单及 PNG 画幅校验：北极熊+羊毛 1280×720 SHA-256 `BD26D755408598DF8B7DC9FE3ECB36CC60247D00E0453BBCF8C95FF709839AF1`、1920×1080 `A886DA5DE5A7E86775DE75FFD2969DCE5A98FDCA38772D09A4452745923F73B1`；DARK 目标选择 1280×720 `1454F767C0A4ED7A42146BDB482EB49A37896117C7C195A298FB07341AD24BCB`、1920×1080 `1B1C3266E0F998573DB8C17EF2FC91D1F461C6879A2262CDC7DCB0793667BA7F`。截图与日志在 `Temp/GoalContinue/PreviewHandInstanceAudit/`。实图确认铭牌不遮挡模型或格位、右侧详情不残留过期卡牌选择；DARK 预览只允许敌方单位行左右端格为雪球目标，中间目标保持不可交互。以上仅验证离线对局，不替代 Docker/Nakama 联机验收。
- 可复现：可靠全量回归为 `unity test .\client-unity --mode EditMode --timeout 900 --editor-path "D:\Unity 6000.0.28f1c1\Editor\Unity.exe" --output .\Temp\GoalContinue\PreviewHandInstanceAudit\full-editmode.xml`；Player 为 `Temp/GoalContinue/PreviewHandInstanceAudit/Build/BiomeRivalsDemo.exe`；截图脚本 `scripts/capture-demo-preview.ps1` 使用 `-PreviewPolarBearWool` 或 `-PreviewDarknessTargeting`，通过 `-CaptureWidth`、`-CaptureHeight`、`-CapturePath` 指定画幅和输出。

## 2026-09-29 UI-040 顶部账户/匹配状态条排版与可读性

- Player 实图发现顶部状态条右侧“匹配”按钮侵入回合状态栏，且账户/卡组文字在 1280×720 下过小。将状态条压缩并定位到标题与回合条之间的独立横向槽位：1920 设计分辨率下其边界为 x=259..545，标题右边界 x=255、回合条左边界 x=560；账户/卡组字号提升到 15/14，连接状态提高到 15 号自适配（14–15），匹配按钮为 14。
- `GeneratedSceneAndRuntimeHierarchyExist` 锁定面板锚点、尺寸、相邻标题/回合面板间距及字号。Unity CLI `1.0.0-beta.8` / Editor `6000.0.28f1c1` 全量 EditMode **330/330 Passed**（`Temp/GoalContinue/online-status-min14-full.xml`）；Windows Development Player 构建成功。基于当前 630 项源码清单，默认本地场景 Player 的退出码、PNG 与画幅检查均通过：1280×720 SHA-256 `5BFC41054FA5262C19751966F640466630FAA40012932971CAF1E7A9547E5814`：[default-1280x720.png](../../Temp/GoalContinue/OnlineStatusMinimumReadable/default-1280x720.png)；1920×1080 SHA-256 `AFBA676BD03863F6626001CC4EC3E27BCE60E9614F6A67F0CDE7F1E4A885B886`：[default-1920x1080.png](../../Temp/GoalContinue/OnlineStatusMinimumReadable/default-1920x1080.png)。
- 为检查长状态的实际显示，`capture-demo-preview.ps1 -PreviewOnlineStatus` 会在 Player 中呈现“正在重连 · 第 999 次”，并校验预览日志、源码清单、退出码及 PNG。1280×720 SHA-256 `273018A33336A42EBD73C3B8BD70CBF7C6D42A15D465742B8CA5C01645C20FE9`：[reconnect-999-1280x720.png](../../Temp/GoalContinue/OnlineStatusMinimumReadable/reconnect-999-1280x720.png)；1920×1080 SHA-256 `0A5AFA0BED5B109CD48B4F502320479D6E60613EF5863FDD131B9918EF660B30`：[reconnect-999-1920x1080.png](../../Temp/GoalContinue/OnlineStatusMinimumReadable/reconnect-999-1920x1080.png)。同一 Player 还通过 Combat Interaction 完成部署、回合切换与攻击；1280×720 SHA-256 `862D81489FDA5E38443FF658BF09AFC7B3E9CC76E80B62E611511B2EE61B779F`：[combat-1280x720.png](../../Temp/GoalContinue/OnlineStatusMinimumReadable/combat-1280x720.png)。三张截图均按当前 630 项源码清单成功校验、Player 退出码为 0；只验证离线流程，不代替 Docker/Nakama 联机验收。
- EditMode 以 Unity `TextGenerator` 测量对局搜索、999 次重连、版本不兼容和底座未启动文案，确认在 14 号最小字号及 88×58 设计区域内可完整换行、不垂直截断。专项 **1/1 Passed**（`Temp/GoalContinue/online-status-readable-min14.xml`）；全量 EditMode **330/330 Passed**（`Temp/GoalContinue/online-status-min14-full.xml`）。
- 可复现：`unity test client-unity --mode EditMode --output Temp/GoalContinue/online-status-min14-full.xml --editor-path "D:\Unity 6000.0.28f1c1\Editor\Unity.exe" --timeout 900`；构建使用 `unity build client-unity --target StandaloneWindows64 --execute-method BiomeRivals.Demo.Editor.DemoBuildAutomation.BuildWindowsFromCommandLine --output-path Temp/GoalContinue/OnlineStatusMinimumReadable/BiomeRivalsDemo.exe --editor-path "D:\Unity 6000.0.28f1c1\Editor\Unity.exe" --allow-dirty-build --timeout 1200 --no-tail`；对局连接状态预览使用 `scripts/capture-demo-preview.ps1 -PreviewOnlineStatus`，并按画幅分别指定输出路径。

## 2026-09-29 UI-039 pending 期间暂停战场指针并恢复悬停

- 联机命令待确认时，格位 Button 已禁用，但 3D 指针仍可能保留地表 Hover/Pressed 高亮，让玩家误以为棋盘仍可操作。现在指针控制器会在命令发出时清理并暂停射线输入，命令收束或连接会话释放时恢复；恢复后的下一帧按光标当前位置重新计算悬停。格位回调另有 pending 防线，已排队事件不能重建高亮。
- 扩展在线输入集成测试，实际对 3D 格位射线执行 hover/press，检查 pending 时指针状态清空、输入被拒绝，服务端拒绝后同一光标位置重新命中并恢复高亮。Unity CLI 专项 **1/1 Passed**，全量 EditMode **330/330 Passed**（`Temp/pending-online-pointer-lock-full.xml`）；Unity 6 Windows Development Player 构建成功。实际 Combat Interaction Player 路径完成格位射线/回合交互检查并验证 630 项源码清单、进程退出码和 PNG：1280×720 SHA-256 `39BF5FDF7074E793A313FC8A50CD4BF793FDD92B7A8C4703B51CBF05D30ED7F9`：[combat-interaction-1280x720.png](../../Temp/GoalContinue/PendingPointerLock/combat-interaction-1280x720.png)。该画面用于确认战斗交互未被改坏，不代表截取到了在线 pending 状态；真实 Docker/Nakama 双端 smoke 仍受本机 Docker Engine named pipe 缺失影响。
- 可复现：`unity test client-unity --mode EditMode --filter PendingOnlineCommandDisablesHeroAndHandInputsUntilRejection --output Temp/pending-online-pointer-lock.xml --timeout 300`；全量测试输出 `Temp/pending-online-pointer-lock-full.xml`。Player 构建位于 `Temp/GoalContinue/PendingPointerLock/BiomeRivalsDemo.exe`，Combat Interaction 截图命令为 `scripts/capture-demo-preview.ps1 -ExecutablePath Temp/GoalContinue/PendingPointerLock/BiomeRivalsDemo.exe -ProjectPath client-unity -PreviewCombatInteraction -PlayerFaction plains_forest -OpponentFaction desert_badlands -CaptureWidth 1280 -CaptureHeight 720 -CapturePath Temp/GoalContinue/PendingPointerLock/combat-interaction-1280x720.png`。

## 2026-09-29 UI-038 联机命令待确认时的回调级输入防线

- UI-036 已禁用可见控件并阻止取消操作，但代码复核发现已排队或直接触发的 UI 回调仍可能绕过控件状态：例如手牌回调覆盖当前选择、结束回合先清除暂存目标再尝试提交第二条命令。现在手牌/支付选择、战场格、英雄攻击、施法、多目标确认、拖放与结束回合入口均在修改状态或发命令前检查 pending 状态；本地对局行为不变。
- 扩展 `PendingOnlineCommandDisablesHeroAndHandInputsUntilRejection`，直接调用排队回调，验证手牌实例、支付方式和目标选择保持不变，且结束回合不会提交第二条命令。Unity CLI 目标回归 **1/1 Passed**，主工程全量 EditMode **330/330 Passed**（`Temp/pending-online-callback-guard-full.xml`）；Unity 6 Windows Development Player 构建成功：`Temp/GoalContinue/PendingCallbackGuard/BiomeRivalsDemo.exe`。这是回调边界的离线集成验证，不替代真实 Docker/Nakama 延迟与拒绝的双端验收；无画面变更，因此本项未生成新截图。
- 可复现：`unity test client-unity --mode EditMode --filter PendingOnlineCommandDisablesHeroAndHandInputsUntilRejection --output Temp/pending-online-callback-guard.xml --timeout 300`；全量测试输出 `Temp/pending-online-callback-guard-full.xml`。构建使用 `unity build client-unity --target StandaloneWindows64 --execute-method BiomeRivals.Demo.Editor.DemoBuildAutomation.BuildWindowsFromCommandLine --output-path Temp/GoalContinue/PendingCallbackGuard/BiomeRivalsDemo.exe --editor-path "D:\Unity 6000.0.28f1c1\Editor\Unity.exe" --allow-dirty-build --timeout 1200 --no-tail`。

## 2026-09-29 UI-037 英雄攻击控件按合法战斗状态启用

- 主行动阶段的己方英雄 HUD 与敌方英雄目标之前仍显示可点击，但回调会直接返回；战斗阶段里敌方英雄目标在未选攻击者时也显示启用。现在己方英雄只在当前回合的战斗阶段、装备可攻击武器且本回合未攻击时启用；敌方英雄目标还要求已选择且可攻击的攻击者，并通过嘲讽/目标规则校验。命令 pending 门禁仍优先生效。
- Unity EditMode 新增 HUD 状态回归，覆盖主阶段关闭与 0.78 淡化、战斗阶段可选英雄、选中合法攻击者后敌方英雄目标恢复亮度、命令待确认时再次淡化、拒绝后恢复。主工程全量 EditMode **330/330 Passed**（`Temp/GoalContinue/hero-attack-controls-full-final.xml`）；Windows Development Player 构建成功，1280×720 默认场景通过 630 项源码清单、进程退出码及 PNG 校验，SHA-256 `A03B8137C5BB141C02BC175AC1DD74856DC80303D87F6DECAFDBAD9828987AFE`：[hero-controls-main-phase-final-1280.png](../../Temp/GoalContinue/hero-controls-main-phase-final-1280.png)。实图确认不可攻击阶段 HUD 轻微淡化，名称、生命及阵营图标仍可辨认。
- 可复现：`unity test client-unity --mode EditMode --filter HeroAttackControlsFollowCombatLegalityAndPendingState --output Temp/GoalContinue/hero-attack-controls-alpha.xml --editor-path "D:\Unity 6000.0.28f1c1\Editor\Unity.exe" --timeout 240`；全量测试 `Temp/GoalContinue/hero-attack-controls-full-final.xml`。Player 构建输出为 `Temp/GoalContinue/HeroAttackControlsFinal/BiomeRivalsDemo.exe`。

## 2026-09-29 UI-036 联机命令待确认时锁定游戏交互

- 复核联机命令等待服务器确认期间的 UI 状态时发现：英雄 HUD 可点击，Esc/右键也能清除本地暂存目标。现在双方英雄按钮、目标取消按钮、键盘/右键取消及可选战吼清除均遵守 pending 锁；本地模式行为不变，命令收束后恢复操作。
- 控制器级 EditMode 回归通过假权威网关发起命令，验证英雄控件、手牌及回合按钮一起禁用，目标取消按钮置灰、取消回调不改暂存目标，回合控件显示“等待服务器”；模拟服务端拒绝后确认输入恢复且暂存目标可以取消。Unity CLI `1.0.0-beta.8` / Editor `6000.0.28f1c1` 主工程全量 EditMode **329/329 Passed**（`Temp/GoalContinue/ui-command-pending-cancel-full.xml`），Windows Development Player 构建成功（`Temp/GoalContinue/PendingCancelLock/BiomeRivalsDemo.exe`）。这是本地状态与 UI 集成验收，不替代真实 Docker/Nakama 双端在线验证。
- 可复现：`unity test client-unity --mode EditMode --filter PendingOnlineCommandDisablesHeroAndHandInputsUntilRejection --output Temp/GoalContinue/pending-online-cancel-lock-r3.xml --editor-path "D:\Unity 6000.0.28f1c1\Editor\Unity.exe" --timeout 240`；全量报告为 `Temp/GoalContinue/ui-command-pending-cancel-full.xml`。构建使用 `unity build client-unity --target StandaloneWindows64 --execute-method BiomeRivals.Demo.Editor.DemoBuildAutomation.BuildWindowsFromCommandLine --output-path Temp/GoalContinue/PendingCancelLock/BiomeRivalsDemo.exe --editor-path "D:\Unity 6000.0.28f1c1\Editor\Unity.exe" --allow-dirty-build --timeout 1200 --no-tail`。

## 2026-09-29 UI-035 单/多卡牌库选择聚焦与规则可读性

- Player 实图复核发现三张考古选项在 1280×720 下规则文本过于紧凑。`TOP_CARD_SCRY` 单卡现在使用 860×790 聚焦面板；`ARCHAEOLOGY_TOP_3` 使用 1080×790 比较面板。两类选项卡面统一为 238×342，三卡用 254×452 单槽和 260 间距；卡牌显示完整注册规则，不再使用手牌紧凑版的省略预览。
- 面板动态调整会同步九切边框、平铺石砖、内 bevel、铆钉和卡牌/状态/确认控件，避免皮肤残影与文字覆盖。观察者端单卡仍只呈现隐藏牌背，不实例化卡面或交互按钮。
- EditMode 覆盖单卡/三卡面板与卡面尺寸、完整规则绑定、选项按钮、材质子节点及对手单卡牌背。Unity CLI `1.0.0-beta.8` / Editor `6000.0.28f1c1` 主工程全量 EditMode **328/328 Passed**（`Temp/GoalContinue/unity-choice-full-rules-full.xml`）；Windows Development Player 构建成功，单卡与三卡考古均由 `GraphicRaycaster` 实际选择，并保持待确认。截图脚本检查当前 630 项源码清单、预期日志、退出码与画幅。单卡 1280×720 SHA-256 `58771CB204DB10904D41F60DA141B36B0B0C48BCF18DA1EA5A96B2521D675326`：[card-choice-full-rules-r5-1280x720.png](../../Temp/GoalContinue/card-choice-full-rules-r5-1280x720.png)；1920×1080 SHA-256 `737A5F1739334DDA4BF2876EC4AEFEA2D32742B75F77AA8D39456738D9DE48F0`：[card-choice-full-rules-r5-1920x1080.png](../../Temp/GoalContinue/card-choice-full-rules-r5-1920x1080.png)。三卡 1280×720 SHA-256 `D758F2C0019A505CEF853CB397E477867EAA81FEC20918E8DEA2D5FCBFDC7C02`：[archaeology-full-rules-r5-1280x720.png](../../Temp/GoalContinue/archaeology-full-rules-r5-1280x720.png)；1920×1080 SHA-256 `1EF2D04FE0D52F02058E760D3FCD78AA6F06E2F9D916952D12904A6CD77C1524`：[archaeology-full-rules-r5-1920x1080.png](../../Temp/GoalContinue/archaeology-full-rules-r5-1920x1080.png)。这是离线 UI/Player 验收，不替代在线隐私双端或物理鼠标验收。
- 可复现：`unity test client-unity --mode EditMode --output Temp/GoalContinue/unity-choice-full-rules-full.xml --editor-path "D:\Unity 6000.0.28f1c1\Editor\Unity.exe" --timeout 900`；构建使用 `unity build client-unity --target StandaloneWindows64 --execute-method BiomeRivals.Demo.Editor.DemoBuildAutomation.BuildWindowsFromCommandLine --output-path Temp/GoalContinue/ChoiceLayoutPreview/BiomeRivalsDemo.exe --editor-path "D:\Unity 6000.0.28f1c1\Editor\Unity.exe" --allow-dirty-build --timeout 1200 --no-tail`。单卡/三卡分别运行 `scripts/capture-demo-preview.ps1 -PreviewChoiceInteraction -PlayerFaction cave_dark_forest -OpponentFaction plains_forest` 与 `scripts/capture-demo-preview.ps1 -PreviewArchaeologyChoice -PlayerFaction desert_badlands -OpponentFaction nether`，各自传入两档尺寸和独立路径。

## 2026-09-29 UI-034 对手牌库选择脱敏视图

- 补齐 `TOP_CARD_SCRY` 的 Unity 对手视角验收：当权威快照由对手持有选择、选项 `cardId` 已脱敏且 `selectable=false` 时，UI 只创建牌背与“牌库信息保密”提示，不实例化卡面、不提供可点按钮；pending choice 遮罩仍挡住战场输入。规则和快照格式未改。
- 新增 `OpponentTopCardChoiceRendersOnlyHiddenBacksAndWaitState` EditMode 回归，使用 Bob 视角的权威 `MatchStateStore` 快照驱动整套 Unity UI 层级。首次全量运行发现测试与其它 EditMode 用例共享的 `GameCompositionRoot.Instance` 会触发 Nakama `DontDestroyOnLoad`（仅 Play Mode 可用）；测试现会暂存/恢复该静态引用，避免初始化账户后端，且不改变运行时代码。修正后 Unity CLI `1.0.0-beta.8` / Editor `6000.0.28f1c1` 主工程全量 EditMode **327/327 Passed**（`Temp/GoalContinue/unity-private-choice-ui-full-r2.xml`）。服务端 `npm test` **234/234 Passed** 与 `npm run typecheck` 通过；既有 `Cave Bat offers one private top-card choice and moves it to the deck bottom` 覆盖服务端 owner/opponent 快照及事件投影脱敏。该组合是服务端规则投影 + Unity 快照/UI 消费测试，不替代 Docker/Nakama 真实双端在线验收。

## 2026-09-29 UI-033 牌库选择面板入场与真实 UI 选择

- 新的 `PendingChoice` ID 出现时，洞穴回声/考古选择遮罩播放 0.18 秒非线性淡入；遮罩从第一帧即拦截背景战场点击，但子按钮和选项在入场结束前不可交互。同一 choice ID 的刷新（例如选项重建）不重播动画；选择结束后遮罩隐藏并恢复透明度状态。未改动服务端规则、协议或选择隐私数据。
- 新增 EditMode 回归覆盖半透明阶段、遮罩挡射线、延迟开放子控件、同一选择刷新不重播，以及关闭后的状态复原。Unity CLI `1.0.0-beta.8` / Editor `6000.0.28f1c1` 全量 EditMode **326/326 Passed**（`Temp/GoalContinue/unity-card-choice-entrance-full.xml`）。确定性 Windows Development Player 实际触发洞穴蝙蝠 `TOP_CARD_SCRY`，通过 `GraphicRaycaster` 与 PointerDown/Up/Click 选择牌库顶牌；日志断言选择正确且 pending choice 仍存在。截图脚本同时校验 Player 退出码、来源清单、日志与画幅：1280×720 SHA-256 `3AF5BCEC8ACB5C71A7C0C84819EE98DC86697FCA35A7758861D6A10CD5F8BA59`：[card-choice-entrance-1280x720.png](../../Temp/GoalContinue/card-choice-entrance-1280x720.png)；1920×1080 SHA-256 `7B8712D524C144EAFBD413601B48513C6CDD3E28FFD576FF1EF1223A98AFB151`：[card-choice-entrance-1920x1080.png](../../Temp/GoalContinue/card-choice-entrance-1920x1080.png)。这是离线玩家视角验收；未声称覆盖真实双端隐私或物理鼠标。
- 可复现：`unity test Temp/AuraReturnLethalReview/Project --mode EditMode --output Temp/GoalContinue/unity-card-choice-entrance-full.xml --editor-path "D:\Unity 6000.0.28f1c1\Editor\Unity.exe" --timeout 900`；使用项目 `DemoBuildAutomation.BuildWindowsFromCommandLine` 构建后，以 `scripts/capture-demo-preview.ps1 -PreviewChoiceInteraction -PlayerFaction cave_dark_forest -OpponentFaction plains_forest` 分别指定截图尺寸和输出路径。

## 2026-09-29 UI-032 新增手牌入场反馈

- 手牌刷新仍会重建卡牌 UI，但现在按稳定的手牌实例 ID 与上次画面做差分；仅在原手牌至少保留一张时淡入新加入的牌。因此初始发牌、切换整副演示牌组或纯 UI 刷新不会让整手牌闪烁。动画采用 0.24 秒非线性透明度过渡，不改变卡牌位置/缩放，不干扰悬停和拖放；动画期间由 CanvasGroup 暂时阻断交互与射线，结束后恢复原状态。
- 新增 EditMode 用例锁定中间透明度及交互门控/恢复。Unity CLI `1.0.0-beta.8` / Editor `6000.0.28f1c1` 隔离工程全量 **325/325 Passed**（`Temp/GoalContinue/unity-card-arrival-r2.xml`）。Windows Development Player 的确定性抽牌预览还检查了“仅新实例动画、旧实例静止、过程可见且不可交互、结束后输入恢复”；截图脚本断言日志、当前源码清单、Player 退出码与画幅。1280×720 SHA-256 `7328989132C61F90481AEA450C274A39476726F7784CF4E88BAD2AB2598A273E`：[hand-arrival-1280x720.png](../../Temp/GoalContinue/hand-arrival-1280x720.png)；1920×1080 SHA-256 `4C767B8D1A878CDC4F4BE254EED37DA4EDF3D1733393B5C8416BADB48DBCE1DA`：[hand-arrival-1920x1080.png](../../Temp/GoalContinue/hand-arrival-1920x1080.png)。这是离线确定性 Player 验收，不替代在线发牌或物理鼠标测试。
- 可复现：`unity test Temp/AuraReturnLethalReview/Project --mode EditMode --output Temp/GoalContinue/unity-card-arrival-r2.xml --editor-path "D:\Unity 6000.0.28f1c1\Editor\Unity.exe" --timeout 900`；构建使用 `DemoBuildAutomation.BuildWindowsFromCommandLine`，再以 `scripts/capture-demo-preview.ps1 -PreviewCardArrival -PlayerFaction plains_forest -OpponentFaction nether` 分别传入两档画幅与独立截图路径。

## 2026-09-29 UI-031 结构牌建筑格射线命中

- 修复建筑格真实指针命中：建筑地表由偶数列像素网格构成，格子几何中心落在两列可见面片之间的缝隙，鼠标若正好悬停或点击缝隙会丢失 3D 射线目标。每格现在另有一张不可见、覆盖完整地表的 BoxCollider 命中面；画面仍保留原像素缝隙，射线命中时仍映射回原有阵营/类型/序号。首次构建完成后同步物理变换，保证碰撞面可立即查询。确定性 Player 将沙漠神殿拖到建筑格 2，断言其占建筑格 2–3、第 1 格保持空、费用只扣一次。
- Unity CLI `1.0.0-beta.8` / Editor `6000.0.28f1c1` 全量 EditMode **324/324 Passed**（`Temp/GoalContinue/unity-ground-footprint-pointer-final.xml`）；`GeneratedSceneAndRuntimeHierarchyExist` 检查完整建筑格碰撞面尺寸，从屏幕投射双方全部 **14 个单位/建筑格中心**并逐一核对格位身份，再通过真实指针控制器入口验证建筑格中心缝隙仍会触发地表悬停反馈。Windows Development Player 使用项目构建入口成功生成；截图脚本通过源码清单、运行日志、退出码和画幅校验：1280×720 SHA-256 `848BCF5252E251734AFE31AD866FCA968FD0C685CB99F4B4CD17358948E737B4`：[ground-footprint-pointer-final-1280x720.png](../../Temp/GoalContinue/ground-footprint-pointer-final-1280x720.png)；1920×1080 SHA-256 `7EDFFBA676A1FA1D7D341C8383CBF1DE7C352B1FFD464E6F66CA05CFDB5F4405`：[ground-footprint-pointer-final-1920x1080.png](../../Temp/GoalContinue/ground-footprint-pointer-final-1920x1080.png)。这是离线确定性 UI/3D 射线验收，非物理鼠标或在线双客户端测试；在线部署仍受 Docker Engine 未运行影响。
- Unity CLI 可复用流程：`unity test <隔离工程> --mode EditMode --output <报告.xml> --editor-path "D:\Unity 6000.0.28f1c1\Editor\Unity.exe"`；`unity build <隔离工程> --target StandaloneWindows64 --execute-method BiomeRivals.Demo.Editor.DemoBuildAutomation.BuildWindowsFromCommandLine --output-path <Player.exe> --editor-path "D:\Unity 6000.0.28f1c1\Editor\Unity.exe" --allow-dirty-build --no-tail`；之后调用 `scripts/capture-demo-preview.ps1 -PreviewStructureDragDeployment -PlayerFaction desert_badlands -OpponentFaction nether` 并设置两个画幅参数及各自输出路径。

## 2026-09-29 UI-030 手牌拖放部署

- 单位、建筑与结构牌现在可直接从手牌拖至战场格：拖动时卡牌抬到最前并暂时让出 UI 射线，战场 3D 指针控制器同步更新格位地表高亮；松手后仍由既有格位点击回调和部署规则结算。法术、材料不启用战场拖放；原“点选手牌→点战场格”的路径保持不变。无效落点预览现在实际派发 UI 拖动事件并检查牌实例仍在手、能量未变、目标格仍空；运行时手牌重建后等待一帧，避免 Unity 延迟销毁的旧 UI 同帧参与下一次射线。初始操作提示已说明两种部署方式。
- 增加 `DeployableCardDragTemporarilyPassesRaycastsAndRestoresItsHandPose` EditMode 回归，锁定拖动时 CanvasGroup 临时让出射线、跟随指针的位移回调，以及松手后恢复手牌位置/旋转/缩放/层级。更新源码清单后重新运行 Unity CLI `1.0.0-beta.8` / Editor `6000.0.28f1c1` 全量 EditMode **324/324 Passed**（`Temp/GoalContinue/unity-card-drag-lifecycle-full.xml`），Development Player 重建成功。同一确定性 Player 对局先用 UI 点击“放牧绵羊”并点格部署验证传统路径，再将另一张绵羊拖到无效位置，断言实例、能量和空格状态不变，然后拖放部署并完成回合与攻击。截图脚本对 Player 退出码、点击/无效拖放/合法部署与战斗完成日志、当前源码清单、PNG 尺寸和 SHA-256 全部校验通过：1280×720 `57532407548C46891D82FAD8BEC081F551DF4ECB004110A63534081F0E0B5F1A`：[card-drag-lifecycle-1280x720.png](../../Temp/GoalContinue/card-drag-lifecycle-1280x720.png)；1920×1080 `8173802BF460675396D9BAB2F20B6245702186E59D46827C31BED70CD9A6022F`：[card-drag-lifecycle-1920x1080.png](../../Temp/GoalContinue/card-drag-lifecycle-1920x1080.png)。这验证的是确定性 Player 事件派发，不是物理鼠标验收；在线双端部署 smoke 仍受 Docker Engine 不可用影响。
- 可复现：`unity test Temp/AuraReturnLethalReview/Project --mode EditMode --output Temp/GoalContinue/unity-card-drag-lifecycle-full.xml --editor-path "D:\Unity 6000.0.28f1c1\Editor\Unity.exe" --timeout 1200`；构建：`unity build Temp/AuraReturnLethalReview/Project --target StandaloneWindows64 --execute-method BiomeRivals.Demo.Editor.DemoBuildAutomation.BuildWindowsFromCommandLine --output-path Temp/GoalContinue/BuildCardDragLifecycleReview/BiomeRivals-card-drag-lifecycle.exe --editor-path "D:\Unity 6000.0.28f1c1\Editor\Unity.exe" --allow-dirty-build --timeout 1200 --no-tail --log-file Temp/GoalContinue/BuildCardDragLifecycleReview/editor-build.log`。之后用 `scripts/capture-demo-preview.ps1` 传入 `-PreviewCombatInteraction -PlayerFaction plains_forest -OpponentFaction desert_badlands`，分别设置两档分辨率与截图路径。

## 2026-09-29 UI-015/016 攻击反馈 Player 复核入口

- `capture-demo-preview.ps1` 新增 `-PreviewAttackFeedback`：用正式本地 `ResolveAttack` 路径结算铁傀儡攻击沙漠僵尸，并断言铁傀儡剩余 5 点生命、目标已阵亡且阶段仍为战斗；不再依赖旧的手工 Player 截图。运行日志保留伤害/反击文本，场景播放世界空间伤害数字、3D 地表受击反馈与攻击前冲。
- Unity CLI `1.0.0-beta.8` 驱动 Editor `6000.0.28f1c1`，隔离工程全量 EditMode **323/323 Passed**（`Temp/GoalContinue/unity-attack-feedback-preview-editmode.xml`）；项目 `DemoBuildAutomation.BuildWindowsFromCommandLine` Windows Development Player 构建成功。Player 截图脚本校验运行退出码、预期结算日志、当前 630 项来源清单、PNG 尺寸和 SHA-256：1280×720 `5DA17DA5A55A080A54F9E98BFFEB1B9ED684FC9BDBFAFEFEB0AE5BDFB2BC6BE1`：[attack-feedback-review-1280x720.png](../../Temp/GoalContinue/attack-feedback-review-1280x720.png)；1920×1080 `32D90613F5E51F30A9C8EB383B7C977A5A892F94AE06ED96FB3A9AE5AD640304`：[attack-feedback-review-1920.png](../../Temp/GoalContinue/attack-feedback-review-1920.png)。画面确认两侧伤害数字和目标受击/阵亡格定位符合状态；这仍是确定性离线 Player 验收，不替代物理鼠标或 Docker/Nakama 在线攻击 smoke。
- 可复现：`unity test Temp/AuraReturnLethalReview/Project --mode EditMode --output Temp/GoalContinue/unity-attack-feedback-preview-editmode.xml --editor-path "D:\Unity 6000.0.28f1c1\Editor\Unity.exe" --timeout 1200`；构建：`unity build Temp/AuraReturnLethalReview/Project --target StandaloneWindows64 --execute-method BiomeRivals.Demo.Editor.DemoBuildAutomation.BuildWindowsFromCommandLine --output-path Temp/GoalContinue/BuildAttackFeedbackReview/BiomeRivals-attack-feedback.exe --editor-path "D:\Unity 6000.0.28f1c1\Editor\Unity.exe" --allow-dirty-build --timeout 1200 --no-tail --log-file Temp/GoalContinue/BuildAttackFeedbackReview/editor-build.log`。截取时以 `scripts/capture-demo-preview.ps1` 传入 `-PreviewAttackFeedback -PlayerFaction plains_forest -OpponentFaction desert_badlands`，并为每档指定独立 `-CaptureWidth`、`-CaptureHeight` 和 `-CapturePath`。

## 2026-09-29 UI-029 小画幅战斗状态条可读性

- 1280×720 Player 截图显示，较长的战斗结果状态会将原 10px 设计字号缩到约 6–7 个屏幕像素。将 `StatusPlate` 最佳适配范围改为 16–18px 设计字号；在窄画幅下保持约 10px 屏幕字号，并依靠原有换行限制长句，1920×1080 下仍完整位于石砖面板内。状态文案逻辑和战斗规则未改。
- Unity CLI `1.0.0-beta.8` / Editor `6000.0.28f1c1` 隔离工程全量 EditMode **323/323 Passed**，报告 `Temp/GoalContinue/unity-status-readability-editmode.xml`。通过项目 `DemoBuildAutomation.BuildWindowsFromCommandLine` 构建 Windows Development Player，并用 `-previewCombatInteraction` 实际跑通部署、回合切换及击败目标；1280×720 截图 SHA-256 `6640E442A556C2AC5CC1302A93597523BA9FA27F04A7F5F758E1E5A564E722C8`：[combat-status-readable-1280x720.png](../../Temp/GoalContinue/combat-status-readable-1280x720.png)；1920×1080 截图 SHA-256 `4E7003F6F37E0D67284B4CA984938C2026ACBC2DCF5A1686C1BB308693F40547`：[combat-status-readable-1920x1080.png](../../Temp/GoalContinue/combat-status-readable-1920x1080.png)。复核基线 HEAD `c942129`，本轮未提交，工作区仍含其他既有修改。
- 可复现测试：`unity test Temp/AuraReturnLethalReview/Project --mode EditMode --output Temp/GoalContinue/unity-status-readability-editmode.xml --editor-path "D:\Unity 6000.0.28f1c1\Editor\Unity.exe" --timeout 1200`；构建：`unity build Temp/AuraReturnLethalReview/Project --target StandaloneWindows64 --execute-method BiomeRivals.Demo.Editor.DemoBuildAutomation.BuildWindowsFromCommandLine --output-path Temp/GoalContinue/BuildStatusReadable/BiomeRivals-status-readable.exe --editor-path "D:\Unity 6000.0.28f1c1\Editor\Unity.exe" --allow-dirty-build --timeout 1200 --no-tail --log-file Temp/GoalContinue/BuildStatusReadable/editor-build.log`。

## 2026-09-29 RULE-033C 离线战斗交互 Player 复核

- 确定性战斗预览现在在派发战场点击后显式清除注入指针的 hover 状态，并等待攻击前冲、伤害/阵亡反馈收束后才报告截图就绪；避免 Player 截图把已攻击单位误显示成仍可行动。真实对局规则与动画时长未改。
- Unity CLI `1.0.0-beta.8` 驱动 Unity `6000.0.28f1c1`，在隔离项目全量 EditMode **323/323 Passed**（`Temp/GoalContinue/unity-combat-preview-final-editmode.xml`），随后使用项目 `DemoBuildAutomation.BuildWindowsFromCommandLine` 构建 Windows Development Player。Player 通过 UI GraphicRaycaster 选择手牌/结束回合、3D Physics 射线部署并选择攻击者/目标；日志确认第 2 回合战斗阶段、铁傀儡存活 5/5、敌方僵尸阵亡，Player 退出码 0。1920×1080 截图 SHA-256 `A21CFAB3F3964F135C08E72429FD2D7360A8B6C7FBB28B65B3A2C976BB7BF8A0`：[combat-interaction-feedback-settled-1920.png](../../Temp/GoalContinue/combat-interaction-feedback-settled-1920.png)。这是确定性事件派发的离线 Player 证据，不等同于物理鼠标测试，也不替代 RULE-033C 在线双客户端 smoke。
- 可复现命令：`unity test Temp/AuraReturnLethalReview/Project --mode EditMode --output Temp/GoalContinue/unity-combat-preview-final-editmode.xml --editor-path "D:\Unity 6000.0.28f1c1\Editor\Unity.exe" --timeout 1200`；构建使用 `unity build Temp/AuraReturnLethalReview/Project --target StandaloneWindows64 --execute-method BiomeRivals.Demo.Editor.DemoBuildAutomation.BuildWindowsFromCommandLine --output-path Temp/GoalContinue/BuildCombatPreviewHoverCleanup/BiomeRivals-combat-preview-verified.exe --editor-path "D:\Unity 6000.0.28f1c1\Editor\Unity.exe" --allow-dirty-build --timeout 1200 --no-tail`。复核基线 HEAD `c942129`；本轮未提交，避免混入工作区其他既有改动。

## 2026-09-28 RULE-033C 离线 Player 回手交互验收

- 新增 `-previewEndReturnInteraction` 确定性 Player 流程：手牌与右侧释放按钮先经 `GraphicRaycaster` 命中，再派发 Unity PointerDown/Up/Click；战场目标则以世界坐标经 3D Physics 射线、按下/抬起和格位身份核验，随后触发场景控制器部署/选目标。另验证结束回合按钮屏幕点确有 UI GraphicRaycaster 命中，并会阻断后方战场格。流程完成末影人部署、紫颂果回手，检查单位格清空、紫颂果进入弃牌堆、本回合 `-1` 折扣及末影人当前费用 2。原 `-previewGroundReturnPulse` 仍仅用于材质脉冲视觉检查，不再被当作回手玩法证据。
- Unity CLI 在隔离工程全量 EditMode **323/323 Passed**，报告 `Temp/GoalContinue/unity-end-return-ui-raycast-proof-editmode.xml`；Windows Development Player 构建成功。Player 日志确认两张手牌与 Cast 按钮 UI 事件、结束回合按钮 UI 命中并阻断战场。截图通过退出码、日志断言、源码清单、PNG 尺寸及 SHA-256 校验：1920×1080，`5062B5AE92D603C53897EEBFF0AA38BB39834CB1245DE013A3567D04A62391F3`：[end-return-ui-raycast-proof-1920.png](../../Temp/GoalContinue/end-return-ui-raycast-proof-1920.png)。Docker/Nakama 双端 smoke 仍未执行，不以此离线验收替代联机门禁。

## 2026-09-28 UI-028 Canvas 缩放下的卡牌文字可读性

- 1280×720 实图表明固定 Canvas 字号会随参考画幅缩小；卡牌规则字号现在按根 Canvas 缩放因子反向适配，紧凑手牌保持至少约 12px、详情卡约 10px 的屏幕字号，并在窗口缩放时重新计算。长手牌规则仍测量后省略，详情卡保持注册原文；为容纳缩放后的详情全文，详情卡立绘区域由 39% 收至 33%，规则区域由 29% 扩至 35%。
- Unity CLI / Unity `6000.0.28f1c1` 全量 EditMode **323/323 Passed**，包含 2/3 与 1/2 Canvas 缩放及运行时缩放变化回归。Windows Development Player 构建成功；1280×720 截图 SHA-256 `305171F257326DB1D38C365377CC2BDE7E02DD533A454649B2ABBAC414DAAB48`：[hand-hover-font-adaptive-r3-1280x720.png](../../Temp/GoalContinue/hand-hover-font-adaptive-r3-1280x720.png)；1920×1080 截图 SHA-256 `1F969B536EFEFD1C9B18445A2758BE90751EB5B27A41D3BBA63A7BB0880E4F04`：[hand-hover-font-adaptive-r3-1920x1080.png](../../Temp/GoalContinue/hand-hover-font-adaptive-r3-1920x1080.png)。

## 2026-09-28 UI-027 紧凑手牌规则文字可读性复核

- 对最新 1920×1080 Development Player 画面复核后，紧凑卡规则字在实际整手布局中仍偏小；将仅限紧凑手牌的自动字号下限从 10 提至 12，详情卡维持 10。卡面尺寸、居中排版和长文省略逻辑不变。
- Unity CLI 驱动 Unity `6000.0.28f1c1` 隔离工程全量 EditMode **323/323 Passed**，覆盖短文完整、长文省略、预览边界不溢出与详情卡全文；Windows Development Player 构建和运行截图验收通过，1920×1080，SHA-256 `13229A6D56D6E420D7AC1F77F50045235E2443B733C78737AE5DBCBF4BD8DEBE`：[hand-hover-font12-player-1920.png](../../Temp/GoalContinue/hand-hover-font12-player-1920.png)，NUnit 报告 `Temp/GoalContinue/unity-ui027-font12-editmode-r2.xml`。

## 2026-09-28 DATA-040H Minecraft 世界纹理注册与本机提取物审计

- 将原先硬编码在提取脚本里的 40 个方块纹理映射提升为 `minecraft-world-texture-registry.v1.json`，增加严格 Draft 2020-12 Schema；世界纹理提取器改为读取共享清单，资源路径从规范注册源进入生成链。
- 新增只读本机素材审计：卡图 74 项必须逐项匹配共享 art registry，世界纹理 40 项匹配新清单，实体模型/贴图 64 项匹配固定 Bedrock commit；逐文件 SHA-256、来源版本/策略、路径安全和未登记的额外文件都会检查。未提取资产的干净机器仍可通过，不要求下载或提交受版权约束的素材。
- 验证：`scripts/validate.ps1` 全链通过，卡牌与素材来源门禁、服务端 **234/234**、TypeScript 和构建均通过；篡改文件、额外未登记文件及非法世界纹理来源路径负向控制通过。实际从本机 Java JAR 临时重提取 **40/40** 方块纹理并由审计器验收；Unity CLI 隔离工程 EditMode **323/323 Passed**（`Temp/DATA040HReview/unity-editmode.xml`）。

## 2026-09-28 QA-041F Unity 验证只读检查生成物

- `validate-unity.ps1` 不再先运行卡牌/卡框同步器，改为验证 card registry、Unity Resources 副本、服务端 catalog 与卡框副本的哈希/生成状态。独立运行 Unity 验证不会把陈旧资源静默改写后再测试；需要更新资源时仍使用单独的 `sync-card-content.ps1` 或 `sync-card-frame-study.ps1`。
- 卡框同步器新增 `-Check` 只读模式；负向测试确认过期与缺失副本都会失败且原文件保持不变。测试：`scripts/test-card-frame-sync.ps1`，并通过 `scripts/validate-card-content.ps1`。Unity Editor 本轮已有当前隔离工程 EditMode **323/323** 和 Windows Player 构建通过；Docker 在线 smoke 仍受 daemon pipe 不可达限制。

## 2026-09-28 DATA-040G 在线匹配前卡牌版本预检

- Nakama health RPC 现在同时公布协议、规则集、卡牌 catalog 和效果实现注册版本。Unity 客户端在打开实时 socket、进入 matchmaker 前先核对四项版本；任一不兼容会停止连接并显示“客户端版本不兼容”，详细差异保留在连接错误日志中。预期版本直接来自运行时加载的 `CardContentRegistry`，不会另设易漂移的客户端常量。
- 为 Nakama health RPC 加入版本回归测试，Unity EditMode 覆盖完全匹配及四种版本不一致/无效版本，并扩展在线 smoke 检查服务端 health 数据。验证：`npm test` **234/234 Passed**；Unity CLI `1.0.0-beta.8` 驱动 Unity `6000.0.28f1c1` 隔离项目 EditMode **323/323 Passed**，并成功构建 Windows Player（`Temp/AuraReturnLethalReview/Build/BiomeRivals.exe`）。Docker/Nakama 实时 smoke 仍需 Docker Engine 恢复后执行。

## 2026-09-28 UI-026 紧凑卡牌规则文字最小字号

- Player 实图审查发现整手紧凑卡规则正文过小；将紧凑卡最佳适配字号下限从 9 提至 10，保留动态适配和超长文本省略，详情卡正文与布局不变。
- EditMode 明确锁定紧凑卡最低字号，并继续验证短文完整、长文省略以及详情全文可容纳。Unity 6000.0.28f1c1 隔离项目全量 EditMode **314/314 Passed**；Windows Development Player 构建成功，悬停手牌画面通过来源清单、运行日志、进程退出码、PNG 尺寸校验。1920×1080 截图 SHA-256 `1F4681442EF6B487A967DCDFA14A446C55FDF9C3900EA6FF231C0673257A0C9E`：[hand-hover-font10-1920.png](../../Temp/UI026Review/hand-hover-font10-1920.png)，机器报告为同名 `.json`。

## 2026-09-28 UI-024 交互格地表高亮可读性校正

- 加强 3D 地块表面像素格边缘的高亮混色，保持群系纹理可见；回手事件改用与末地紫色地面有明显对比的金色，并将事件脉冲延长至 0.95 秒。验收预览单独延长时长以跨过 Player 初始化，不影响实战时长。
- Unity CLI 隔离项目 EditMode **313/313 Passed**；Unity CLI 构建 Windows Player 成功。七种群系分别运行 1920×1080 截图验收，均通过进程、日志、源文件清单、尺寸和 SHA-256 校验；末地截图哈希 `C50C98DE26CC0EA59AE3EEE6B391184BCE92616C3DB8B0707C46BDF4E3D7E72B`，见 [ground-return-pulse-final.png](../../Temp/GroundPulseReview/ground-return-pulse-final.png)，七群系对照见 [theme-pulse-contact-sheet.png](../../Temp/GroundPulseReview/theme-pulse-contact-sheet.png)，各阵营机器报告为同名 JSON。七种材质上金色 Minecraft 像素格都直接贴地显示，没有切换为 UGUI 浮框。

## 2026-09-28 DATA-040E 效果注册版本依赖显式化

- 卡牌内容版本 `42` 与效果实现注册版本 `48` 保持各自递增，但定义注册表新增 `implementedEffectRegistryVersion`，记录卡牌实现状态所依据的效果注册表版本。内容生成器统一写入 Unity Resources 与 Nakama catalog；AJV/内容门禁拒绝定义注册表引用旧效果版本。Unity `CardContentRegistry` 暴露两个版本，服务端生成 catalog 也导出效果注册版本常量。
- 负向控制以当前效果注册版本 `49` 模拟陈旧的 `48` 依赖，确认在 schema 结构合法时仍被跨注册表校验拒绝。`scripts/validate.ps1` 全链通过：服务端 **233/233**、TypeScript、构建；Unity CLI 隔离项目 EditMode **314/314 Passed**，报告 `Temp/GroundPulseReview/unity-data040e-editmode.xml`。

## 2026-09-28 DATA-040F Bedrock 生物来源 Schema

- 为 Mojang Bedrock 生物模型来源清单补充严格 JSON Schema，校验固定仓库/版本策略、完整提交 SHA、来源根目录，以及安全的 `.geo.json`、PNG/TGA 路径；纳入统一 AJV 内容门禁。
- 语义门禁要求每个生物的几何模型与纹理映射键完全一致；负向控制分别确认模型/纹理错配和越出来源根目录的路径都会被拒绝。Unity 资源未重提取或改写，本任务只规范来源数据契约。
- `scripts/validate.ps1` 离线全链通过：74 张卡内容注册、TypeScript、服务端 **233/233** 与构建；Unity 6000.0.28f1c1 隔离项目 EditMode **314/314 Passed**，报告 `Temp/DATA040FReview/unity-editmode.xml`。Docker daemon 的 `desktop-linux` named pipe 当前不可达，未运行或声称通过在线 smoke。

## 2026-09-28 UI-025 长规则手牌预览溢出提示

- 紧凑手牌过去在 9px 最小字号下仍可能静默裁掉长规则。现在由 Unity `TextGenerator` 按实际卡面宽高、字体与最小字号测量；只有全文放不下时才在可见预览末尾添加省略号，能容纳的短文不变，右侧详情卡始终保留全文。
- Unity CLI 隔离项目 EditMode **314/314 Passed**，含手牌预览省略、短文完整，以及最长规则在预览和详情卡最小字号下均不溢出的测量回归；Development Player 构建成功，1920×1080 下界对末地截图通过来源清单/日志/退出码/尺寸校验，SHA-256 `8AEB40CF938555E8CFB3C37BAA24486C91AEC415FC3BE4EC13D92A09BEA39600`：[`card-text-fit-nether-1920.png`](../../Temp/GroundPulseReview/card-text-fit-nether-1920.png)。最终测试报告：`Temp/GroundPulseReview/unity-card-text-fit-final.xml`。

## 2026-09-28 DATA-040D 其余卡牌注册表 JSON Schema

- 为卡名、中文卡文、七系主题、Minecraft 卡图映射和本机素材来源增加 Draft 2020-12 Schema，接入 DATA-040C 的 AJV 门禁。对象拒绝未登记字段；枚举、格式、颜色、纹理路径和来源策略有结构级约束。负向控制确认错误效果版本和越界纹理路径都会失败，正式共享 JSON 未被写回。
- `scripts/validate.ps1` 全链通过：Schema 覆盖 74 张定义、74 组本地化、7 个主题、74 个卡图映射、62 个已实现效果及来源元数据；服务端 **233/233**、TypeScript、构建通过。Unity CLI 隔离项目 EditMode **314/314 Passed**，报告 `Temp/GroundPulseReview/unity-data040d-editmode.xml`。

## 2026-09-28 DATA-040C 核心卡注册 JSON Schema 门禁

- 为已实现效果注册表补充 Draft 2020-12 JSON Schema；共享验证器通过服务端已有 AJV 同时验证规范卡定义注册表及其逐卡 schema、效果注册表。`validate-card-content.ps1` 在生成物/副本检查前运行该 schema gate，负向控制确认错误的效果注册表版本会被拒绝。
- `scripts/validate.ps1` 全链通过：74 张卡定义/62 个已实现效果的 schema 检查、服务端 TypeScript、规则测试 **233/233** 与构建；Unity CLI 隔离项目 EditMode **314/314 Passed**，报告 `Temp/GroundPulseReview/unity-data040c-editmode.xml`。DATA-040 仍未收尾：本阶段未给名称/本地化、主题、卡图和来源清单都补齐 JSON Schema。

## 2026-09-28 DATA-040B 卡图来源与用途约束

- 内容校验现核对卡图 sourceId 与 Minecraft Java 本地素材来源的版本目录、JAR SHA-256/文件大小、仅本机拥有素材策略；卡图默认状态和每条素材用途也必须属于明确支持的枚举，避免来源或临时用途元数据静默漂移。
- `scripts/validate-card-content.ps1` 和总验证链通过；服务端 **233/233**、TypeScript、构建通过。Unity CLI 隔离项目全量 EditMode **313/313 Passed**，报告 `Temp/GroundPulseReview/unity-cli-editmode-current.xml`。本次未运行 Docker 在线 smoke，也未触及容器或 Docker 内部数据。

## 2026-09-28 DATA-040A 注册表生成漂移校验

- 卡名、定义和中文文本同步脚本新增只读 `-Check`，接入 `validate-card-content.ps1`；校验按 JSON 语义比较，既能发现源文档/共享注册表漂移，也不会把键顺序或缩进差异误判为内容错误。卡牌自身的 `contentVersion` 从规范定义注册表按卡保留，新卡默认使用顶层版本，避免重生成时把既有卡版本抹平；所有共享注册表的 schema/locale 和逐卡版本范围也会校验。
- 负向控制确认旧的 41 版临时生成物会被 `-Check` 拒绝，未改写正式注册表。`scripts/validate.ps1` 全链通过：74 张注册内容、TypeScript 类型检查、服务端 **233/233** 规则测试及构建；Unity CLI 隔离项目 EditMode **313/313 Passed**，报告 `Temp/GroundPulseReview/unity-cli-data040-editmode.xml`。DATA-040 仍进行中，规范卡数据与剩余元数据/美术注册源的统一还未完成。

## 2026-09-28 UI-023 战场事件地表脉冲

- 修正回手/攻击/阵亡反馈先前借用输入态 `Pressed` 的问题：非可操作格不一定呈现事件颜色。新增独立 3D 地表表现脉冲，不更改 hover、pressed 或目标合法状态；事件颜色直接混入方块纹理表面，并在空格上做轻微抬升/扩张，拒绝态仍保持最高优先级。
- EditMode 锁定紫色反馈进入地表 `_HighlightColor` 与 `_HighlightStrength`，并推进脉冲时间验证空地块真实抬升/扩张后能回落；riser 不会伪装成鼠标悬停。Unity CLI 隔离项目全量 **313/313 Passed**（`Temp/GoalContinue/unity-ground-return-pulse-editmode.xml`）。Docker Engine pipe 仍不可达，因此本次未做 Docker/Nakama 双端实时演出验收。

## 2026-09-28 RULE-033C 回手在线界面反馈

- Unity 在线事件 Presenter 现在会播报单位从战场回到拥有者手牌，或因手牌已满进入弃牌堆；同步以末地主题短暂脉冲原单位格并提示对应玩家 HUD。回手减费只在拥有者视角显示，费用到期时也只向牌主说明恢复后的费用，对手侧不因脱敏空事件暴露私有手牌信息。
- 新增事件注册、折扣可见性、满手弃牌和折扣到期 EditMode 回归。通过 Unity CLI 在隔离项目执行全量 **312/312 Passed**（`Temp/GoalContinue/unity-return-presenter-editmode.xml`）；用例额外验证即使 UI 收到未脱敏折扣字段也不会向非拥有者展示。RULE-033C 的 Docker/Nakama 双端在线 smoke 仍待 Docker Engine 恢复，离线验证不替代该门禁。

## 2026-09-28 UI-022 战场相机视口比例适配

- 移除相机独立的固定 aspect，按 UI 的 16:9 设计画幅做居中 contain：窄屏上下留边，超宽屏两侧留边；Screen 或 RenderTexture 改变时自动重算 camera rect。这样保持 Minecraft 模型比例，也避免 4:3 视口裁掉阵营栏与详情面板。
- `capture-demo-preview.ps1` 新增可选 `-CaptureWidth/-CaptureHeight`，截图、构建来源清单、尺寸和 SHA-256 仍完整校验。Unity CLI 全量 EditMode **309/309 Passed**，报告 `Temp/GoalContinue/unity-camera-aspect-editmode.xml`；隔离项目 Windows Development Player 构建成功。4:3 截图 [camera-aspect-letterbox-1280x960-r2.png](../../Temp/GoalContinue/camera-aspect-letterbox-1280x960-r2.png)，SHA-256 `B472A1DEF9C5FC2199273F6A77BF742816DA607240D458FF5E911B06B03F7C26`；16:9 截图 [camera-aspect-letterbox-1920x1080-r2.png](../../Temp/GoalContinue/camera-aspect-letterbox-1920x1080-r2.png)，SHA-256 `947BDD2E2320FDD732D0AFD6035703A0FA2C2C0F195637D9E855632A0183B649`；21:9 截图 [camera-aspect-letterbox-2520x1080.png](../../Temp/GoalContinue/camera-aspect-letterbox-2520x1080.png)，SHA-256 `90AC4E44E84A3FC1D1C5B49513BCCE8AE31B962EBFC7AF5F39B84E9E3395C995`。三张均通过 Player 退出码、日志、尺寸与当前构建来源清单校验。

## 2026-09-28 RULE-033C 回手目标阵营校验

- 为 ED-002/ED-005 补充敌方战场对象目标回归：两者均须在支付前原子拒绝敌方对象，且不改变 revision、能量、手牌、弃牌堆或敌方单位状态。
- 通过 Unity CLI `test` 命令在隔离项目运行全量 EditMode，**309/309 Passed**（`Temp/GoalContinue/unity-return-target-editmode-cli.xml`）；`scripts/validate.ps1 -WithDockerConfig` 通过，服务端 **233/233**、TypeScript、构建及 Compose 静态配置均通过。

## 2026-09-28 UI-021 双阵营天空氛围映射

- 2.5D 战场不再固定使用纯近黑色清屏；相机背景色由玩家与敌方群系环境光混合而成，保持低亮度，同时让切换任一侧阵营都能反馈到整张场景的氛围。
- 扩展七群系切换 EditMode 回归，逐个检查玩家侧主题天空色，并验证只切换敌方阵营也会更新天空。Unity CLI EditMode **309/309 Passed**（`Temp/GoalContinue/unity-biome-sky-editmode.xml`）；Windows Development Player 构建成功。末地对下界画面 SHA-256 `4566CA55BD5D8B025D195D8B34D1EE532DEB3C45BDEE8C8233582ED4D4E92EA4`；再用冰原对海洋实机复核，确认冷色主题下的天空混色及卡面协调，SHA-256 `83732868E88430F5A53968ED6DEF2DBE9AF06C63630619F2A654ACFCB4636446`。两张 1920×1080 截图分别为 [末地对下界](../../Temp/GoalContinue/biome-sky-end-nether-1920.png) 和 [冰原对海洋](../../Temp/GoalContinue/biome-sky-snow-ocean-1920.png)，均通过 Player 来源清单校验。

## 2026-09-28 UI-013B 手牌悬停可读性与拾起反馈

- 手牌悬停从轻微 1.08 倍/22px 调整为 1.22 倍/32px，卡片置顶并继续使用平滑、非受暂停影响的过渡；离开时恢复原扇形顺序与位置。
- EditMode 测试读取场景实际配置，验证缩放目标、抬升、置顶和退出恢复；确定性预览会在 UI 重建后按当前手牌实例重新定位目标，并固定预览悬停状态，不依赖暂停 EventSystem。Unity CLI EditMode **308/308 Passed**（`Temp/GoalContinue/unity-editmode-ui-hover-final.xml`）；Windows Development Player 由 Unity CLI 在隔离项目构建成功。`capture-demo-preview.ps1 -PreviewHandHover` 的源码清单（630 个文件）、进程退出、PNG 1920×1080 与 JSON/SHA-256 检查均通过。
- 悬停实机画面：[hand-hover-122-1920-cli-20260928.png](../../Temp/GoalContinue/hand-hover-122-1920-cli-20260928.png)，SHA-256 `4FF8B23EDEF90CB45FB863A351D40D65C3672B05B7B125C811C9B11B43BA83AC`，完整机器报告为同名 JSON；普通末地对下界基线图：[demo-end-nether-1920.png](../../Temp/GoalContinue/demo-end-nether-1920.png)。
- 2026-10-02 当前源码复核：同一 Unity 6000.0.28f1c1 Development Player 对 1920×1080 与 4:3（1600×1200）执行确定性悬停预览；两次均核验 630 项源码清单、Player 退出码 0 与画幅，并目视确认放大卡未裁切、详情规则可读。SHA-256：`EC316872F8308A977255C69C47070C80938166FEC46987B2E883E360D44D266D` [1920×1080](../../Temp/GoalContinue/CurrentAudit/hand-hover-current-source-1920.png)，`C4F94F83A7A1E292FE8AF4BB3AB95CB35068EC12F9673D3497129BAAD0FECCCF` [4:3](../../Temp/GoalContinue/CurrentAudit/hand-hover-current-source-4x3-1600x1200.png)。

## 2026-09-28 RULE-033C 对局内战场实例 ID 唯一性

- 服务端权威状态校验与 Unity 快照接收现在共同要求战场对象 `instanceId` 在整场对局中唯一，而非仅在各自玩家的半场内唯一；重复 ID 会在提交状态/重连快照前被拒绝，保护精确目标与事件回放。
- 新增服务端和 Unity EditMode 回归。`scripts/validate.ps1 -WithDockerConfig` 通过，包含内容注册校验、TypeScript、服务端 **232/232**、构建与 Compose 静态配置；Unity CLI 隔离副本 EditMode **309/309 Passed**，报告 `Temp/GoalContinue/unity-global-instance-editmode.xml`。随后用更新后的隔离项目重新构建 Windows Development Player 并运行悬停截图验收，画面为 1920×1080，SHA-256 `7F531CD3112280ED81590B92DF231F077F0D4DA1C9217515B0D20166272A1713`，截图：[hand-hover-globalids-1920.png](../../Temp/GoalContinue/hand-hover-globalids-1920.png)，机器报告为同名 JSON。
- RULE-033C 仍未关闭：真实 Docker/Nakama 双端 smoke 尚未运行，当前 Docker Engine API pipe 不可用。

## 2026-09-28 QA-041A 统一验证入口失败传播

- `scripts/validate.ps1` 现在检查 `npm run typecheck`、`npm test`、`npm run build` 与可选 `docker compose config --quiet` 的原生退出码；任何非零状态都会中止验证并指出失败阶段，避免错误后仍打印总体验证通过。
- 正常链 `scripts/validate.ps1 -WithDockerConfig` 通过：卡牌注册校验（74 张定义/文本/卡图映射、62 个已实现效果）、服务端类型检查、规则测试 **231/231**、服务端构建及 Compose 配置检查均通过。负向控制注入类型检查退出码 23、构建退出码 37 和 Compose 检查退出码 43，均确认入口在准确失败阶段停止。
- Compose 配置校验不启动 Docker Engine，也不替代 RULE-033C 双客户端在线 smoke；该在线验收仍待 Docker Engine 可用。

## 2026-09-28 QA-041B 分阶段验证耗时

- `validate.ps1` 现在在每层成功时输出阶段名与耗时，失败时输出失败阶段、已耗时间和底层错误；结束时报告全链耗时，保留原有命令输出与报告路径。
- 本机热缓存一次 `scripts/validate.ps1 -WithDockerConfig` 用时 **10.3 秒**：内容校验 0.4s、服务端类型检查 2.3s、服务端测试 3.9s、构建 3.4s、Compose 配置 0.4s。此为诊断样本，不是跨机器耗时承诺。

## 2026-09-28 QA-041C Player 截图验收入口

- 新增 `scripts/capture-demo-preview.ps1`：从显式 Windows Player 路径启动 Demo，复用游戏内 `-captureDemo` 流程，验证进程退出码、日志中的目标路径、运行时异常标记、PNG 签名和 1920×1080 尺寸，并输出 SHA-256 与 JSON 清单。拒绝覆盖已有截图/日志/清单；超时只结束本脚本启动的 Player。
- 截图验收入口使用末地/下界组合 Player 实际运行通过；画面确认末影螨效果说明与部署操作同时可见。经源码清单验证、并在完整 EditMode 运行后再次捕获的截图：[manifest-after-editmode-end-1920.png](../../Temp/QA041/DemoAcceptance/manifest-after-editmode-end-1920.png)，SHA-256 `3EFA68A81D78ED4ABDFADE11DBE7A8BA13D10F5F6CFB3CD4EA17C4DC6123D34C`；机器报告为同目录 `.json`。另用含空格的输出路径复测通过。
- 此入口验收的是 Player 画面，不会隐式构建或打开编辑器；Docker 双端 smoke 仍需要 Engine 可用。

## 2026-09-28 QA-041D Player 来源清单与新鲜度

- Unity `DemoBuildAutomation` 在成功构建后为 Player 写出 `.build-manifest.json`，逐项记录 `Assets`、`ProjectSettings`、`Packages` 的相对路径与 SHA-256，以及 Unity 版本、目标和构建时间。
- 截图入口现在必须同时给出 Player 和 Unity 工程路径；运行前校验来源文件集合与哈希，缺清单或源码不匹配即拒绝启动，防止旧 Player 生成貌似有效的验收图。Unity CLI 隔离构建成功并记录 **630** 个源文件；当前 `client-unity` 与构建副本一致，错误工程负向控制（0 vs. 630 个源文件）及无清单旧 Player 均被拒绝，未写截图。随后全量 Unity CLI EditMode **307/307 Passed**，测试后再次截图仍通过来源一致性检查。

## 2026-09-28 QA-041E 本机/CI 共用服务端验证入口

- GitHub Actions `Validate` 工作流切到 Windows PowerShell runner：执行 `npm ci` 后直接调用与本机相同的 `scripts/validate.ps1`，不再在 YAML 中重复维护内容检查、类型检查、规则测试和构建命令。
- 使用相同的 `pwsh -NoProfile -File .\scripts\validate.ps1` 在本机实跑通过：内容校验、服务端类型检查、**231/231** 规则测试和构建全绿，用时 **10.0 秒**。尚未推送，因此 GitHub 托管 runner 的实际运行仍待 PR/push 验证；Unity 授权测试与 Docker 在线 smoke 仍是可选本机验收层。
- 本次续审通过 Unity Hub CLI 在隔离项目副本重新执行 EditMode：Unity **6000.0.28f1c1**，**307/307 Passed**，报告 `Temp/GoalContinue/unity-editmode-current-20260928.xml`；主项目 Editor 保持打开且未受影响。随后 `scripts/validate.ps1 -WithDockerConfig` 通过，服务端 **231/231**、类型检查、构建、内容注册及 Compose 静态配置均通过，用时 **10.4 秒**；未启动 Docker Engine。

## 2026-09-28 UI-020 未接入效果的可部署卡提示

- 对效果状态为 `PENDING` 的单位/建筑，在卡牌详情中显示玩家向提示“仅基础属性可用 · 卡牌效果尚未接入”；基础费用、攻击/生命及部署操作仍然有效，已实现效果的卡牌不会继承此提示。
- 新增 Unity 场景 EditMode 回归，覆盖当前全部四张 `PENDING` 可部署单位（ED-001、ED-003、DB-008、TK-017）；并验证 ED-001 仍可按基础属性部署且扣费正确、已实现卡不显示残留提示。Unity CLI 专项测试 **1/1**、全量 EditMode **307/307 Passed**。Windows Development Player 构建和运行成功。
- Player 1920×1080 画面：[pending-effect-end-1920.png](../../Temp/PendingEffectReview/pending-effect-end-1920.png)，SHA-256 `7FE1003C141828C9A0531C50BC142B256C60D5D443C99FAF437E894F73168DD7`；视觉复核确认说明位于卡牌详情操作提示下方、清晰可读，部署入口仍可见。

## 2026-09-28 UI-019 玩家详情隐藏规则诊断

- 卡牌详情面板默认不再显示“规则状态”与内部 `effectId`，避免将注册/实现细节暴露到玩家界面；开发排障仍可给 Player 显式传入 `-showRuleDiagnostics` 恢复该区块。
- 新增 EditMode 回归验证默认隐藏与开启诊断后的完整效果 ID。Unity CLI 专项测试 **1/1**、全量 EditMode **306/306 Passed**；Windows Development Player 构建及两种参数模式运行成功，日志无运行时异常。
- 默认玩家画面：[player-clean-1920.png](../../Temp/RuleDiagnosticsReview/player-clean-1920.png)，SHA-256 `831D27968EC226D8EF7638B4F5392A61ED48663AC442191B2130CA4A074BC4EB`；诊断画面：[player-debug-1920.png](../../Temp/RuleDiagnosticsReview/player-debug-1920.png)，SHA-256 `A28FE93A14715FFB88847118306AA506344D465762FF5FEDC45B6B23A50B7F8C`。

## 2026-09-28 UI-018 群系主题操作提示

- 详情面板中部署/目标操作的可执行提示，以及合成支付强调色，现读取当前所选卡牌注册的群系 Accent；危险/材料不足提示继续使用红色，费用折扣提示继续使用金色，固定品牌青绿色不变。
- 新增 Unity 场景回归，遍历七种群系并选择各自已实现单位，逐个核对提示色与注册主题色。Unity CLI 全量 EditMode **305/305 Passed**；Windows Development Player 构建、运行均退出码 0，Player 日志无异常。
- Player 1920×1080 截图：[faction-accent-1920.png](../../Temp/FactionAccentReview/faction-accent-1920.png)，SHA-256 `B27C217B91675EBED83D543FAB014584A6DC2FFEC08FC820E1796684DC82BBCC`。平原部署提示从固定青绿切换为与卡框相呼应的金色，文字仍清晰。

## 2026-09-28 UI-017 阵营 HUD 原版生物头像

- 双方 HUD 头像现在随阵营切换为对应的已注册 Minecraft 生物卡图：平原村民、沙漠骆驼、冰原北极熊、深暗监守者、海洋海豚、下界烈焰人、末地末影人；头像资源缺失时保留原有像素符号回退。
- 新增场景 EditMode 覆盖七个阵营的玩家/敌方头像切换、Sprite 比例、射线配置及缺图回退。Unity CLI 专项测试 1/1、全量 EditMode **304/304 Passed**；Windows Development Player 构建和运行退出码均为 0。
- Player 1920×1080 截图：[faction-avatar-1920.png](../../Temp/FactionPortraitReview/faction-avatar-1920.png)，SHA-256 `964B58C189758574BF8D692B4A4C7EF6D8B713A421131DF516F90CECA83630BA`。画面复核确认玩家平原/敌方下界头像均显示对应原版卡图，HUD 尺寸与战场布局稳定。

## 2026-09-28 RULE-033C 双客户端在线回手探针预备

- 新增 `server-nakama/scripts/smoke-end-return.mjs` 与 `npm run smoke:end-return`：分别为 ED-002 与 ED-005 创建双客户端末地对局，推进回合收集对应回手牌和己方单位，检查 `CARD_PLAYED → OBJECT_RETURNED` 顺序、精确返回手牌实例 `-1/-2`、对手手牌投影脱敏、双方断线重连后公开棋盘收敛且手牌身份仍保密，以及结束回合时该实例的费用到期。
- 当前 Docker daemon 仍不可连接（`dockerDesktopLinuxEngine` named pipe 不存在），所以**没有运行或声称通过在线 smoke**；探针仅完成语法检查。服务端规则测试 **231/231**、生产 TypeScript 类型检查通过。
- 双端快照断言补齐后，再次通过 Unity CLI 全量 EditMode **303/303 Passed**，退出码 0；报告 `Temp/RULE033C-current-goal-unity-editmode-r3.xml`（SHA-256 `BB8BEF42B5B9081D84BE7772D56603C8C441BD62BC03E7ED98D65CC1C57037D8`）。主项目 Editor 保持运行且未被测试副本干扰。

## 2026-09-28 UI-016 战斗伤害数字反馈

- 战场单位与建筑现在会在受击格位上方显示朝向相机的 Minecraft 风格浮动伤害数字：短促弹出、上浮、淡出，并带暗色投影；英雄受击仍使用既有 HUD 脉冲。
- 本地对局使用战斗攻击/反击数值；联机对局消费权威 `ATTACK_RESOLVED` 伤害字段。对象阵亡并已离开快照时暂存实例伤害，并在对应 `OBJECT_DIED` 格位事件到达后显示，避免丢失致死反馈或定位错误。
- 新增 EditMode 回归，锁定伤害数字为战场 3D 文字、数值与暗色投影正确，并按建筑多占用格中心定位；零伤害不生成浮字。随后补充真实事件 Presenter 回归，验证联机致死目标先暂存伤害、再由 `OBJECT_DIED` 提供敌方原格位，数字不会显示在错误一侧。最新 Unity Hub CLI 全量 EditMode **303/303 Passed**（退出码 0），报告 `Temp/CurrentGoal-final.xml`。Windows Development Player 构建成功并运行预览退出码 0。1920×1080 画面 [`damage-number-preview.png`](../../Temp/DamagePopupReview/damage-number-preview.png)，SHA-256 `D8C1619FFEBFD02419DA71818F21AF3887CF5243B1E83B3064DAB7740439E7BB`；运行日志无运行时异常，主项目 Editor 未受影响。
- **2026-09-29 复核**：使用当前 UI-039 后的 Development Player 重跑 `-PreviewAttackFeedback`。真实本地攻击结算仍为 5 点伤害、2 点反击、目标阵亡且停留在战斗阶段；1280×720 和 1920×1080 两档均通过 630 项源码清单、Player 退出码和 PNG 校验。1280 SHA-256 `EA6DEA11F7E6057E930A233909CB659290D55AF2F3EFAEA19A96557D8E313AE9`：[attack-feedback-1280x720.png](../../Temp/GoalContinue/PendingPointerLock/attack-feedback-1280x720.png)；1920 SHA-256 `8D5962B4FC09724DB1B973C82999DECA0CA4B8F794004C926D76DFB5691A2E60`：[attack-feedback-1920x1080.png](../../Temp/GoalContinue/PendingPointerLock/attack-feedback-1920x1080.png)。实图确认伤害/反击数字位于对应受击格位，未遮挡右侧战斗提示；这是本地 Player 复核，不替代在线实时攻击 smoke。

## 2026-09-28 UI-015 攻击与阵亡事件的战场反馈

- 在线 `ATTACK_RESOLVED` 现在按观看者视角播报攻击方、目标、伤害与反击；单位/建筑目标和英雄目标使用不同提示，并对仍存活的战场对象或受击英雄 HUD 做短促反馈。`OBJECT_DIED` 明确播报阵亡，并在原 3D 地块上闪现击中高亮，即便对象已从权威快照移除也保留反馈位置。
- 本地与在线单位攻击现在都会驱动原版模型在 2.5D 棋盘平面内前冲并回位；攻击者对远处目标的位移有上限，避免穿模。阵亡单位模型已不在快照中时，在线 presenter 会暂存实例 ID，并在匹配的 `OBJECT_DIED` 地块事件到来时补播冲锋。
- 新增攻击播报视角、伤害、反击、英雄目标、冲锋落点及致死伤害 presenter 时序 EditMode 回归，并提供 `-previewAttackFeedback` 确定性场景入口复用正常本地攻击路径。最新 Unity Hub CLI 全量 EditMode **303/303 Passed**；Windows Development Player 构建成功（Unity 6000.0.28f1c1）。Player 1920×1080 截图 `Temp/AttackFeedback/attack-lunge-1920.png`（SHA-256 `2EB64AFB2B3C29DAE7C942B48D75E920869E28E2D7AE3C418AC0EB94F7EDA515`）；主项目 Editor 未受影响。尚未做 Docker/Nakama 实时联机攻击 smoke。

## 2026-09-28 UI-014 暂存交互取消控制

- `Esc` 或右键现在按优先级逐步撤销：法术/材料目标、可选战吼目标、当前攻击者、最后才清除手牌选择；每步保留其他暂存状态并在状态栏确认。进行中的联机命令、待处理选择和已结束对局不受影响。
- 手牌与攻击者的选择提示补充取消方式。新增场景 EditMode 回归，验证四级撤销顺序、不会误清其他选择、状态栏反馈及空闲时不执行操作。
- Unity CLI 全量 EditMode **298/298 Passed**、0 失败且退出码 0；Unity CLI Windows Development Player 构建成功（退出码 0）。报告：`Temp/AuraReturnLethalReview/unity-interaction-cancel-full.xml`。
- 最新 Player 以 `-previewHandHover` 再次捕获 1920×1080 画面：`Temp/AuraReturnLethalReview/interaction-cancel-ui-1920.png`（SHA-256 `F33F7EF7E773CC32DC6078EEDB0EE86D557A5235C5ACCEC93BEFA95D17EA7B7E`），确认卡牌与 2.5D 战场无布局回归。

## 2026-09-28 UI-013 手牌悬停拾起反馈

- 手牌悬停在原有 1.08 倍放大和置顶之外增加 22 px 平滑上浮，退出后同时回到原位与原扇形层级；按钮、起手选择和其他预览卡的悬停行为不变。新增 `-previewHandHover` 确定性截图入口，复用正式悬停处理与动画更新。
- Unity CLI 在隔离项目副本运行完整 EditMode **297/297 Passed**（退出码 0），回归覆盖上浮目标、实际平滑位移、离开后的回落与扇形顺序恢复；随后 Windows Development Player 构建成功（退出码 0）。
- Player 固定渲染 1920×1080 截图：`Temp/AuraReturnLethalReview/hand-hover-1920.png`（SHA-256 `D7BF3BF31E80B21C19776F933E03A687B0087522E7ABA00A66997A162DC926DF`）。视觉复核确认被悬停卡片在相邻手牌前抬起，卡面内容仍完整可读；主 Unity Editor 未受影响。

## 2026-09-27 UI-011 手牌规则说明可读性复核

- 紧凑卡规则说明的最佳字号上限由 12 提高到 14，保留 9 的自动缩小下限和居中对齐；短描述更清楚，长描述仍按卡面可用区域缩小，避免硬性放大造成裁切。
- Unity CLI 构建隔离 Windows Development Player 成功（Unity 6000.0.28f1c1，构建命令退出码 0），从 Player 实机捕获 1920×1080 卡面截图：`Temp/AuraReturnLethalReview/card-readability-1920.png`（SHA-256 `B8ED94E05232BA32945C94C614076CE2D4320C1762132EFE00E324ADC851D1DC`）。视觉复核确认手牌说明更易辨读、仍在规则文字区域内且保持居中。
- Unity 全量 EditMode NUnit XML 为 **297/297 Passed**，覆盖新的字号上限断言；Editor 测试 runner 日志记录退出码 0，但 CLI 外层未及时释放隔离 batch Editor，结束了该次精确匹配的临时进程。主项目 Editor 未受影响。

## 2026-09-27 RULE-033C Unity 跨所有者回手隐私回放

- 为 Unity 权威状态仓库新增双视角 `OBJECT_RETURNED` 回放回归：被操控但归对手所有的单位返回后，原拥有者看到新手牌实例及 `-2` 折扣；操控者只看到手牌数量占位，实例 ID、卡牌 ID 与费用不泄露。
- 通过 Unity CLI 在隔离副本运行全量 EditMode：**297/297 Passed**，两种观看者视角均通过，命令干净退出；报告：`Temp/AuraReturnLethalReview/unity-full-editmode-cross-owner-return-final.xml`。主 Unity Editor 保持运行。

## 2026-09-27 RULE-033C Unity 离线末影珍珠回手回归

- 补齐 Unity 离线 Demo 中 `ed_005` 的纵向回归：实际打出末影珍珠回手 3 费村民农夫，断言只移除选定单位、手牌实例获得 `-2` 折扣、本回合末折扣与到期归属清除，恢复为原费用。
- Unity CLI 在隔离项目副本执行完整 EditMode：**295/295 Passed**，新测例通过且命令干净退出；报告：`Temp/AuraReturnLethalReview/unity-full-editmode-ender-pearl.xml`。主 Unity Editor 保持运行。

## 2026-09-27 Unity CLI Player 视觉复核与本地卡图打包

- 统一主行动按钮到暗红石/红石暖色的暗石砖体系，并让交互色直接作用于真实九宫格边框；场景回归检查材质键与 Button 高亮/按下色。
- Unity CLI 在隔离项目副本中构建 Windows Development Player 后，发现卡图读取依赖启动工作目录。构建入口现在将 74 张本地 Minecraft 卡图复制到 Player 旁，Development Player 可从仓库外目录启动并正确加载；主 Unity Editor 保持打开未受影响。
- Unity CLI 构建成功；Player 从 `C:\Users\Lenovo` 启动并以退出码 0 输出 1920×1080 截图：`Temp/UnityUiReview-RULE-033/redstone-primary-button-self-contained.png`（SHA-256 `69AD9D11BDA4511BF385EE083E074C5729238748661ECB481A0B258E3CC359A6`）。目标 EditMode 场景测试的 NUnit XML 为 **1/1 Passed**：`Temp/UnityUiReview-RULE-033/ui-editmode-self-contained.xml`；Unity CLI 外层在报告写出后未干净退出，故不记为 CLI 测试命令成功。

## 2026-09-27 RULE-033C 即时回手纵向切片（进行中）

- RULE-033B 已独立提交：`c942129 feat: add stable hand card instances`。
- 服务端已增加不可变 `ownerPlayerId`、协议 38 / `prototype-0.63`、`OBJECT_RETURNED` 公开事件，以及 ED-002/ED-005 的目标校验、TK-017 法术保护、回手/满手弃牌和实例级折扣；服务端规则测试当前 **230/230**、TypeScript 类型检查通过。
- Unity 状态仓库已接入对象所有者和 `OBJECT_RETURNED` 因果回放，新增 CARD_PLAYED→OBJECT_RETURNED→私有手牌投影回归；离线 Demo 已接入 ED-002/ED-005 目标选择、TK-017 法术保护和回手实例折扣。回手手牌实例 ID、折扣和到期玩家只对所有者投影。Unity CLI EditMode 报告当前 **293/293 通过**，报告 `Temp/RULE033C-return-aura-editmode.xml`；卡牌内容注册校验通过（74 张定义、62 个已实现效果）。Docker daemon 当前不可用，故尚未完成 Docker/Nakama 双端回手 smoke；RULE-033C 继续进行。
- **跨所有者回手折扣到期修复**：审查发现受控对象回到原拥有者手牌后，减费正确归当前行动玩家到期，但服务端只扫描行动玩家自己的手牌，导致跨所有权场景的修正残留。现在到期扫描遍历双方手牌，事件明确区分手牌拥有者与到期玩家，并只向手牌拥有者投影副本/费用；Unity 状态仓库校验同一事件并清除精确实例，并拒绝向控制者泄露对方手牌实例。服务端 **229/229**、TypeScript 类型检查、卡牌内容校验通过；Unity CLI EditMode **292/292** 通过，报告 `Temp/RULE033C-cross-owner-expiry-editmode.xml`。Docker daemon 仍未运行，双端 smoke 未完成。
- **回手清除邻接光环回归**：新增海龟光环来源回手后，邻接单位在同一事件序列 `CARD_PLAYED → OBJECT_RETURNED → AURA_RECALCULATED` 中回退生命上限和当前生命的服务端与 Unity 回放测试。服务端 **230/230**、TypeScript 类型检查通过；Unity CLI EditMode XML **293/293 Passed**，报告 `Temp/RULE033C-return-aura-editmode.xml`。Editor 日志记录测试以退出码 0 完成；CLI 外层进程仍在退出清理中，故干净退出尚待确认。
- **回手导致光环致死时执行完整死亡结算**：新回归先复现服务端将回手命令以 `INVALID_STATE` 拒绝（光环重算把相邻单位生命降到 0，但仍留在战场）；现服务端与离线 Demo 在光环重算后运行统一死亡队列，结算弃牌与亡语/召唤，终局时不再触发后续感测效果。服务端 **231/231**、TypeScript、内容校验通过；Unity CLI EditMode 全套 **294/294 Passed** 并干净退出，报告 `Temp/AuraReturnLethalReview/unity-full-editmode.xml`。
- **RULE-033C Docker smoke 复测**：服务端规则 **230/230**、TypeScript 和卡牌内容校验再次通过。Docker Desktop CLI 启动失败，backend 日志定位到旧 `sailor-ingest.sock` reparse point 无法访问/重命名；未触碰该 Docker 内部路径及容器/镜像/卷，待用户确认仅备份此路径后重试。

## 2026-09-27 Unity Hub 命令行环境修复与授权复测

- **Hub 更新**：原 Unity Hub 3.3.6 及其 Licensing Client 均未通过 Windows Authenticode 验签。通过 Unity 官方 CDN 下载并验证签名后，以静默方式并行安装 Hub 3.21.3 到用户级标准目录；未覆盖旧 Hub、Unity Editor 或项目文件。新版 Hub 与 Licensing Client 1.18.3 签名有效，Hub CLI 可识别已安装的 Unity 6.0.28f1c1，GUI 主进程也可正常启动。
- **授权与 Editor 实测**：Unity CLI 状态显示 Unity Personal 已激活；通过 `unity open client-unity` 正常启动 Unity 6.0.28f1c1，项目加载完成，日志未见 C# 编译错误。裸 Editor `-batchmode` 预检仍报告 `com.unity.editor.headless` entitlement 不可用；将验证入口改为 Unity CLI `test` 后，EditMode 可在 CLI 管理的 Editor 会话中执行（详见下条）。旧 `C:\ProgramData\Unity\Unity_lic.ulf` 被新版 Hub 报告为过期，且机器绑定与当前环境不符，未删除。`Temp/Unity_v6000.0.28f1c1.alf` 为本机申请文件；Personal 授权使用 Unity CLI/Hub 登录会话，不需要手动激活文件。
- **RULE-033B 实例费用与卡面反馈**：场景控制器此前在已选 `handCardInstanceId` 失效时，会按 `cardId` 自动回退到另一张同名手牌；离线拒绝原因分类及联机探针也曾按卡名取首张副本。现在显式选中的实例失效后不替换副本，费用分类和联机命令都绑定具体实例。卡牌费用角标不再写死为 `-1`，而按基础费与实际费差显示累计减费。新增有效/失效选择、费用拒绝分类与 `-1/-2/-3` 卡面角标回归；Unity CLI EditMode **269/269** 通过。结果：`Temp/RULE-033B-card-cost-ui-editmode.xml`。随后经 CLI 重新打开项目，Unity 6000.0.28f1c1 加载完成且日志无 C# 编译错误。
- **终局回合费用到期**：服务端在结束阶段触发致胜伤害时会立即发出 `MATCH_ENDED` 并中断剩余回合流程，旧路径因此跳过手牌费用修正到期。现在 `END_TURN` 结算进入终局时，先按实例清除到期修正并发出私有化投影事件，再发最终 `MATCH_ENDED`；Unity 事件 DTO 补齐对应费用字段，客户端投影回归验证终局状态和对手手牌隐私。Nakama 规则测试 **220/220**、TypeScript 类型检查、Unity CLI EditMode **270/270** 通过。Unity 结果：`Temp/RULE-033B-terminal-expiry-editmode.xml`。
- **费用修正叠加边界**：新增服务端与离线 Demo 组合回归，验证 `db_005` 出土减费与所选手牌副本的临时修正按同一公式叠加，负值截断到 0 后允许零红石部署。Nakama **221/221**、TypeScript 类型检查、Unity CLI EditMode **271/271** 通过。Unity 结果：`Temp/RULE-033B-cost-stack-editmode.xml`。
- **同名手牌的 UI 身份绑定**：手牌与详情卡 UI 现在保留 `handCardInstanceId`，对象名也包含该 ID，避免同名牌在 Unity Hierarchy/自动化检查中无法区分。新增场景集成回归，实际点击两张同名牌并检查各自费用、选中抬升和详情面板副本切换。Unity CLI EditMode **272/272** 通过；结果：`Temp/RULE-033B-duplicate-card-ui-editmode.xml`。
- **合成预览绑定所选副本**：复核发现详情面板的材料可用性和支付方式切换仍有两处按 `cardId` 取首张同名牌。两处 UI 查询现传入当前 `handCardInstanceId`，失效选择会提示重新选择；部署仍由同一个实例 ID 校验及消耗。新增重复成品实测：第二张 `db_007` 可作为被选产物合成，第一张原样留在手牌；过期实例不能回退至第一张。Unity CLI EditMode **273/273** 通过。结果：`Temp/RULE-033B-crafting-instance-ui-editmode.xml`。因主项目已在 Unity 中打开，测试在同源码临时项目副本运行，未关闭或干扰现有 Editor。
- **手牌进入路径 ID 验收**：服务端与 Unity 离线 Demo 新增针对性断言，覆盖初始发牌全局唯一 ID、起手替换保留未换牌实例并为补牌分配新 ID、普通抽牌、私有生成、出土与掉落创建新实例。服务端规则回归 **221/221**、TypeScript 类型检查通过；Unity CLI EditMode **273/273** 通过。Unity 报告：`Temp/RULE-033B-hand-entry-paths-editmode.xml`。
- **服务端合成副本错选修复**：入口审计发现后端虽然按实例校验合成成品，却在扣材料后按首个匹配 `cardId` 消耗成品。现在材料结算后仍以锁定的 `handCardInstanceId` 定位成品；新增双 `db_007` 回归，验证提交第二张后第一张实例留在手牌。Nakama 规则 **222/222** 与生产 TypeScript 类型检查通过。
- **联机卡牌命令实例 ID 序列化修复**：审查发现 Unity 命令 DTO 虽持有 `handCardInstanceId`，但 Nakama 网关的 JSON wire payload 未声明该字段，导致 DEPLOY/PLAY 发往服务端时丢失实例身份。现为部署、普通/指定目标/多目标出牌 wire payload 补齐字段，序列化前校验 `hand-<数字>` 格式；命令工厂也拒绝缺失实例 ID。新增 wire JSON、目标数组和缺失/错误 ID 回归。Unity CLI EditMode **277/277** 通过，报告：`Temp/RULE-033B-online-instance-wire-editmode-r2.xml`。
- **在线命令 API 编译期约束实例身份**：继续审查发现在线会话与命令工厂仍将手牌实例 ID 暴露为可省略参数，调用方只有在运行时才会因空值失败。现把部署/出牌的实例 ID 调整为必填参数，目标和支付选项仍保留可选；所有场景调用显式传递选中副本。Unity CLI EditMode **277/277** 通过，报告：`Temp/RULE-033B-required-online-hand-id-editmode.xml`。
- **在线出牌会话实例链回归**：新增 EditMode 集成用例，从 `DemoOnlineMatchSession.PlayCardAsync` 发出指定手牌与目标实例，核对命令载荷，并应用权威确认后的私有手牌投影，验证选中副本从手牌移除且 pending 状态结束。Unity CLI EditMode **278/278** 通过，报告：`Temp/RULE-033B-online-session-instance-chain-editmode.xml`。
- **恢复快照保留手牌实例**：新增 Unity MatchStateStore 恢复测试，验证重连快照保留同名手牌各自的实例 ID、折扣及到期玩家，同时对手两张隐藏手牌仍为不可见空占位。Unity CLI EditMode **279/279** 通过，报告：`Temp/RULE-033B-hand-instance-recovery-editmode.xml`。
- **规则核心禁止缺失实例回退**：审查发现 Nakama 网络入口虽拒绝缺失 ID，底层规则引擎仍为部署/出牌保留 `hand.indexOf(cardId)` legacy fallback，且会在命令前悄悄修补不一致的手牌数组。现引擎入口直接要求 `hand-<数字>` 并按实例查询，移除按卡名回退和隐式状态修补；新增原子拒绝回归。测试夹具显式构造实例身份。Nakama **223/223**、生产 TypeScript 类型检查通过。
- **本地命令工厂禁止同名副本歧义**：离线 Demo 的部署/出牌命令工厂和部署/合成预览缺少实例 ID 时，仅在唯一匹配时推导；重复卡必须使用 UI 当前选中的 `handCardInstanceId`，不能再默认为第一张。确定性场景夹具已显式传入副本 ID。新增折扣副本与原价副本并存测试，确认两者预览费用不同且缺少具体实例时拒绝。Unity CLI EditMode **280/280** 通过，结果：`Temp/RULE-033B-strict-local-command-ids-editmode.xml`。
- **失效手牌选择在详情面板锁定**：联机快照移除已选副本后，即使手牌里还剩同名牌，详情面板也不再误显可释放/部署操作，而是提示重新选择；多目标和普通出牌、部署操作统一要求当前选中实例仍在手牌中。为疲劳/临时红石演示在重置手牌后重新绑定新实例，避免旧选择悬空。新增完整场景交互回归，Unity CLI EditMode **281/281** 通过，报告：`Temp/UI-stale-selected-card-instance-full-editmode.xml`。
- **手牌卡规则文字可读性**：紧凑卡面文字最低字号由 8px 提高到 9px、上限设为 12px，并扩大规则区域宽度和高度；Unity EditMode 同时断言规则文字与立绘表面、卡牌类型标签不重叠。属性区、群系边框和详情卡布局保持不变。Unity CLI 全量 EditMode **281/281** 通过；报告：`Temp/UI-card-rules-readability-editmode.xml`。
- **合成场景预览重新绑定手牌实例**：实际运行 Windows Player 并检查 1920×1080 截图时，发现合成预览重置手牌后只更新 `cardId`、未更新所选 `handCardInstanceId`，造成仍在手中的卡显示“选择已失效”。现在预览通过统一选择入口绑定新实例；新增场景回归检查详情、警告和合成支付按钮。Unity CLI EditMode **282/282** 通过；临时副本 Player 构建并运行成功，截图：`Temp/ui-card-readability-after.png`，运行日志：`Temp/ui-card-readability-player.log`。
- **Unity 权威手牌上限校验**：RULE-033B 审查发现服务端与共享 Schema 规定手牌最多 7 张，但 Unity 恢复快照和增量私有投影仍按 10 张校验。客户端现统一拒绝超过 7 张的任一方快照/投影，并新增两端超限回归。Unity Test Framework NUnit 报告 **284/284** 通过：`Temp/RULE-033B-hand-limit-editmode.xml`；Editor 日志记载测试完成并请求退出码 0，但 Unity CLI 在子进程关闭阶段未收尾，包装器最终报告运行未完成，因此本次不记为 CLI 命令干净退出。
- **Unity 手牌实例协议校验收口**：继续对齐服务端不变量，恢复快照与私有投影现在都要求 `hand-<数字>` 实例 ID，并拒绝到期玩家不属于本局的费用修正；新增恢复和增量投影回归。Unity CLI 完整 EditMode **286/286** 通过且进程干净退出（exit code 0），报告：`Temp/RULE-033B-hand-instance-boundaries-editmode.xml`。
- **恢复 Minecraft 卡面图标并做 Player 实机复核**：发现本地忽略目录缺少图标，导致所有卡面中央显示占位菱形。按注册清单从本机 Minecraft Java 1.21.10 安装 JAR 提取 74 张原版图标到 `Assets/Generated/MinecraftCardIcons`；来源受控资产仍遵守不提交策略。Unity CLI 使用项目 Development Player 入口构建临时副本并运行预览，详情卡和手牌已渲染实际方块/物品图标。1920×1080 截图：`Temp/ui-card-art-development-build.png`；SHA-256：`90505D24B34000FAFDC57808A3F6A7DB1ECD93463CB984ADDB6EDCF10FAC9614`。普通非 Development Player 不启用此本地读取路径，需用 `scripts/build-demo.ps1 -WithMinecraftAssets -WithWindowsPlayer` 复现完整本机资源构建。
- **手牌上限跨层 Schema 一致性**：Schema 审查发现 Unity 已限制双方最多 7 张，但 MatchSnapshot 未限制手牌数组长度，EventBatch 仍允许本人/对手有 10 张。现快照双方 `hand`/`handCards` 与增量投影双方手牌数量全部统一为 7，并新增 AJV 回归覆盖自己、对手及私有投影超限。Nakama **224/224**、生产 TypeScript 类型检查通过；报告输出见 `server-nakama` 的 `npm test`。
- **RULE-033B 验收矩阵复核**：内容校验、Nakama 224/224、TypeScript、Unity CLI EditMode 286/286 与 Compose 配置均通过；五张目标牌仍为 `PENDING`，协议中未增加回手/悬置状态或事件。实现与验证完成，但当前工作区还有 38 个 tracked 修改和 3 个来源未明 untracked 项；按子任务独立提交要求，B 保持“待工作区分组/提交”，RULE-033C 暂不启动。

## 2026-09-23 RULE-033B 稳定手牌实例（进行中）

- **玩法与协议**：权威手牌升级为 `handCardInstanceId` 实例，逐张费用修正及回合到期事件已接入；同名牌部署/出牌精确选择实例。协议更新至 37、规则集 `prototype-0.62`。Nakama 私有事件投影只返回本人完整手牌实例，对手仅收到手牌数量；网络命令入口拒绝缺失/格式错误的实例 ID。尚未实现 RULE-033C/D 的即时回手或悬置。
- **Unity**：快照 DTO、事件后手牌投影同步、费用显示、详情与部署合法性检查、部署/出牌命令均已接入精确实例；成功出牌后 UI 重新绑定剩余手牌实例。离线 Demo 现以同一列表维护稳定手牌实例，抽牌/生成/掉落/移除/合成同步增删，费用修正按实例读取并在所有回合结束分支到期；合成校验也按所选成品实例排除材料。新增重复卡费用隔离、事件投影及离线实例稳定/到期 EditMode 用例；本机 Unity Roslyn 对 Core、Demo、Networking 与相关 EditMode 测试程序集静态编译成功。
- **验证与未完成项**：服务端 220/220、TypeScript 类型检查/构建、卡牌内容与 smoke 脚本语法检查通过；隔离 Nakama 3.40.0 双端测试验证协议 37、精确实例部署命令与对手实例隐私。Strider 对局 `3c0b5c59-27f9-4e1e-90a1-fd469a5c89ed.brprobe` 在 revision 22/24 验证 FIRE/净火时序；WITHER/Fortress 对局 `34cacfcc-bb59-4395-9aec-8dbe603e4b3c.brprobe` 在 revision 38/40 验证重连与后续状态结算。探针报告在本机目录 `Temp/RULE-033B-online-probe.json`，SHA-256 `826FF022B63494E5F9E2898AAC322811DB4F885FDBAA69EBA3072D57FFE38D62`。Unity 编辑器直接验证仍失败于中国版许可证缺少 `com.unity.editor.headless` entitlement；`validate-unity.ps1` 已加无项目授权预检，失败时在内容同步前退出，当前负向验证确认没有改写 tracked 文件。独立 Roslyn 编译不能替代 EditMode 执行。RULE-033B 仍进行中，不启动 RULE-033C。

## 2026-09-23 RULE-033A 悬置与回手契约冻结

- **实例身份先行**：审计确认当前权威手牌是 `string[]`，部署/出牌依赖 `indexOf(cardId)`；同名卡无法可靠区分被回手的折扣副本。新契约强制先引入稳定 `handCardInstanceId`，命令、支付、合成、Unity 选择和重连都精确绑定实例，禁止用按卡名共享折扣的临时实现。
- **区域与时序**：冻结对象所有权、公开悬置区、回手满手进弃牌堆、清除战场状态且不触发死亡/亡语/掉落。悬置牌按稳定顺序在拥有者下个 `TURN_STARTED` 后、普通抽牌前返回；返回占满手牌时后续返回和正常抽牌按规则爆牌。
- **卡牌边界**：ED-002/005 分别只给返回实例 `-1/-2`；ED-003 战吼可选且仅成功悬置后 +0/+1；ED-006 强制敌方生物。TK-017 只防当前控制者自己的法术，因此 ED-005 非法，ED-002 材料、ED-003 战吼和 ED-006 敌方法术合法。
- **范围、验证与拆分**：规范见 [`../design/end-suspend-return-spec-v1.md`](../design/end-suspend-return-spec-v1.md)，任务包见 [`task-packets/RULE-033.md`](task-packets/RULE-033.md)。A 不改运行时、协议 36、规则集 `prototype-0.61`、效果注册表 47、卡牌定义/catalog 41 或注册状态。内容、TypeScript、服务端 218/218、构建与 Compose 配置通过。直接调用 Unity `6000.0.28f1c1` 时，编辑器因缺少 `com.unity.editor.headless` entitlement 在加载项目之前退出；未把既有 262/262 XML 冒充本次结果。下一唯一任务 RULE-033B 只建立稳定手牌实例与精确费用修正，不接通回手/悬置卡牌。

## 2026-09-23 RULE-032F3 下界状态与召唤证据收口

- **任务闭合**：复核契约 `634a6d6`、炽足兽 `1bfb63a`、WITHER 基础设施 `b09d3aa`、凋灵骷髅 `57fea39`、下界要塞 `92c7fbc`、Unity 演示 `1d6d268` 与 Docker 双端重连 `819eb60`，全部为当前 HEAD 的祖先。`nt_004`、`nt_005`、`nt_008` 均为 `IMPLEMENTED`，RULE-032A—F3 已完整关闭。
- **最终版本与测试**：协议 36、规则集 `prototype-0.61`、效果实现注册表 47、卡牌定义/catalog 41；74 张卡中 60 个效果已实现、9 个待实现、5 张无效果。服务端 218/218；直接调用 Unity `6000.0.28f1c1`，EditMode 262/262。Docker Engine 29.8.0、Nakama 3.40.0、PostgreSQL 16.8 健康。
- **可复核证据**：F1 Play Mode 截图 [`../design/assets/demo-nether-status-summon-preview-v1.png`](../design/assets/demo-nether-status-summon-preview-v1.png) 的 SHA-256 为 `A6DEF84189D046603B31B40F201D677A6E59BC8FD15FFBF3EDB33523FDCA1861`。F2 在线报告 SHA-256 为 `FD2ADA0B7756F3BB24DDE052AD1486AE3C51B3AE0913F13B050B4AB9A540D005`；两个 Match ID、关键 revision 与双方重连恢复证据已写入任务包。
- **范围与后续**：本项只修改证据文档，没有改运行时、视觉、协议或内容。下一唯一任务为 RULE-033A，只冻结悬置/回手契约并拆分后续实现、Unity、联机和证据任务。

## 2026-09-23 RULE-032F2 下界状态与召唤 Docker 双端验收

- **专项双对局探针**：新增 `smoke:nether-status-summon` 与 `scripts/validate-nether-status-summon-online.ps1`。两个真实 Nakama WebSocket 客户端使用正常牌库、起手替换、逐回合红石增长、部署、战斗和结束阶段命令，不注入测试状态；随机起手不足时有界重匹配。炽足兽对局 `4df72230-5bec-4910-9234-3b4a1480777b.biome-rivals` 在 revision 22 完成烈焰人伤害后施加 FIRE，revision 24 严格按 `CARD_DEPLOYED → OBJECT_STATUS_REMOVED → OBJECT_STATS_CHANGED` 完成净火治疗。
- **WITHER、要塞与双重连**：下界/海洋对局 `93fe5fc6-0773-4330-8cbd-ea0fc5c51243.biome-rivals` 在 revision 35 按 `ATTACK_RESOLVED → OBJECT_STATUS_APPLIED` 创建完整来源的两次 WITHER；revision 36 按相邻事件完成要塞 1 点自动支付和最左单位格 `object-4` 的 3/3 `tk_015` 召唤。双方依次替换 socket 后都恢复到同一 revision 36，公开投影逐字段一致，保留 7/0/7 红石、三格 `object-3` 要塞、`nextInstanceId = 5` 与剩余 2 次 WITHER；revision 38 继续按真实伤害与 Tick 将其降为 1 次。
- **投影与产物**：探针对每条命令核对双方 revision、ack、公开 eventId/类型顺序，并对攻击、状态、支付、召唤与 Tick 的完整公开 payload 做双端相等校验。报告位于 `artifacts/nether-status-summon-online-probe.json`，SHA-256 `FD2ADA0B7756F3BB24DDE052AD1486AE3C51B3AE0913F13B050B4AB9A540D005`。
- **环境修复与验证**：Docker Desktop 从 4.48.0 原位升级至 4.91.0，并把三个损坏的 Unix socket 运行时目录移到可恢复备份；未恢复出厂，原镜像与卷保留。Docker Engine 29.8.0、PostgreSQL 16.8 与 Nakama 3.40.0 健康。`scripts/validate.ps1 -WithUnity -WithDockerConfig` 通过，服务端 218/218；直接调用 Unity `6000.0.28f1c1`，EditMode 262/262。未改规则、协议、内容或视觉；下一唯一任务为 RULE-032F3 证据收口。

## 2026-09-22 RULE-032F1 下界状态与召唤 Unity 演示

- **确定性完整链路**：新增 `-previewNetherStatusSummon` 本地 Play Mode 入口，使用真实离线规则依次部署下界要塞、放牧绵羊、凋灵骷髅与炽足兽，执行净火治疗、普通攻击施加 WITHER、要塞自动支付并在最左空格召唤 `tk_015`，再推进到凋零造成 1 点真实伤害后的稳定局面；不是静态摆放或绕过规则的截图状态。
- **世界内反馈与原版素材**：WITHER 新增紫色贴地材质高亮和附着模型的体素凋零粒块；下界要塞在有能量与空单位格时使用岩浆色同步脉冲。三格要塞不再复用通用结构，改用原版 `nether_bricks`、`polished_blackstone_bricks` 与 `magma` 纹理构成桥面、门楼、城垛与双塔；炽足兽、凋灵骷髅、要塞令牌、绵羊和海龟继续复用原版 Minecraft 实体贴图模型。
- **Unity 实际审查**：直接调用 Unity `6000.0.28f1c1` 进入 Play Mode，最终 1920×1080 截图为 [`../design/assets/demo-nether-status-summon-preview-v1.png`](../design/assets/demo-nether-status-summon-preview-v1.png)，SHA-256 `A6DEF84189D046603B31B40F201D677A6E59BC8FD15FFBF3EDB33523FDCA1861`。审查确认 3D 地表高亮随透视投影、模型与双半场地形接触、像素 UI 材质统一且底部结果文案可读；静态截图只能证明最终状态，触发动画时序仍由运行时测试与 Play Mode 日志共同覆盖。
- **验证与范围**：实现提交 `1d6d268`；Unity EditMode 262/262 通过，结果为 `client-unity/Logs/rule032f1-editmode-results.xml`，Play Mode 日志为 `client-unity/Logs/rule032f1-playmode-capture.log`，没有编译或运行时异常。启用 Unity New Input System 原生后端以保证编辑器/运行时交互；未运行 Docker、未改协议、规则集、效果注册表或卡牌内容版本。下一唯一任务为 RULE-032F2，只做 Docker 双端与重连验证。

## 2026-09-22 RULE-032E 下界要塞纵向切片

- **结束阶段事务**：`nt_008` 在海底神殿与僵尸猪灵岩浆之后、对象状态之前建立稳定来源快照，并按起始建筑格/实例 ID 逐座重检来源、当前能量和当前单位格。只有同时存在至少 1 点可用红石与空单位格时，才临时红石优先支付 1 点，紧接着在最左空单位格发布 `OBJECT_SUMMONED` 并创建 3/3 `tk_015`；满场或能量不足不扣费、不分配实例号、不发空事件。
- **边界与恢复**：猪灵先消费最后一点能量时要塞跳过；支付、召唤和新单位监听位于同一个权威命令副本中，内部不可能完成召唤时不会提交部分状态。快照保存支付后的基础/临时/总能量、令牌实例、格位和召唤回合。要塞令牌虽有亡灵/骷髅/凋灵标签，但不会继承 `nt_005` 的 WITHER 触发。
- **Unity 与离线同构**：Unity 状态仓库要求要塞 `REDSTONE_CHANGED(AUTOMATIC_PAYMENT)` 来自行动方存活结构且仍有空格，并要求下一条召唤事件使用同一来源、固定 3/3 令牌和当时最左空格；孤立召唤被拒绝。离线 Demo 同构处理双方结束阶段、满场跳过与临时能量优先支付；联机演出脉冲要塞、HUD 与令牌，并显示“要塞增援”。
- **内容、版本与验证**：冻结后的完整规则文本已同步，`nt_008` / `effect.nt_008.01` 转为 `IMPLEMENTED`。协议维持 36，规则集 `prototype-0.60`→`prototype-0.61`，效果实现注册表 46→47，卡牌定义/catalog 维持 41；当前 60 个已实现效果、9 个预留效果。实现提交 `92c7fbc`；`scripts/validate.ps1` 通过，服务端 218/218；直接调用 Unity `6000.0.28f1c1`，EditMode 261/261，结果位于 `client-unity/Logs/rule032e-final-editmode-results.xml`。下一唯一任务为 RULE-032F1，只做确定性 Play Mode 演示、截图与视觉审查。

## 2026-09-22 RULE-032D 凋灵骷髅纵向切片

- **权威战斗触发**：`nt_005` 在主动攻击或反击对生物造成正数普通伤害后，对仍存活的受伤生物施加 `WITHER`。两个方向均在 `ATTACK_RESOLVED` 后、统一死亡结算前按主动方再反击方的顺序执行；来源即使在同时伤害中降至 0 仍可完成已经成立的触发，死亡目标、英雄、建筑、法术与状态伤害均不触发。
- **刷新、来源与回放**：剩余 1 次的凋零刷新至 2 次并替换为当前凋灵骷髅来源；已经为 2 次时保留原来源和状态数组位置，但仍发布权威应用结果。Unity 状态仓库要求凋零应用能追溯到当前或前一条 `ATTACK_RESOLVED`，核验伤害方向、存活目标、`nt_005` 实例及来源保留规则，拒绝孤立或伪造事件。
- **离线与内容注册**：离线 Demo 采用相同的主动/反击、死亡和刷新顺序，并在攻击结果中报告凋零施加次数。`nt_005` / `effect.nt_005.01` 转为 `IMPLEMENTED`；协议维持 36，规则集 `prototype-0.59`→`prototype-0.60`，效果实现注册表 45→46，卡牌定义/catalog 维持 41，当前为 59 个已实现效果、10 个预留效果。未实现 `nt_008`。
- **验证与提交**：实现提交 `57fea39`。`scripts/validate.ps1` 通过，包含内容同步、TypeScript、服务端 214/214 与构建；直接调用 `D:\Unity 6000.0.28f1c1\Editor\Unity.exe`，EditMode 258/258，结果位于 `client-unity/Logs/rule032d-full-editmode-results.xml`。下一唯一任务为 RULE-032E，只实现下界要塞结束阶段的原子支付与令牌召唤。

## 2026-09-22 RULE-032C WITHER 状态基础设施

- **权威状态生命周期**：协议新增仅限战场生物的 `WITHER`。状态默认持续两次目标控制者结束阶段，每次按对象格位/实例 ID、再按对象状态数组顺序造成 1 点真实伤害；同名状态不叠加且不移动数组位置。剩余 1 次刷新到 2 次时替换完整来源，已经为 2 次时保留旧来源。
- **死亡、来源与恢复**：凋零来源实例离场后仍由快照保存玩家、卡牌、实例和效果 ID；致死时用保存的玩家归属进入统一死亡、亡语和掉落链，不再发布死者的 Tick/移除事件。快照投影、Schema 和 Unity 重连恢复均保留完整状态；状态伤害事件同时携带临时生命投影，避免客户端错误清零增益。
- **Unity 回放与本地同构**：状态仓库严格核验 `OBJECT_STATS_CHANGED(TRUE DAMAGE) → OBJECT_STATUS_TICKED/REMOVED` 的相邻因果链、来源一致性、两次持续时间与刷新边界，并拒绝建筑凋零、超长持续时间及孤立 Tick。离线 Demo 同样结算两次真实伤害，场景事件演出和对象信息显示凋零文案与紫色状态强调。
- **范围与版本**：协议 35→36、规则集 `prototype-0.58`→`prototype-0.59`；效果实现注册表维持 45、卡牌定义/catalog 维持 41，仍为 58 个已实现效果、11 个预留效果。`nt_005` 保持 `PENDING`，可按现有白板单位策略部署，但不会创建 WITHER；未实现下界要塞。
- **验证与提交**：实现提交 `b09d3aa`。`scripts/validate.ps1` 通过，含服务端 209/209 与构建；直接调用 Unity `6000.0.28f1c1`，EditMode 256/256，结果位于 `client-unity/Logs/rule032c-editmode-results.xml`。下一唯一任务为 RULE-032D，只接通凋灵骷髅的主动攻击/反击触发和内容注册。

## 2026-09-22 RULE-032B 炽足兽纵向切片

- **权威战吼**：`nt_004` 只把己方存活且带 FIRE 的生物视为当前合法角色目标；己方着火建筑/结构和敌方着火对象不计入。有合法目标时缺失、敌方或未着火目标均在支付前原子拒绝；无合法目标时可正常部署。结算按 `CARD_DEPLOYED → OBJECT_STATUS_REMOVED(reason = EFFECT_REMOVED) → OBJECT_STATS_CHANGED(reason = HEAL)`，满血目标也发布治疗结果。
- **Unity 回放与交互**：状态仓库核验移除和治疗必须来自同控制者的存活炽足兽，且治疗必须紧跟匹配的 FIRE 移除。离线 Demo 保持相同目标与结算语义；场景控制在有合法目标时复用 3D 世界内模型/地表高亮并强制先锁定目标，无目标时开放部署格、显示无目标提示。成功净火会脉冲目标并显示“净火疗愈”。
- **内容与版本**：`nt_004` 和 `effect.nt_004.01` 转为 `IMPLEMENTED`；协议 34→35，规则集 `prototype-0.57`→`prototype-0.58`，效果实现注册表 44→45，卡牌定义/catalog 维持 41。当前为 58 个已实现效果、11 个预留效果；未引入 WITHER 或下界要塞运行时逻辑。
- **验证**：`scripts/validate.ps1` 通过，包含内容同步检查、TypeScript 类型检查、204/204 服务端规则测试与构建。直接调用 `D:\Unity 6000.0.28f1c1\Editor\Unity.exe` 编译并运行 EditMode，250/250 通过；结果位于 `client-unity/Logs/rule032b-editmode-results.xml`。下一唯一任务为 RULE-032C，只建立 WITHER 状态基础设施并保持 `nt_005` 为 `PENDING`。

## 2026-09-18 RULE-032A 下界状态与召唤规则契约

- **炽足兽**：冻结“角色”为英雄与生物，不含建筑/结构；只允许选择实际带 FIRE 的己方存活角色。有合法目标时必须选择，无目标时仍可部署；权威顺序固定为部署与入场监听 → 移除 FIRE → 治疗 1 → 触发/死亡/胜负检查。
- **凋灵与凋灵骷髅**：NT-005 的主动攻击和反击都可在造成正数普通攻击伤害后，对仍存活的生物施加 WITHER。WITHER 在对象控制者结束阶段造成 1 点真实伤害、持续 2 次、不叠加；刷新、来源、击杀掉落、状态事件和终局中止均已定案。
- **下界要塞**：冻结在猪灵岩浆之后、状态之前结算；多实例按起始建筑格和实例 ID 排序，每座重新检查空格与能量，临时红石优先支付，并在最左空单位格召唤 TK-015。满场或费用不足不扣费，支付成功与召唤必须原子完成。
- **范围与版本**：规范见 [`../design/nether-status-summon-spec-v1.md`](../design/nether-status-summon-spec-v1.md)。本项不改运行时代码、协议 34、规则集 `prototype-0.57`、效果注册表 44 或卡牌定义/catalog 41；下一唯一任务为 RULE-032B 炽足兽纵向切片。

## 2026-09-18 RULE-031E3 下界自伤与临时能量证据收口

- **提交链闭合**：规则契约 `399c7a9`、首次掉血标记 `e61d1b7`、临时红石状态/到期 `f9f622e`、临时优先支付 `3d1a70d`、猪灵成长 `2f79a7d`、猪灵岩浆 `b758f31`、重生锚 `2a006ef`、Unity 确定性演示 `72032e6`、Docker/重连探针 `6825ee5` 形成连续祖先链。`nt_002` 与 `nt_007` 均为 `IMPLEMENTED`，RULE-031 无未提交的玩法或验收工作。
- **最终版本与验证**：协议 34、规则集 `prototype-0.57`、效果实现注册表 44、卡牌定义/catalog 41；69 个有文本效果中 57 个已实现、12 个预留。服务端 201/201；最终审查再次直接调用 Unity `6000.0.28f1c1_a1337fc966e0`，EditMode 246/246。内容、TypeScript、构建与 Compose 配置均通过。
- **可复核证据**：E1 1920×1080 Play Mode 图为 `client-unity/Logs/nether-trigger-lifecycle-e1-final-20260918.png`（SHA-256 `D3BBDE29A6BAB47A4660D75AF8470AEC3C883E91698FD2D185D3A8CEAC6AA8ED`）；E2 专项对局 `7b1263ac-b44c-4079-96e4-bed08c708e83.biome-rivals` 为 trigger/reconnect revision 26、final revision 28；Unity 双 Player 对局 `d1150229-86b1-410d-8b73-01377f1fe08a.biome-rivals` 收敛到 FINISHED revision 12。
- **边界与后续**：两种内容版本字段仍按不同职责独立存在，交由 DATA-040 明确约束；无 GPU 联机探针的 Shader unsupported 日志不代表视觉失败，视觉证据只取实际 Play Mode 截图。下一唯一任务为 RULE-032A，仅冻结炽足兽、凋灵骷髅与下界要塞的规则契约。

## 2026-09-18 RULE-031E2 下界触发器 Docker 双端与重连验收

- **专项权威探针**：新增 `scripts/validate-nether-trigger-online.ps1` 与 `smoke:nether-trigger`。两个真实 Nakama WebSocket 客户端使用正常下界牌库、起手替换、抽牌、部署与出牌命令，在不注入测试状态的前提下凑齐至少两座重生锚、僵尸猪灵和熔岩献祭；随机牌序导致手牌无法继续时会有界重匹配，不能把未完成对局误报为通过。
- **事件与恢复证据**：成功对局 `7b1263ac-b44c-4079-96e4-bed08c708e83.biome-rivals` 在 revision 26 依次发布 `CARD_PLAYED → HERO_DAMAGED → HERO_LIFE_LOSS_MARKED → OBJECT_STATS_CHANGED → REDSTONE_CHANGED ×2 → CARD_DRAWN`。触发端随即替换 socket，私有恢复快照仍为 revision 26、临时红石 2、猪灵 3/3、两座锚；revision 28 的结束批次按序完成猪灵自动支付、1 点普通伤害、剩余临时红石到期和回合移交。双方对每个 revision 的公开事件 ID/类型完全一致，完整报告位于 `artifacts/nether-trigger-online-probe.json`。
- **Unity 双客户端回归**：直接调用 Unity `6000.0.28f1c1_a1337fc966e0` 重新生成 Demo 场景与 Windows Player。两个 `-batchmode -nographics` Player 在对局 `d1150229-86b1-410d-8b73-01377f1fe08a.biome-rivals` 中完成设备认证、匹配、起手、部署、回合切换、攻击、强制断线重连、继续操作与投降，最终共同收敛到 FINISHED revision 12 和同一胜者。无网络/状态异常；无 GPU 模式下的 Shader unsupported 日志不作为视觉证据。
- **验证与版本**：服务端 201/201；Docker Desktop 28.5.1、Nakama 3.40.0，运行模块确认为协议 34 / `prototype-0.57`。本项未修改规则、协议、卡牌内容或 Unity 视觉，版本均不变。

## 2026-09-18 RULE-031E1 下界触发器确定性演示审查

- **完整本地链路**：新增 `-previewNetherTriggerLifecycle`，在同一确定性 Play Mode 场景中串联疲劳首次掉血、僵尸猪灵永久成长至 3/3、两座重生锚授予 2 点临时红石、猪灵岩浆优先消耗其中 1 点并造成 1 点伤害、剩余 1 点到期；最终双方英雄均为 29、基础红石保持 9/9。
- **交互与可读性修正**：对手行动时右侧卡牌详情不再错误提示选择部署格，改为明确锁定手牌操作；部署预览在非己方回合完全关闭。底部状态文本启用 10–14 像素自动适配并压缩链路文案，完整信息可在材质化容器内读完。
- **Unity 实机证据**：直接调用 Unity `6000.0.28f1c1_a1337fc966e0`，EditMode 246/246；实际 Play Mode 1920×1080 截图为 `client-unity/Logs/nether-trigger-lifecycle-e1-final-20260918.png`。画面确认原版僵尸猪灵与两座重生锚模型、3/3、29/29、9/9、对手行动锁定提示和无误导部署高亮。E1 未运行或修改 Docker/Nakama。

## 2026-09-18 RULE-031D 重生锚纵向切片

- **权威触发闭环**：`nt_007` 仅在控制者自己的回合、英雄首次非致死实际掉血后触发；英雄掉血标记和单位监听完成后，存活重生锚按建筑格 0→2、实例 ID 稳定顺序逐座发布 `REDSTONE_CHANGED(reason = TEMPORARY_GRANTED)`。护甲完全吸收、对手回合掉血、同回合后续掉血与致死伤害均不授能。
- **资源与双端同构**：每座锚增加 1 点公开临时红石而不改变基础红石或容量，允许总量超过 10；普通支付和猪灵岩浆继续优先使用临时池，未使用部分在结束阶段合法效果后到期。Unity 状态仓库校验来源、存活建筑、己方行动回合、首次掉血标记和逐点增长；离线 Demo 覆盖多锚、优先支付、疲劳触发、对手回合屏蔽与到期。
- **Unity 视觉与资源管线**：新增 `-previewRespawnAnchor` 确定性 Play Mode 场景、权威事件的充能提示和场内脉冲。重生锚使用原版 `respawn_anchor_top`/`respawn_anchor_side1`、黑曜石与萤石构成专用 2.5D 方块模型；Minecraft 纵向动画 PNG 提取统一裁取首帧，修复卡图被渲染为紫色细线的问题。最终画面明确显示 `2/7` 与“临时 +2”。
- **注册与验证**：协议维持 34，规则集 `prototype-0.56`→`prototype-0.57`，卡牌定义/catalog 版本维持 41，效果实现注册表 43→44；`nt_007` 转为 `IMPLEMENTED`，合计 57 个已实现效果、12 个预留效果。服务端 201/201；Unity `6000.0.28f1c1_a1337fc966e0` EditMode 244/244。Unity 实际 Play Mode 的 1920×1080 截图为 `client-unity/Logs/respawn-anchor-preview-20260918.png`。

## 2026-09-18 RULE-031C2 僵尸猪灵岩浆与完整注册

- **结束阶段权威结算**：每个存活 `nt_002` 在控制者结束阶段按单位格、实例 ID 稳定顺序逐只重新读取可用红石；足够时通过统一支付函数优先扣除 1 点临时红石，再发布 `REDSTONE_CHANGED(reason = AUTOMATIC_PAYMENT)`，随后对敌方英雄造成 1 点普通伤害。结算插在海底神殿之后、状态伤害之前；不足的实例跳过且不发空事件，最后 1 点红石只供最先轮到的实例使用。
- **伤害与终局边界**：护甲可以完全吸收岩浆且不占首次掉血窗口；非致死实际掉血继续复用 C1 的首次标记与猪灵成长链。致死伤害在本次支付和伤害后立即停止其余猪灵、状态与回合移交。Unity 状态仓库要求每个猪灵岩浆伤害紧跟同来源的一点自动支付，并校验临时池优先、存活来源、行动方、普通伤害及生命/护甲投影。
- **离线、UI 与内容注册**：离线 Demo 新增基础＋临时总能量的优先支付与到期语义，双方猪灵均能在各自结束阶段结算；联机事件演出显示“岩浆喷射”并高亮来源模型。`nt_002` 的冻结规则文本已同步到共享内容、Unity 和服务端 catalog，效果状态改为 `IMPLEMENTED`，可从手牌正常部署。卡牌详情与手牌规则文字均保持居中并启用自动字号适配，避免完整规则被截断。
- **版本与验证**：协议维持 34，规则集 `prototype-0.55`→`prototype-0.56`；卡牌定义/catalog 版本维持 41，效果实现注册表 42→43，共 56 个 `IMPLEMENTED`、13 个 `PENDING`。服务端 198/198；Unity `6000.0.28f1c1_a1337fc966e0` EditMode 240/240。Unity 实际进入 Play Mode 的专用 `-previewPiglinMagma` 场景显示两只原版僵尸猪灵按序支付、敌方从 30 降至 28、己方能量归零，1920×1080 截图为 `client-unity/Logs/piglin-magma-preview-20260918.png`。

## 2026-09-18 RULE-031C1 僵尸猪灵首次实际掉血成长

- **权威触发**：`nt_002` 存活在场时，其拥有者英雄在任一玩家回合第一次发生非致死实际掉血后，紧跟 `HERO_LIFE_LOSS_MARKED`，按单位格 0→3 为每个存活实例发布 `OBJECT_STATS_CHANGED(reason = PERMANENT_STAT_MODIFIER)`。每次永久增加 1 点攻击、1 点当前生命与1点最大生命；已有伤口比例不被重算，临时攻防修正及到期回合保持不变。护甲完全吸收、同回合后续掉血、首次掉血后的晚入场实例和致死掉血均不触发。
- **Unity 与离线同构**：Unity 状态仓库核验标记窗口、卡牌/实例来源、精确 +1/+1 和临时修正不变，拒绝矛盾的猪灵成长事件；重连快照直接恢复永久后的最终属性。离线 Demo 以统一英雄生命观察入口覆盖法术、疲劳、普通攻击、装备反击、出土陷阱、末影水晶脉冲与亡语，并在回合交接重置双方首次掉血窗口。
- **版本与边界**：协议形状维持 34，规则集 `prototype-0.54`→`prototype-0.55`，内容版本 41/42 不变。`nt_002` 继续 `PENDING` 且服务端与离线 Demo 都拒绝从手牌部署；本切片没有实现结束阶段岩浆支付/伤害，也没有修改 `nt_007`。
- **验证**：服务端 194/194；`scripts/validate-unity.ps1` 使用 Unity `6000.0.28f1c1_a1337fc966e0`，EditMode 236/236。另由 Unity 编辑器实际打开 Demo 场景、进入 Play Mode，并以双方不同群系和地表悬停状态输出 1920×1080 渲染截图 `client-unity/Logs/review-current-20260918.png`，用于确认当前 2.5D 透视、半场拼接与 UI 实机表现。

## 2026-09-15 RULE-031B2b 临时红石优先支付

- **权威费用**：部署、法术、材料与装备的红石费用统一检查基础＋临时总量，付款先用临时池、不足再扣基础池；合成材料支付不碰两池。可复用的 `trySpendRedstone` 在费用不足时返回 false 且两池均不改变，供后续岩浆自动支付使用。费用不足的实际命令保留原拒绝码，手牌、revision、事件游标、实例编号和去重表均不推进。
- **Unity 回放**：卡牌支付事件和 `REDSTONE_CHANGED(reason = AUTOMATIC_PAYMENT)` 核对前后两池变化，拒绝“临时未用却先扣基础”的伪事件；合成事件核对两池完全不变。联机 HUD 与可打出条件沿用 B2a 的总可用量，部分支付后的临时余量可经 Nakama 私有重连快照恢复。
- **版本与边界**：协议形状维持 34、规则集 `prototype-0.53`→`prototype-0.54`，内容版本 41/42 不变；`nt_002`/`nt_007` 仍 `PENDING`，没有实际自动授能或岩浆效果。后续将僵尸猪灵拆为 C1 永久成长、C2 岩浆与完整注册。
- **验证**：`scripts/validate.ps1 -WithDockerConfig` 通过，服务端 191/191；`scripts/validate-unity.ps1` 使用 Unity `6000.0.28f1c1_a1337fc966e0`，EditMode 233/233。覆盖部署、法术、材料、装备跨池、合成不扣池、自动支付函数、双命令原子拒绝、回合末余量到期、重连投影及 Unity 逐事件拒错。Docker 真实在线对局未在 B2b 单独运行。

## 2026-09-15 RULE-031B2a 临时红石状态、回放与到期

- **资源模型**：服务端保留 `redstone` 为基础可用量，新增当前行动方独有的 `temporaryRedstone`（0–3），快照派生并公开 `totalRedstone = redstone + temporaryRedstone`。基础上限仍为 10，故 10/10 + 临时 2 能公开显示总可用 12/10；结束阶段现有合法效果结算后、`TURN_ENDED` 前会发 `REDSTONE_CHANGED(reason = TEMPORARY_EXPIRED)` 并清空剩余临时量。若提前终局，同样先发布到期资源事件，再发布 `MATCH_ENDED`，避免最终快照留下非行动回合临时池。
- **协议与 Unity**：协议 33→34、规则集 `prototype-0.52`→`prototype-0.53`，卡牌部署、出牌、装备及 `TURN_STARTED` 事件携带基础/临时/总量；独立资源事件预留授能、自动支付和到期原因。Schema 校验范围、非行动方临时池及事件类型，跨字段求和由服务端派生与 Unity 入站状态仓库校验。Unity 联机视图使用总量作可用能量，HUD 有临时量时显示第二行“临时 +N”；重连快照保留三池。
- **边界与验证**：本切片不授能、不实现临时池优先支付，`nt_002`/`nt_007` 仍 `PENDING`；内容版本 41/42 不变。`scripts/validate.ps1 -WithDockerConfig` 通过，服务端 183/183；`scripts/validate-unity.ps1` 使用 Unity `6000.0.28f1c1_a1337fc966e0`，EditMode 229/229。测试覆盖超基础上限、非法池、公开及私有重连快照、资源事件回放、矛盾总量拒绝、结束阶段与终局到期及 UI 投影。Docker 真实在线对局尚未在本切片单独验证。

## 2026-09-15 RULE-031B1 首次英雄实际掉血标记

- **权威伤害窗口**：玩家状态新增 `heroLifeLostThisTurn`。每次英雄生命变化由统一事件出口观察：普通/真实伤害、攻击和装备反击、疲劳、建筑脉冲与亡语均使用同一判定；只消耗护甲或致死掉血不发布标记。首次非致死实际生命下降紧跟源事件发布 `HERO_LIFE_LOSS_MARKED`，同一单人回合中每名玩家至多一次；`TURN_ENDED` 对双方一起清零，回合交接后的疲劳可重新标记。
- **协议与重连**：协议 32→33、规则集 `prototype-0.51`→`prototype-0.52`；玩家快照和事件 Schema、Nakama 私有重入快照、Unity 状态仓库与重放同步。Unity 核对源事件、前后生命、护甲、回合、操作者和事件编号，拒绝护甲-only 伪标记。卡牌定义/catalog 版本 41、效果实现注册表版本 42 不变，`nt_002`/`nt_007` 继续 `PENDING`，未增加临时能量字段。
- **验证**：`scripts/validate.ps1 -WithDockerConfig` 通过，服务端 178/178；`scripts/validate-unity.ps1` 使用 Unity `6000.0.28f1c1_a1337fc966e0`，EditMode 226/226。覆盖双玩家、重复伤害、护甲-only、攻击/反击、疲劳、建筑、致死中止、单人回合重置、公开快照、私有重连和 Unity 回放。Docker 在线双客户端暂未在 B1 切片单独运行，留给 RULE-031E 拆分验收。
- **提交边界**：只提交 B1 的权威模型、协议、Unity 投影、测试和任务文档；本机未追踪的 `client-unity/ProjectSettings/PackageManagerSettings.asset`、`scripts/add_units.py` 保持原状。

## 2026-09-15 北极熊＋羊毛集成演示与联机回归

- **演示提交 `c2ed046`**：`-previewPolarBearWool` 确定性预置英雄 15 点生命、原版北极熊部署和一张羊毛施放，场上北极熊为 4/7；世界内三行标签区别显示嘲讽、永久攻 +1、临时血 +1，右侧与手牌复用统一卡面。1920×1080 实机验收图见 [`demo-polar-bear-wool-preview-v1.png`](../design/assets/demo-polar-bear-wool-preview-v1.png)，无文字溢出或布局遮挡。`scripts/validate-unity.ps1` 为 223/223；`scripts/build-demo.ps1 -WithWindowsPlayer` 和 Player 截图捕获通过。
- **恢复测试提交 `36ce93c`**：服务端通过权威 `PLAY_CARD` 施放羊毛后替换旧 session，重入者独享的 opcode 4 快照保留当前/最大生命、临时生命修正、过期回合、revision 与事件游标；Unity 状态仓库和 Demo 视图也验证快照替换后的属性恢复。`scripts/validate.ps1 -WithDockerConfig` 通过，服务端 174/174；`scripts/validate-unity.ps1` 通过，Unity EditMode 224/224。
- **真实联机**：当前构建的 Nakama 日志确认模块加载为协议 32、规则集 `prototype-0.51`。`npm run smoke:integration --workspace server-nakama` 对局 `9b2a5a77-5ed6-4b27-a04f-d14fa799fb9b.biome-rivals` 到 revision 3；`scripts/validate-online-demo.ps1` 两个 Windows Player 在首端强制断线重入后收敛到对局 `c052f850-8cd6-4c1a-acd6-62cea846183a.biome-rivals` 的 FINISHED revision 11、胜者 `bfa161a4-f933-4244-b2f3-fb15a93e1ae8`。报告与日志位于 `artifacts/online-probe-{a,b}.json`、`client-unity/Logs/online-probe-{a,b}.log`。
- **版本边界**：E1/E2 均未升级协议、规则集或卡牌版本。当前效果实现注册表 `contentVersion = 42`；卡牌定义注册表及生成 catalog 的 `contentVersion = 41`。内容校验通过且无生成物漂移，但两个版本字段的独立语义未被验证脚本明确约束，留给 DATA-040 决定是否统一。
- **剩余风险**：真实双端对局没有施放羊毛，不能声称完成“带羊毛状态的双端端到端重连”；该字段恢复目前由服务端及 Unity 针对性测试证明。联机探针使用 `-nographics`，其日志有无 GPU 时的 Shader unsupported 报错，不能用它判断视觉质量；视觉验收来自独立 Player 截图。Unity 批处理有一次测试成功后未在 60 秒内退出，验证脚本仅在确认完成日志后停止了对应进程。

## 2026-09-15 羊毛临时生命纵向切片

- **权威规则**：TK-001「羊毛」现在可在主行动阶段从手牌指定一个存活己方生物。0 费仍校验行动、阶段、手牌和稳定目标；合法施放进入弃牌并计入本回合出牌，产生 `CARD_PLAYED → OBJECT_STATS_CHANGED(reason = TEMPORARY_HEALTH_MODIFIER)`。每张牌令当前/最大生命同时 +1，并累计到施放者本回合结束时统一撤销。
- **叠加与原子性**：两张羊毛叠加 +0/+2；过期时只发一个事件、保留受伤并在需要时夹取当前生命。缺失、敌方、建筑、英雄、离场目标均在支付前拒绝，不推进 revision、命令去重表或任何卡牌/资源状态。
- **Unity 交互**：复用 3D 地表合法目标选择，只标记存活己方单位；权威属性事件和本地规则接受后才脉冲目标。世界标签区别显示“羊毛护持 +N 临时生命”，并从永久生命成长的计算中排除临时值。
- **版本与内容**：协议维持 32；规则集升级至 `prototype-0.51`，效果实现注册表版本升级至 42，卡牌定义/catalog 版本仍为 41。74 张牌中 55 个效果 `IMPLEMENTED`、14 个 `PENDING`，其余待办效果未改变。
- **验证**：`scripts/validate.ps1` 通过，服务端 173/173；Unity `6000.0.28f1c1` EditMode 222/222。确定性演示、Windows Player、Docker/Nakama 双客户端和截图留给 RULE-030E。

## 2026-09-15 临时生命协议基础设施

- **协议形状**：战场快照与 `OBJECT_STATS_CHANGED` 新增 `temporaryHealthModifier`、`temporaryHealthModifierExpiresOnTurn`；协议升级至 32，规则集升级至 `prototype-0.50`，内容版本仍为 41。
- **权威过期**：服务端在结束阶段既有触发与状态结算之后、`TURN_ENDED` 之前统一撤销临时攻击和临时生命。生命上限回退后会夹取当前生命；两类修正同时到期只发出一个 `TEMPORARY_EXPIRED` 事件，不伪造伤害或死亡触发。
- **校验与回放**：服务端拒绝负数、非整数、数值/过期字段不同步及侵蚀基础最大生命的状态；JSON Schema 拒绝零值/过期矛盾。Unity 权威状态仓库可无损恢复和回放新字段，离线 Demo 使用同构过期流程。
- **范围边界**：`tk_001` 羊毛仍为 `PENDING`，效果计数保持 54 个 `IMPLEMENTED`、15 个 `PENDING`；本切片不增加任何可创建临时生命修正的卡牌入口。
- **验证**：服务端规则测试 169/169；Unity `6000.0.28f1c1` EditMode 220/220。Windows Player、Docker 双端和视觉验收留给 RULE-030E。

## 2026-09-15 北极熊低生命部署加成

- **权威规则**：SI-005「北极熊」在部署时读取控制者英雄当前生命；生命不高于 15 时，基础 3/6 在 `CARD_DEPLOYED` 和既有动物入场监听之后永久获得 +1 攻击。生命 16 不触发，部署后的治疗或受伤不会追溯增减。
- **事件与版本**：复用 `OBJECT_STATS_CHANGED(reason = PERMANENT_STAT_MODIFIER)`，携带北极熊来源实例和最终属性；协议结构保持 31，规则集升级为 `prototype-0.49`，内容版本升级为 41。实现注册状态变为 54 个 `IMPLEMENTED`、15 个 `PENDING`。
- **Unity 同构**：离线 Demo 使用相同的 15 点边界与一次性部署检查，并提供范围校验的英雄生命重置入口供确定性测试；权威回放测试确认永久攻击变化不会破坏 `TAUNT` 或误写临时攻击字段。
- **验证**：服务端规则测试 166/166；Unity `6000.0.28f1c1` EditMode 217/217。截图、Windows Player 和 Docker 双端验收按拆分边界留给 RULE-030E。

## 2026-09-14 账户服务与最小大厅外壳

- **认证解耦**：新增 `IPlayerAccountService` / `IPlayerAccountBackend` / `IPlayerAccountSessionProvider`。设备认证、token 恢复、资料读取、改名和退出集中到 Nakama 账户适配器；实时传输只消费已认证 session，不再拥有身份生命周期。
- **可恢复状态机**：账户明确区分未登录、认证中、就绪、更新中、退出中和失败；认证失败或取消后可重试，跨对局复用同一账户会话。
- **大厅顶栏**：顶部材质化容器同时显示游客身份、当前原型卡组、连接状态与匹配入口；左侧栏从“群系”明确为“卡组”。实机布局已收窄到标题栏和回合栏之间，不再发生容器重叠。
- **配置边界**：连接资源缺失时直接报错，移除编译进代码的 server-key 后备值；开发配置及环境变量覆盖路径保持可用。
- **自动验证**：账户服务测试覆盖成功复用、失败重试、取消重试、改名、退出和输入校验；双客户端报告额外核对账户 Ready、账户 ID 与权威 viewer 一致及显示名非空。
- **验收结果**：Unity `6000.0.28f1c1` EditMode 214/214；Windows Player 构建通过；Docker/Nakama 双客户端对局 `684247e5-23b4-47c0-ad78-1d597c2e17f1.biome-rivals` 在完成强制断线恢复与动作链后收敛到 revision 16。视觉验收见 [`account-lobby-shell-preview-v1.png`](../design/assets/account-lobby-shell-preview-v1.png)。

## 2026-09-14 断线重连与权威状态恢复

- **旧会话替换**：Nakama 在双人房已满时允许同一 `userId` 的新 session 重入，并在加入阶段原子移除该玩家的旧 presence，消除 socket 已重连但服务端尚未来得及清理旧会话时的“match is full”竞态。
- **私有恢复快照**：重入者收到只投递给新 session 的 opcode `4` 快照；测试覆盖 revision/event 游标、等待选择及其私有选项、隐藏对手手牌，并断言权威去重表不会投影到客户端。
- **客户端故障边界**：快照和事件批次同时校验协议与规则集；不兼容后 Gateway 锁定为 `Failed`，底层 socket 的后续 `Ready` 不能误解锁交互。命令自身也同时校验协议与规则集。连接进入 `Reconnecting` 时立即结束所有结果未知的 pending 命令，避免等待超时或重复操作。
- **真实断线探针**：新增 `-autoReconnectProbe` 自动化诊断入口。Windows 双客户端验证会强制关闭第一端 socket，确认 `Reconnecting → Ready`、原 Match ID 和新快照恢复后，再继续部署、阶段切换、结束回合、攻击与投降。本次对局 `08ede1b0-4b1c-49b5-a978-474c270b0b3b.biome-rivals` 最终双方收敛到 revision 18、相同胜者，首端报告 `reconnectRecovered=true`。
- **验证**：服务端 163/163；Unity `6000.0.28f1c1` EditMode 208/208；Windows Player 构建及真实 Docker/Nakama 双客户端探针通过。

## 2026-09-14 Unity 双客户端权威对局探针

- **补齐投降边界**：为 `MatchCommandFactory` 和 `DemoOnlineMatchSession` 增加 `CONCEDE` 命令入口，Unity 不再只能被动接收投降事件。
- **真实动作链**：`-autoOnlineAction` 从仅发送 `ENTER_COMBAT` / `END_TURN` 升级为完整最小对局：双方确认起手，按当前手牌和能量部署安全生物，轮转阶段并结束回合，召唤疲劳结束后攻击敌方英雄，最后由攻击方投降。
- **交叉验证**：两个报告必须确认相同 Match ID、不同设备玩家、镜像阵营、相同最终 revision/胜者、FINISHED 状态、生命变化和双方可见的场上单位；部署、攻击、结束回合、投降四类动作缺一即失败。
- **实机结果**：本地 Docker Desktop 28.5.1、Nakama 3.40.0 与两个当前 Windows Player 完成对局，双方收敛到 revision 13；Unity 编译及 203/203 EditMode 测试通过，客户端探针日志无异常。

## 2026-09-14 烈焰人、烈焰棒与 FIRE 权威状态闭环

- **规则切片**：NT-003「烈焰人」在主动攻击或反击造成普通伤害且目标仍存活后施加 `FIRE 2`；被敌方击杀时向击杀者生成 TK-013「烈焰棒」。烈焰棒先对存活敌方生物造成 1 点普通伤害，目标存活时再施加或刷新着火。
- **状态时序**：`FIRE` 在受影响对象控制者的两次结束阶段各造成 1 点真实伤害，随后移除；刷新不叠加，保留最新持续时间与权威伤害来源。致死会立即进入统一死亡、掉落与终局结算，不创建无意义状态，也不继续执行已失效的阶段步骤。
- **协议与内容**：升级至 `protocolVersion 31`、`rulesetVersion prototype-0.48`、内容版本 40；快照和状态生命周期事件公开 `FIRE`，Unity 状态仓库执行同构校验。卡牌效果注册状态更新为 53 个 `IMPLEMENTED`、16 个 `PENDING`。
- **离线表现**：Demo 可确定性预置烈焰人与烈焰棒，复用 3D 地表目标选择、原版烈焰人体素模型和状态显示；视觉验收见 [`demo-blaze-fire-preview-v1.png`](../design/assets/demo-blaze-fire-preview-v1.png)。
- **验证**：`scripts/validate.ps1` 通过，包括内容同步校验、TypeScript 类型检查、162/162 服务端规则测试和服务端构建；`scripts/validate-unity.ps1` 使用 Unity `6000.0.28f1c1` 通过 202/202 EditMode 测试。

## 2026-09-14 场地扩建、群系装饰与光影强化

- **扩大场地**：体素地形由 23×13 扩展至 **23×17**（玩家半场 z −8…−1、敌方半场 z 1…8），地基与四周边框同步放大；靠近边框的两排地面略抬升形成缓阶；河道加密石块并新增砂砾河岸（新纹理 `gravel`）。部署格与棋盘坐标不变，UI 投影映射不受影响。
- **群系装饰随阵营切换重建**：装饰迁入独立的 `BattlefieldDecor` 子树，`RebuildDecor()` 在 `BuildNow` 与每次 `SetBattlefieldThemes` 时按双方群系重建。七种群系各配装饰套件：平原=橡树/灌木丛（新纹理 `oak_log`）、沙漠=仙人掌×4+沙堆、冰原=冰柱×4+雪堆、深暗=巨石+黑橡树干、海洋=珊瑚堆+海晶灯、下界=玄武岩柱+火苗+岩浆池、末地=黑曜石柱+紫珀块。每侧两根**萤石灯柱**（新纹理 `glowstone`），挂群系环境色的点光源（随装饰一起销毁重建）。
- **光影强化**：太阳仰角 48° → 38°、方位 −32° → −30°，拉长全场阴影；阴影浓度 `shadowStrength` 提至 0.82；灯柱点光源提供近景氛围光。
- **测试**：七群系循环新增装饰重建断言（每群系地标装饰、双侧灯柱、装饰子树规模），202/202 通过。
- **视觉验收**：默认场景 [`demo-voxel-battlefield-preview-v3.png`](../design/assets/demo-voxel-battlefield-preview-v3.png) 与沙漠 vs 末地 [`demo-faction-desert-end-preview-v3.png`](../design/assets/demo-faction-desert-end-preview-v3.png) 通过——地形延伸至画面边缘无黑洞、群系装饰与灯柱可见、长影子清晰、部署格与棋子无遮挡、无渲染错误；溺尸与末影人等原版模型在逐面明暗下清晰可辨。

## 2026-09-14 全部占位模型替换为原版 MC 模型（36 生物 + 3 建筑）

对照卡牌注册表盘点了全部战场对象，把剩余占位模型全部替换为原版 Minecraft 模型：

- **14 个新增生物模型**（此前只有 21/36 个生物卡有专属模型，其余显示通用方块占位）：尸壳、沙漠考古学家（原版沙漠村民皮肤）、恶地劫掠者、商队领头骆驼、北极熊、地牢骷髅、海龟、僵尸猪灵、炽足兽、凋灵骷髅（含要塞凋灵骷髅）、末影螨、末影人、潜影贝、末影龙化身。全部从 bedrock-samples 提取几何 + 配套贴图，接入工厂元数据表（按几何实际比例选择高度/宽度适配、悬停高度）。至此 36 个生物卡全部拥有原版模型，通用方块生物仅作资产缺失时的兜底。
- **3 个建筑换掉通用方块结构**：重生锚（原版 `respawn_anchor_side1/top` 方块纹理 + 四角充能发光）、下界要塞（下界砖墙体 + 双塔 + 橙色窗光）、末地传送门框架（基岩方块环架 + 中央发光核心）。
- **提取脚本增强**：`extract-minecraft-entity-models.ps1` 下载加重试机制（三次尝试），避免网络抖动中断提取。
- **解析/构建修复**：骨骼父级引用大小写不敏感（原版劫掠者文件中 `rightItem` 的父级写作 `rightArm` 而骨骼名为小写）；骆驼与北极熊按高度适配（几何高大于宽，宽度适配会放大变形）；末影龙 `wing1` 骨骼烘焙 180° 偏航（基岩几何把右翅画在左侧、原版靠动画翻转），双翅对称展开后按翼展适配。
- **测试**：`FactoryMetadataCoversEveryEntityCard` 覆盖全部 36 个生物卡；几何集成测试新增 15 个 TestCase 并放宽最宽模型（龙）的包围盒上限。

## 2026-09-13 实体模型精细化：原版逐面明暗、骨骼待机动画与覆盖层

第一轮几何修复后，模型数据正确但观感偏"生硬"：均匀自发光把明暗洗平、模型完全静止、部分原版双层贴图部位缺失。本轮按原版 Minecraft 实体渲染风格细化：

- **新增实体着色器 `BiomeRivals/Demo/Entity`**（`Demo/Art/DemoEntity.shader`）：非光照渲染，`纹理 × 逐面顶点色 × 染色` 并支持雾效与镂空（alpha clip），外加 `_EmissiveBoost` 供火焰类生物（烈焰人、岩浆 cube 材质 0.3）无阴影提亮。删除原先 `_EmissionMap=纹理 + 0.34 均匀自发光` 的洗平做法。
- **构建器烘焙原版逐面明暗到顶点色**：顶面 100%、南北面 80%、东西面 60%、底面 50%（与原版 `RenderType.entityCutout` 的方向明暗一致），模型立刻获得方块感的体积光。
- **新增 `DemoEntityIdleAnimator`**：按骨骼驱动的程序化待机动画（正弦振荡骨骼局部旋转/位移），工厂元数据表为每个实体配置轨道——蜜蜂/蝙蝠扇翅（含翅尖跟随）、烈焰人 12 根火棒交错浮动+旋转、守卫者尾鳍三节摆动、海豚/鲑鱼摆尾、狼摇尾、蜘蛛腿部微颤、亡灵/劫掠者手臂摆动+头部扫视、羊/山羊点头、铁傀儡与雪傀儡手臂摆动等。棋子重建时自动重挂。
- **流浪者兜帽覆盖层**：提取配置新增 Java JAR 的 `stray_overlay.png`（`entity_stray_overlay`），流浪者以 0.55px 膨胀第二层渲染兜帽；绵羊羊毛层膨胀 1.0 → 1.2 更蓬松。
- 材质创建 API 增加 `emissiveBoost` 参数；实体染色固定为白色（原版贴图自带配色）。
- 测试：顶点色逐面明暗断言、蜜蜂扇翅动画轨道断言、流浪者兜帽覆盖层断言。
- 视觉验收：精化后截图 [`demo-voxel-models-preview-v2.png`](../design/assets/demo-voxel-models-preview-v2.png) 通过验收——逐面明暗带来的体积感、羊毛/海晶石像素纹理清晰、旧版均匀自发光造成的"洗平泛光"完全消除，无回归（对比基线见 `demo-voxel-models-preview-v1.png`）。

## 2026-09-13 阵营选择 → 场景显示一致性检查

对"选择阵营后战场是否随之切换"做了全链路核对（代码 + 自动化测试 + 实机截图）：

- **代码链路**：三条路径全部收敛到 `DemoBattlefield3D.SetBattlefieldThemes(playerFaction, opponentFaction)`——玩家侧栏按钮 `SelectFaction`、敌方轮换器 `SelectOpponentFaction`（锁定状态下拒绝切换并提示）、权威联机开局 `ApplyAuthoritativeFactionVisuals`。该调用同步更新：双方地形材质（主纹理/次纹理/副纹理 + 地基 + 远近边框）、七个部署格贴图与主题染色、双方环境灯光；UI 侧的玩家/敌方头像色、界面染色、头衔由 `ApplyPlayer/OpponentFactionVisuals` 同步更新。
- **自动化锁定**：EditMode 测试在七群系切换循环中新增断言——地形方块贴图、玩家/敌方地基贴图、玩家/敌方环境灯光颜色逐群系校验（连同既有的部署格贴图与染色断言），任何一方漂移都会导致测试失败。187/187 通过。
- **实机验证**：通过 `-previewPlayerFaction/-previewOpponentFaction` 命令行参数实拍两组此前未覆盖的组合，均通过视觉验收：
  - 沙漠 vs 末地 [`demo-faction-desert-end-preview-v1.png`](../design/assets/demo-faction-desert-end-preview-v1.png)：红砂岩/紫珀双方地形、红砂/紫珀部署格、沙漠卡组与 `db_` 效果槽、末影水晶悬浮体素模型。
  - 海洋 vs 深暗 [`demo-faction-ocean-cave-preview-v1.png`](../design/assets/demo-faction-ocean-cave-preview-v1.png)：海晶石/苔藓石砖双方地形、海洋卡组、敌方洞穴蝙蝠与洞穴蜘蛛模型。
  - 连同此前的平原 vs 下界（默认开局）、雪原 vs 海洋（`-previewGoat`）两组，四个组合、七种群系材质均有实机证据覆盖。
- 已知保留项不变：橡树/玄武岩柱等装饰为通用布景，不随群系切换。

## 2026-09-13 战场升级为真正的 2.5D 体素模型并修复生物渲染错误

### 背景

此前战场由两套"伪 2.5D"内容组成：

1. **背景与地面**：`DemoSceneBuilder` 把手绘背景图 `demo-battlefield-bg-v1.png` 写入 `DemoBattlefield3D.illustratedBackdrop`，一旦存在该图，`BuildNow()` 直接跳过真实体素地形 `BuildTerrain()` / `BuildBiomeDecor()`，只显示一块贴着手绘插画的全屏 Quad；七个群系主题则依赖 14 张手绘半场图（`field-{faction}-v1.png` / `field-{faction}-far-v1.png`）经 `DemoCompositeBackdrop.shader` 合成。
2. **场上生物**：`DemoMinecraftModelFactory` 用约 380 行手写的立方体 + 手调 UV 坐标拼出 17 种生物模型。这些 UV 是按旧版（重做前）Minecraft 实体贴图布局凭手估算的，而本地提取的实体贴图来自 1.21.10（狼 1.20.5 重做、蝙蝠 1.21.2 重做、鲑鱼 1.21.5 重做等），两者不匹配，导致脸、毛色、翅膜等大面积贴图错乱；透明贴片（蜂翼、烈焰棒等）的镂空也不稳定。

### 调研结论（开源/官方资料）

- Java 版客户端 JAR 中**并不包含**实体几何 JSON（实体模型硬编码在 Java 代码里；本机 1.20.1 与 1.21.10 JAR 均已验证）。
- 实体几何的公开数据源是 **Mojang 官方 bedrock-samples 仓库**（`resource_pack/models/entity/*.geo.json`，即 Blockbench 兼容的 `minecraft:geometry` 格式），配套 `resource_pack/textures/entity/` 贴图可以保证"几何 + 贴图"版本一致。参考资料：[Mojang/bedrock-samples](https://github.com/Mojang/bedrock-samples)、[Microsoft geometry 1.12.0 schema](https://learn.microsoft.com/en-us/minecraft/creator/reference/content/schemasreference/schemas/minecraftschema_geometry_1.12.0)、[Blockbench](https://blockbench.net/)、[skinview3d（box-UV 参考实现）](https://github.com/bs-community/skinview3d)、[SizableShrimp/EntityModelJson（Java 侧导出对照）](https://github.com/SizableShrimp/EntityModelJson)。
- 从实际数据确认的坐标/角度约定：模型正面朝 -Z；**基岩几何的 X 轴与 Java 镜像**（换算时 X 取反）；旋转为角度制，且 X/Z 轴符号与右手系相反（用羊 `bind_pose_rotation: [90,0,0]` 与蜜蜂翅膀姿态验证）；镜像立方体 `mirror` 只水平翻转 UV。

### 改动内容

**资源管线（生成物，均不提交 Git）**

- 新增 `scripts/extract-minecraft-entity-models.ps1` + `shared-schema/card-art/bedrock-entity-source.v1.json`：从 bedrock-samples 固定 commit（`7360724`）下载 17 个实体几何 JSON 与配套实体贴图（部分为 TGA），从本机 1.21.10 JAR 提取 `sheep_wool.png` 羊毛覆盖层，全部写入 `Assets/Generated/MinecraftWorldTextures/Resources/DemoWorld`，并记录 `entity-asset-provenance.local.json`（URL + SHA256）。
- `scripts/extract-minecraft-world-textures.ps1` 只再负责方块纹理；其实体贴图条目移交新脚本，避免同一资源两个来源。
- `scripts/build-demo.ps1 -WithMinecraftAssets` 现在会依次调用实体模型提取脚本。

**运行时（全部提交 Git）**

- 新增 `DemoJsonParser.cs`：无依赖的极简 JSON 解析器（支持 `minecraft:geometry` 与 legacy `geometry.*` 两种根格式）。
- 新增 `DemoMinecraftEntityGeometry.cs`：几何 DTO + 解析器（bones/cubes/uv/rotation/bind_pose_rotation/mirror/inflate/骨骼父子层级）。
- 新增 `DemoMinecraftEntityModelBuilder.cs`：按原版 box-UV 布局构建每个立方体网格（含镜像立方体 UV 翻转、零厚度双面贴片、立方体旋转烘焙、骨骼层级与枢轴、`inflate` 膨胀），并按每卡目标高度自动缩放、按悬停高度落地。生物模型**不再使用任何手写 UV**。
- 重写 `DemoMinecraftModelFactory.cs`：卡牌 → 几何模型/贴图/目标高度/悬停高度元数据表；羊增加羊毛覆盖层（第二层材质 + inflate）；烈焰人的 12 根火棒在几何里是靠动画摆放的，静态模型通过骨骼枢轴覆盖烘焙出经典三环姿态。幼年/小型变体（`tk_003`/`tk_004`/`tk_011`/`tk_014`）保留原有缩放。
- `DemoBattlefield3D`：删除插画背景 Quad 与 `CompositeBackdrop` 依赖；`BuildNow()` 永远构建真实体素地形与群系装饰；地形方块按群系主题取材并记录渲染器，`SetBattlefieldThemes` 运行时整体换材质（含地基、边框、部署格贴图）；部署格地面改用群系方块纹理平铺（不再屏幕投影手绘图）。实体材质改按贴图键缓存，并补上 `RenderType=TransparentCutout` 覆盖标签，修复 Standard 管线下透明贴片的裁剪。
- `DemoBattlefieldThemeCatalog`：由"近端/远端手绘图路径"重构为"群系 → 主/次/副地面方块纹理 + 地基纹理 + 颜色"映射，七个群系全部有真实方块纹理。
- `DemoSceneBuilder`：不再加载任何背景图，`Configure` 签名收敛为 `(blockShader, groundSurfaceShader)`。
- 删除不再使用的资源：`demo-battlefield-bg-v1.png`、14 张 `field-*-v1.png` / `field-*-far-v1.png`、`DemoCompositeBackdrop.shader`。
- `DemoWorldAssetProvider.CreateEntityMaterial` 增加 cutout 渲染队列标签（见上）。

**测试**

- 更新 `DemoLocalMatchTests.GeneratedSceneAndRuntimeHierarchyExist`：`Configure` 新签名、部署格贴图断言改为方块纹理、`_UseScreenProjection=0`、七群系切换断言改为群系主纹理、删除背景 Quad 断言，并新增"玩家/敌方体素地形已构建"断言。
- 新增 `DemoMinecraftEntityModelTests`：解析器（现代/legacy 格式、bind pose）、构建器（骨骼层级、UV 范围、自动缩放、X 镜像、悬停基面、旋转符号约定）、工厂元数据完整性，以及"本机已提取时对全部 17 个实体逐个建模并校验网格/UV/包围盒"的集成测试（未提取时自动 Ignore）。

### 已知限制

- 流浪者兜帽、骷髅披风等需要第二张贴图（overlay）的部位暂未实现双层渲染；烈焰人火棒的待机动画为静态烘焙姿势。
- 群系装饰（橡树、玄武岩柱）仍是通用布景，不随群系切换（地形、部署格、灯光已随群系切换）。
- 实体朝向、镜像与旋转约定来自对官方样本数据的推导与单元测试锁定，若个别模型出现左右贴图互换，调整 `DemoMinecraftEntityModelBuilder` 中 X 镜像与 -X/+X 区域映射即可。

### 视觉验收与追加修复

- 新增 `Demo/Editor/DemoPlaymodeCapture.cs`：命令行进入 Play 模式，复用运行时 `-captureDemo` 机制生成预览截图。
- 截图验收发现并修复：部署格地面在群系切换后仍保留旧染色（`grass_block_top` 等灰度纹理依赖主题色），`SetBattlefieldThemes` 现在同步更新部署格材质 `_Color`，并停止每帧把 `_Color` 强制回白色；测试新增"部署格染色随群系切换"断言。
- 预览截图（视觉验收通过）：[`demo-voxel-battlefield-preview-v1.png`](../design/assets/demo-voxel-battlefield-preview-v1.png)（平原 vs 下界：体素地形、岩浆怪与溺尸模型）、[`demo-voxel-models-preview-v1.png`](../design/assets/demo-voxel-models-preview-v1.png)（雪原 vs 海洋：守卫者、山羊、绵羊羊毛层、悬浮海豚）。

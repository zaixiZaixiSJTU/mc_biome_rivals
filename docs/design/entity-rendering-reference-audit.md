# 生物渲染开源对照（UI-073）

日期：2026-10-04。此文是工程对照，不替代渲染正确性的Unity实测。

当前UI-084：固定Mojang已有独立幼羊geometry/32x32 TGA，client/controller不用成人羊毛叠层；旧baby_transform明确弃用。tk_003已换六原cube的大头短腿/独立皮肤，羊颜色mask和项目待机/落地保持，board高度.9非世界1:1。源配置成对登记、OnlyKeys新目录提取/增量sidecar，成人源hash不变。Unity687/687，最新双模式r2各690来源/74卡图/20诊断隔离通过；144图仅幼羊4变/140不变，四原尺寸/双侧真实部署攻击/原slot射线及None普通成人羊图、三负控制通过。源链接、编译/fixture中间失败和hash见 [UI-084](../development/task-packets/UI-084.md)。未复制上游代码/重绘原资产；完整Molang、PBR/MERS、per-face对象非本项实现。下一 [UI-085](../development/task-packets/UI-085.md) 按原范围联合收口。以下“当前”条目是历史，不把扩展清单自动变成原请求的完成条件。

当前UI-083：将固定Blockbench物理顶点与box UV、PrismarineJS未镜像面的坐标对应到本地X反射后，独立数字/GPU fixture确认共用U方向、mirror东西区域与底面V错误。仅修UV；24新增测试，685/685，双模式684来源/74卡图/20诊断边界通过。144图全部变化且逐张原尺寸看过，源geometry hash全部未变；guardian真实部署攻击/原slot射线及None普通战场、三负控制通过。未复制代码/安装依赖/重绘原素材，保留ZYX和mesh-only binding。证据及旧北面测试期望纠正见 [UI-083](../development/task-packets/UI-083.md)。下一 [UI-084](../development/task-packets/UI-084.md) 原幼羊；per-face UV对象和完整动画/材质仍待。以下各“当前”段落均是历史阶段，不是当前基线。

当前UI-082：两格式Parser忽略所属bone mirror默认值已修，cube显式覆盖和child独立默认保持；与固定Blockbench现代/legacy契约一致。7项新增用例包括原guardian与独立非方atlas北面UV/几何不变，Unity661/661；最新两模式684来源/74卡图/20诊断边界通过。144图94不变、50变化（13登记变体）全部原尺寸看过，guardian真实部署攻击/原格射线和None普通流浪者战场通过。仅修默认继承，未改Builder现有UV算法或源资产，不冒称全部六面正确；详细hash见 [UI-082](../development/task-packets/UI-082.md)。下一 [UI-083](../development/task-packets/UI-083.md) 六面box UV/镜像，原幼羊和完整动画/材质仍待；以下“Parser仍忽略bone mirror”等为历史差异，不是当前实现。

当前UI-081：原烈焰人pivot覆盖改变了旋转轴而未真正展开杆网格；对照固定Mojangclient/controller/move公式，原pivot保持，显式源位置offset先展开再fit，12条三层环绕/上下浮动轨道替换旧杆近似，原头待机保持。Unity654/654；当前双模式684来源/74卡图/20诊断边界通过，144图只有烈焰人4张变化、140不变，四图与真实部署攻击/双侧原格射线、None普通下界场景已看。原资源未改，没有完整Molang、源world-life时钟、charged粒子或body/head双材质；证据/hash见 [UI-081](../development/task-packets/UI-081.md)。下一 [UI-082](../development/task-packets/UI-082.md) 骨骼默认mirror缺省继承；原幼羊/全部六面UV和完整外观目标仍待。以下各阶段记录为历史。

最新UI-080：守卫者依赖原默认动画的眼/尾/十二刺定位已登记为明确静态展开适配；中间失败暴露共用XYZ组合错误。对照固定Blockbench的ModelFormat默认ZYX、Group预览采用Format.euler_order及legacy X镜像，改Rz*Ry*Rx，独立三轴与源蜘蛛逐腿解析式验证。上一UI-079符号修复保留，但“保留XYZ即可正确”的结论已被否定。当前650/650，双模式各684来源/74卡图/20诊断边界通过；144图128不变、16变更原尺寸均已看，守卫者真实部署攻击/双侧原格射线、蜘蛛中毒回归和None普通海洋场景通过。来源/失败与成功hash/边界见 [UI-080](../development/task-packets/UI-080.md)。未复制GPL代码、未改源资产；完整Molang/官方出生和泳姿/六面UV未覆盖。Parser仍只读cube mirror、不继承bone默认mirror，后续独立核查；下一 [UI-081](../development/task-packets/UI-081.md) 烈焰人，原幼羊和整体外观仍待。以下续接条目为各阶段历史，不把旧测试/Player自动升级为当前证据。

续接状态：UI-073已完成独立BindPoseRotation及源网格烘焙，海龟/绵羊不再共用binding与普通rotation；旧问题1描述为修复前事实。587/587与双側海龟两尺寸、北极熊/绵羊实际图通过；问题3仍待UI-074，问题4全模型图册未完成。

UI-074续接：指定geometry精确匹配/缺失拒绝/重复ID拒绝已修，606/606核验36工厂登记全部精确命中。未提供ID保留默认；派生模型明确unsupported而不猜合并。问题3选择错误已关闭，派生支持仅在新增登记需求时另验。问题4继续UI-075全登记Player图册。

UI-075续接：36登记×双側×两待机相位的144张Unity Player实图及六页全部查看。实际发现Entity Shader被构建剥离并加入Always Included引用，608/608及当前两模式684项Player通过。图册仍可见沙漠村民缺脸、末影人透明破碎、羊毛/部分部件异常；问题4的可复现图册工具完成，全部外观验收**未完成**。先UI-076村民基础皮肤/群系层，其他异常另拆。联系表只覆盖斜视面/工厂待机，不是六面UV或完整对局证据，详见 `docs/development/task-packets/UI-075.md`。

UI-076续接：已读取固定官方村民client entity、v2与实际引用的v3 render controller，确认base/biome分离。原网格UV上合成两原纹理后沙漠村民头脸恢复；611/611与当前两模式684输入来源通过，144图仅该村民4图改变，140张SHA完全不变，双侧战场图/原格射线/待机通过。没有复制开源代码或改MC原图。官方链接与当前证据见 `docs/development/task-packets/UI-076.md`；末影人仍破碎透明，下一UI-077，全部外观验收仍未完成。

## 已读取的原始项目

UI-079续接：官方蜘蛛动画的default_leg_pose未应用，且本地X镜像后的Y/Z旋转符号与Blockbench原始导入逻辑不同。独立坐标fixture复现6失败/蜘蛛2侧失败，修共用骨骼/两类cube旋转为(-X,-Y,+Z)，源八腿角度只转换一次；20个低alpha红眼登记已有发光近似。646/646，两模式684来源/74卡图/诊断隔离通过，144图123不变/21变更原尺寸全查看，当前真实手牌部署攻击中毒与None普通战场通过。此修复因此必要地覆盖其他旋转模型，不用蜘蛛局部反号掩盖通用坐标错误。没有复制GPL/MIT代码、改源资源或称通用Molang/完整原版shader，详见 [UI-079](../development/task-packets/UI-079.md)。下一UI-080守卫者，烈焰人等仍待，全模型目标未完成。

UI-078续接：原羊geometry中已有更短头/半截腿羊毛外层，原64×64 TGA的alpha为颜色遮罩；错误的整只剪毛羊膨胀/Java羊毛组合及通用透明裁剪都需修。已改显式源overlay挂同一骨骼和羊专用颜色遮罩，634/634（4项新增真实GPU读取），两模式684来源/74卡图/诊断隔离通过；当前144图仅羊8张变，其他136不变，八图和两侧分别真实战场已看。原素材未改，来源/初轮白脸失败/当前hash见 [UI-078](../development/task-packets/UI-078.md)。tk_003仍既有缩小成人羊，不冒称完整官方baby管线。下一UI-079蜘蛛，守卫者/烈焰人另待，全外观目标不关闭。

UI-077续接：原TGA六个alpha=3/255紫眼被通用cutoff剔除，其他黑块部分为不透明原像素；专用低alpha发光近似和明确静态站姿适配已验证。621/621含4项真实GPU读取，两模式684项Player/74卡图/诊断隔离通过，144图仅末影人4张变化，140张不变；末影人四图与None普通战场已看。官方client/render controller/base_pose链接、实现边界及hash见 [UI-077](../development/task-packets/UI-077.md)。没有完整执行Molang或照抄未读取的官方fragment shader，未复制开源代码/改原资产。羊毛及其他模型继续独立UI-078，全部外观验收仍未完成。

- [PrismarineJS实体渲染器](https://github.com/PrismarineJS/prismarine-viewer/blob/7fa43a7317467a3ba84f857ba6b1ca9597c72b8c/viewer/lib/entity/Entity.js)，参考commit `7fa43a7317467a3ba84f857ba6b1ca9597c72b8c`。[MIT许可](https://github.com/PrismarineJS/prismarine-viewer/blob/7fa43a7317467a3ba84f857ba6b1ca9597c72b8c/LICENSE)。其box-face UV按texWidth/texHeight归一化、Nearest采样，网格构造处理骨骼姿态，然后组装骨骼父子与相对pivot、建立SkinnedMesh绑定。不能把Three.js负号/欧拉顺序直接抄到Unity；Unity当前X镜像需要独立等价测试。若后续移植代码须保留上游版权/MIT声明；当前仅阅读、未复制代码或安装依赖。
- [Blockbench Bedrock工具链](https://github.com/JannisX11/blockbench/tree/e2ede0809ee6bc91f374ac7e00d34cffbdf86a14/js/formats/bedrock)，参考commit `e2ede0809ee6bc91f374ac7e00d34cffbdf86a14`。[GPLv3许可](https://github.com/JannisX11/blockbench/blob/e2ede0809ee6bc91f374ac7e00d34cffbdf86a14/LICENSE.MD)。已读multi-file/default-pose代码，但它不是所有旧格式绑定语义的充分依据；可作为离线导入可视对照工具，本轮不复制其代码、不新添运行时依赖。

## 本地源码已经证实的差异

1. Parser把普通rotation和bind_pose_rotation合并成一个Rotation；Builder将其作用于Transform并被子骨骼继承。羊专项现用网格绑定覆盖解决，但turtle.json仍含body/eggbelly绑定姿态，应先建带父子pivot的独立参考fixture，再分离绑定/动画姿态，覆盖真实注册模型。
2. 羊毛atlas和基础atlas不同尺寸，UI-072已修；需继续核查所有模型的实际贴图尺寸与geometry metadata，不默认全部64x64。
3. Legacy Parse无视preferredIdentifier并跳过带冒号的派生geometry；现代Parse找不到指定ID会回退第一项。不能静默装配错误模型。确认目前注册表实际用到哪些条目后，补严格选择/派生合并及负例。
4. 几何用例“有mesh/包围盒正确”不证明皮肤/脸朝向/透明区域正确。后续以统一相机、两侧朝向、真实材质的模型图册检查；明确每个来源模型/纹理/注册卡的映射，保持原素材来源hash。

## 下一切片的完成门禁

先将绑定姿态与骨骼运动在中间表示中分开，以羊/海龟/北极熊父子层级为回归，再检查六面UV、镜像、旋转组合与atlas。每次改通用规则后调用Unity全量EditMode和当前Player真实渲染，不能仅新添模型特例或用占位块替代。后续全模型图册另为一个独立切片，不在当前羊专项宣称全部通过。

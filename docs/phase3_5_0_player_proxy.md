# Phase 3.5.0：玩家代理实体设计

日期：2026-10-03。工程：MC7DTD-Bridge，`D:\wenjian\minecraft\7-M`。

## 1. 范围与实际状态

player proxy 是 Minecraft 已有玩家在七日杀中的被动表示。Minecraft 是身份、位置和生命周期的数据来源；七日杀代理只消费这些状态，不接管 Minecraft 玩家，也不替代七日杀本地玩家。

本阶段仅扩展配置、定义能力和映射规则、提供 JSON 示例及离线验证。**不生成玩家实体，不实现适配器、AI、输入控制或动画同步**，也不新增 Minecraft 玩家实体采集器或发送入口。

两个游戏 Mod、Bridge 生产源码、EntityRegistry、CoordinateMapper、entity_state 协议及 network.json/coordinate.json 均不修改。类型目录仍未接入运行时；现有 marker 和 player_position 行为不变。

## 2. 类型定义与能力

`config/entity_types.json` 新增一条禁用定义，原 marker 定义保留：

```text
源类型：minecraft:player
目标游戏：7dtd
目标别名：7dtd:player_proxy
适配器：player_proxy（尚未注册或实现）
enabled：false
```

沿用现有 Schema 和配置版本 1，无新增字段。`7dtd:player_proxy` 是框架设计中的本地代理别名，不是已经验证的七日杀原生玩家类或 XML 实体名。未来实际渲染对象、模型和原生 API 需另行选型与验收，本阶段不宣称已经选定可用实现。

| capability | 配置声明 | 设计含义 |
| --- | --- | --- |
| spawn | true | 未来可开始表示源玩家 |
| update_position | true | 未来可更新脚底中心位置 |
| despawn | true | 未来可清理代理及其资源 |
| update_rotation | false | 仅保留协议必填 rotation，不应用朝向 |
| health | false | 不驱动代理血量，不映射伤害 |
| collision | false | 不参与碰撞、阻挡或交互 |
| ai | false | 不寻路、不自主移动、不作 NPC 决策 |
| combat | false | 不攻击、不受击、不结算战斗 |
| persistence | false | 不保存或跨会话恢复代理对象 |

这里的 true 是**禁用预留定义的拟定能力**，不是已实现能力。enabled=false 时没有任何能力被授予。将其手动改为 true，现有离线校验器会拒绝未注册的 player_proxy 适配器；不能通过配置绕过实现与验收。把它改为 marker 适配器同样被拒绝，玩家类型不回退为 marker。

输入控制和动画同步不在当前能力词汇中，不新增字段或消息来表示这些功能。metadata 中的文本也不会成为指令。

## 3. 身份与一对一映射规则

拟定映射为 `(source=minecraft, entity_type=minecraft:player, target_game=7dtd)` 精确查询目录，内部解析到 player_proxy；未知、禁用或未实现类型只诊断，不创建替代对象。

每个源玩家的一次生命期对应一个桥接 `entity_id`，同生命期的 spawn/update/despawn 保持一致。使用小写 UUID，不以坐标、显示名称或七日杀本地玩家编号推断身份；示例 UUID 是虚构的桥接身份，不是账号标识。

代理实例的逻辑键沿用 `(source, world_id, dimension, entity_id)`。`stream_id` 标识发送轮次，不代替逻辑键。未来由源端维护原生玩家与桥接身份的进程内对应表；不在消息中暴露 Steam ID、登录账号或原生身份表。

七日杀本地对象句柄仅由未来接收端持有，不写回源 `entity_id`。目标别名不替换线上 `entity_type`，EntityRegistry 仍记录 `minecraft:player`。在已注册生命期内不得修改类型；玩家死亡后重生使用新桥接身份，离开世界或身份连续性无法证明时清理旧表示并重新建立。

世界和维度字段描述源上下文，并不是目标存档路径。未来代理只能落在经验证的当前七日杀测试世界；不能根据 world_id 自动加载世界。本阶段未新增世界配对配置或实现跨存档路由。跨维度采用旧键 despawn(reason=dimension_changed) 后新维度 spawn，避免旧世界表示残留，不增加 Registry 的状态种类。

## 4. 位置、姿态与数据权威

参考点为玩家脚底中心。Minecraft 发原始 double 坐标，入站 `position.space=minecraft`；Bridge 沿用 CoordinateMapper，输出 `space=7dtd`：

```text
x_target = x_source * scale + offsetX
y_target = y_source * scale + offsetY
z_target = z_source * scale + offsetZ
```

实体传输沿用现有 scale>0 校验。接收端不得再次缩放或偏移；未来若模型原点不是脚底中心，其局部显示偏移由适配器处理，不修改线上坐标。坐标比例不自动定义模型尺寸、玩家碰撞体或速度。本阶段不定义插值、预测、动画或朝向应用。

rotation 的 yaw/pitch/roll 继续按原协议完整提供，Bridge 原样转发；update_rotation=false 不表示可以省略该必填对象。代理未来忽略姿态应用，但不修改消息。

metadata 为完整扁平快照。示例仅用可选诊断展示名 ProxyDemo 和 native_type；不包含血量、输入、动画、账号数据。显示名称不是身份依据，也不是路径或可执行内容。despawn 必须 position=null、rotation=null、metadata={}。

## 5. 生命周期与旧通道兼容

| 事件/条件 | 源端设计 | 沿用的处理或未来代理职责 |
| --- | --- | --- |
| spawn | 完整状态，entity_type=minecraft:player | Registry 建立记录；未来适配器才可能建立代理 |
| 重复 spawn | 同键保持身份 | 沿用防重复处理，不产生第二个代理 |
| update | 完整状态、同键同类型同 stream | 更新已有记录；未来主线程更新表示位置 |
| 未知 update | 不应凭位置推断玩家 | 沿用 entity_not_found，不创建对象 |
| despawn | 合法 reason、无位置和姿态、空 metadata | 删除记录；未来主线程清理对象 |
| 断线/世界退出 | 不解释为玩家死亡 | 沿用现有失效清理，未来代理同步释放 |
| Bridge 重启 | 内存记录为空 | 等待重连后的完整 spawn，不靠 update 恢复 |

sequence 使用协议已有字段，示例为 1、2、3；不增加序号字段、排序状态机或墓碑。本阶段不修改既有 Registry 对 sequence 的处理。未来由源端按已有连接约定更新 stream 并重新发完整 spawn。

`player_position` 继续独立发送和记录。它没有玩家 ID、世界/维度和生命周期，不能自动转为 entity_state 或驱动同一代理两次。当前 Minecraft Mod 仍只保持既有玩家坐标通道及手动测试功能；player proxy 自动采集不在本轮实现范围。

## 6. 示例 JSON

可直接解析的独立消息文件：

- `docs/examples/player_proxy/spawn.json`：源位置 (10,64,20)，sequence=1。
- `docs/examples/player_proxy/update.json`：移动至 (11,64,21)，sequence=2。
- `docs/examples/player_proxy/despawn.json`：world_unloaded，sequence=3。

三条消息均使用 `type=entity_state`、`version=1` 和已有字段，不添加 proxy_id、target_type、capabilities 或输入字段。它们仅是静态设计示例，不自动发送到游戏。

离线预期文件 `docs/examples/player_proxy/mapping_expectations.json` 包含测试用 coordinate 和三条 forwarded 输出；**该外层对象和数组是测试夹具，不是新的线上协议格式**。夹具采用 scale=2、offsets=(100,-10,25)，不修改实际 coordinate.json：

| 事件 | 原始位置 | 转发位置 |
| --- | --- | --- |
| spawn | (10,64,20)，minecraft | (120,118,65)，7dtd |
| update | (11,64,21)，minecraft | (122,118,67)，7dtd |
| despawn | null | null |

输出继续保留 entity_type=minecraft:player、身份、rotation、metadata。7dtd:player_proxy 只用于未来接收端本地解析，不能由 Minecraft 作为源类型发送。

## 7. 自动验证与结果

在工程根目录运行：

```powershell
.\tests\run-phase3_5_0.ps1
```

需要 Python 3.11+ 和工程现用 .NET SDK。复用 Phase 3.4 的工程内 Schema 依赖；缺失时按已有固定版本 requirements 安装到 work 内。所有日志、依赖和构建输出均在工程根目录内。

验证只读配置和示例，不启动 Bridge 服务、不连接游戏、不调用实体生成 API：

- 配置：复用原有 5 组校验，增加 4 组玩家代理检查，包括默认禁用、精确映射、拟定能力、未实现适配器拒绝、禁止 marker 回退及严格 JSON。
- 消息：新离线 .NET 测试程序引用原有 Bridge，通过实际 EntityTransport、CoordinateMapper、EntityRegistry 校验示例。检查默认/非默认转换、身份及元数据不变、spawn/update/despawn 记录变化、重置清理、拒绝目标类型冒充源类型、重复映射空间、额外 capabilities 字段和非空 despawn metadata。

实际结果：**9 个配置测试组通过；29 项协议示例及映射/生命周期检查通过；0 失败。** 新离线测试程序及其 Bridge 引用构建运行成功。没有部署或编译修改后的游戏 Mod，因为本轮没有修改它们。

证据：`work/phase3_5_0-test/catalog-tests.log`、`catalog-results.json`、`example-tests.log`、`example-results.json`。

## 8. 文件变更与停止点

修改：`config/entity_types.json`、`README.md`。

新增：本设计文档；示例目录内 spawn.json、update.json、despawn.json、mapping_expectations.json；`tests/entity-types/test_player_proxy.py`；`tests/player-proxy-design-runner/PlayerProxyDesignRunner.csproj`、Program.cs；`tests/run-phase3_5_0.ps1`。

限制：类型目录未接入运行时，player_proxy 适配器尚不存在，玩家实体示例并不表示实际游戏已发送或显示玩家代理。未来实现之前仍需确认原生对象选型、主线程创建/清理和目标世界边界，再分别验收；不把本轮离线结果当作实机验收。

Phase 3.5.0 设计完成后停止，等待确认；不进入玩家实体生成、AI、输入控制或动画同步。

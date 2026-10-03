# Phase 3.0：跨游戏实体状态协议设计

日期：2026-10-03（Asia/Shanghai）
工程：MC7DTD-Bridge，D:\wenjian\minecraft\7-M。
协议名称：entity_state，版本：1。本文主体为 Phase 3.0 设计基线；Phase 3.1 已实现的传输子集和事件名称以文末附录为准，完整生命周期应用尚未实现。

## 1. 范围与现有协议兼容

本协议描述源游戏已有实体的状态，供后续跨游戏接收和处理使用。本阶段不生成实体，不修改两个游戏 Mod、Bridge、配置或连接逻辑；不增加运行中的消息类型支持。

沿用本地 JSON/WebSocket 通道 ws://localhost:18771/ws、现有角色握手、welcome 和 test 消息。entity_state 使用独立的 type 和 version，不把现有玩家消息改成实体消息。

现有 player_position 继续保持五个字段和 Phase 2.1 转换方式：

```json
{
  "type": "player_position",
  "source": "minecraft",
  "x": 100,
  "y": 64,
  "z": 200
}
```

Phase 3.0 时 Bridge 只支持握手后的 test 和 player_position。当时 entity_state 会被拒绝；Phase 3.1 的支持范围见附录。未经部署新版七日杀 Mod，不应把正在运行的旧实例当作新版实体日志接收端。

entity_state 中的玩家实体与 player_position 暂不自动关联：旧消息没有玩家 ID，不能凭坐标相同认定是同一个实体。后续接收端应指定唯一的玩家状态消费者，避免两条消息驱动同一对象两次。

## 2. 消息结构

每条消息是一个 UTF-8 JSON 对象，描述一个实体；不在本版本加入批量数组。必填字段如下：

| 字段 | 类型 | 语义 |
| --- | --- | --- |
| type | string | 固定 entity_state |
| version | integer | 固定 1，仅为新消息版本，不改变旧消息 |
| source | string | minecraft 或 7dtd；Bridge 必须与连接的握手角色核对 |
| stream_id | string | UUID；本次发送轮次，处理断线重建和旧状态隔离 |
| entity_id | string | UUID；源端为一次实体生命期分配的桥接身份 |
| entity_type | string | 命名空间类型名，如 minecraft:player、7dtd:zombie |
| world_id | string | 源端世界/存档的稳定、不含路径的标识 |
| dimension | string | 源端维度/场景标识，如 minecraft:overworld、7dtd:main |
| sequence | integer | 当前 stream 内该实体递增序号，范围 1..9007199254740991 |
| lifecycle | object | event，及移除时的 reason |
| position | object 或 null | x/y/z 和 space；announce/update 必须是对象，remove 必须为 null |
| rotation | object 或 null | yaw/pitch/roll；announce/update 必须是对象，remove 必须为 null |
| metadata | object | 扩展状态，允许空对象；remove 必须为 {} |

source 表示数据所有者；position.space 表示当前坐标所属空间，两者不能混用。除上述字段和 lifecycle.reason 外，不允许新增顶层或嵌套结构字段；扩展内容放在 metadata 中。

## 3. Entity ID 与 Entity Type

实体的逻辑键为 (source, world_id, dimension, entity_id)。entity_id 使用标准小写带连字符 UUID，由源 Mod 维护，可使用原生 UUID 的稳定对应或自行分配 UUID。它不是目标游戏实体编号，也不携带用户名、Steam ID、账号 ID 或文件路径。

同一实体在同一次生命期中的 announce/update/remove 必须保持逻辑键不变。离开同步范围后又回来，仍是同一原生实体时可复用 entity_id；真正死亡后重生、原生数字 ID 被复用或无法证明身份连续时必须使用新 entity_id。不能仅凭原生数字 ID、类型或坐标复用桥接身份。进程重启且无法恢复身份对应表时分配新身份，旧轮次状态由 stream 规则清理。

跨维度移动以旧维度 remove(reason=dimension_changed) 和新维度 announce 表示；即使保留 entity_id，逻辑键也已改变。同一逻辑键的 entity_type 在有效生命期内不变，变形或类型替换用 remove 后的新 entity_id 表示。

entity_type 必须符合 `[a-z0-9_.-]+:[a-z0-9_./-]+`，最大128字符，命名空间与 source 相符。7dtd:zombie 等是本协议的类型别名示例，不宣称是已验证的七日杀原生注册名；原生类型对应表待后续阶段定义。

未知但格式合法的类型可记录并标为 unsupported，不自动替换成另一种实体。类型信息仅作识别，不是生成命令。

world_id 长度1..128，使用不含个人信息的 ASCII 字母、数字、下划线、短横线或点；dimension 长度1..128，使用与 entity_type 相同的命名格式。两个游戏的 world_id 不必相同，本版本不定义世界之间的对应表。

## 4. Position 与坐标转换

position.x/y/z 是有限 JSON 数字，不接受字符串、NaN 或 Infinity。保留双精度，不在协议层取整。参考点统一为实体脚底中心；没有脚部的对象使用其下方边界中心，原生坐标参考点的适配由源 Mod 负责，不能直接假定两个游戏的实体原点一致。

发送源坐标时 position.space 必须等于 source：minecraft 或7dtd。Bridge 只能接受该原始空间，防止把已经映射的坐标再次映射。转发时保留 source 和身份，转换 position.x/y/z，并将 space 改为接收游戏名。

Minecraft → 七日杀复用既有公式：

```text
x_7dtd = x_minecraft * scale + offsetX
y_7dtd = y_minecraft * scale + offsetY
z_7dtd = z_minecraft * scale + offsetZ
```

为保持实体大小/姿态语义，本版本未来启用 entity_state 时要求 scale>0，且转换结果有限。这个要求只针对新实体协议，不改变现有 CoordinateMapper/player_position 允许零或负比例的行为；不符合时拒绝实体消息，旧位置同步仍按现有规则工作。

七日杀 → Minecraft 的拟定位置规则为对应逆变换 `(coordinate-offset)/scale`，仅在两个方向使用同一组映射参数且 scale>0 时成立。本阶段没有实现逆映射；不得宣称当前 Bridge 支持七日杀位置反向发送。

接收端只消费 space 等于自身游戏的状态；不在接收 Mod 再次执行比例与偏移。世界对应、坐标轴旋转、地形匹配和地图边界不在本版本范围内。

## 5. Rotation

rotation 是实体本体朝向，不是玩家摄像机或头部朝向。单位为度，三个字段均须提供有限数值：

| 字段 | 范围 | 协议约定 |
| --- | --- | --- |
| yaw | [0,360) | 水平朝向；0沿+Z、90沿-X、180沿-Z、270沿+X |
| pitch | [-90,90] | 0水平、正值向下、负值向上 |
| roll | [-180,180) | 绕本体朝前轴的旋转；正值按右手方向 |

朝向向量由 yaw/pitch 定义为 `(-sin(yaw)*cos(pitch), -sin(pitch), cos(yaw)*cos(pitch))`，计算时角度先转弧度；roll 再绕该朝前轴作用于本体局部基向量。只支持直立实体的适配器发送 roll=0，不得凭空估算倾斜。

以上是协议统一约定，不是对两个游戏原生角度约定的事实断言。各游戏适配器须转换原生角度后发送，并在后续实现时验证轴向。Bridge 的正比例和平移不改变 yaw/pitch/roll。协议不约定插值算法、不自动修正看向目标，也不添加摄像机同步。

## 6. Lifecycle、顺序与重连

lifecycle.event 仅允许 announce、update、remove。这里的 announce 表示开始观察源端实体，不是命令接收游戏生成实体；remove 表示结束观察，也不是击杀或删除目标游戏实体的命令。

| 事件 | 所需状态 | 含义 |
| --- | --- | --- |
| announce | 完整 position、rotation、metadata；序号严格递增 | 开始观察或重新发送完整状态；可对已有活动实体幂等刷新 |
| update | 完整 position、rotation、metadata；已存在活动逻辑键 | 替换最新状态，不采用局部补丁 |
| remove | position=null、rotation=null、metadata={}；必有 reason | 停止观察，保留序号墓碑至轮次结束 |

remove.reason 仅允许 died、despawned、out_of_scope、world_unloaded、dimension_changed。died 表示源游戏已观察到死亡事件，不要求接收游戏结算伤害或奖励。无 remove.reason 的 announce/update 不携带 reason 字段。

接收端按逻辑键与 stream_id 保存最后 sequence；较小或相同序号忽略，不能覆盖新状态。sequence 是排序依据，不使用不同进程的墙上时钟排序。同一 stream 内不同实体的序号互不影响。首次 announce 序号为1；update 必须晚于 announce。remove 后，update 不可重新激活实体；重新进入范围必须发送更大序号的 announce。重复 remove 可幂等结束观察，未知逻辑键的 remove 也可留下墓碑，不能因此创建实体。

stream_id 是源端生成的发送轮次 UUID。每次建立新的 WebSocket 会话、对端重新上线或 Bridge 重启后，源端应开始新 stream，重置序号，并对当前观察范围逐个发送完整 announce。源端每个角色同时只允许一个活动 stream；首次消息必须是 announce。新轮次到来时，接收端将该 source 的上一轮状态全部标为 stale；后续轮次不能退回任何已经结束的 stream。连接内使用有序串行发送，不能混入上轮积压消息。

连接丢失、对端不在线或服务重启不等于实体死亡。接收端按连接会话失效事件把有关状态标为 stale，而不是伪造 remove(reason=died)。Bridge 未来须保留会话/轮次对应，拒绝旧轮次复活；现有 welcome/peer_connected 字段不变。对端下线的具体通知或接收端到期机制尚未实现，属于后续生命周期实现的必要前置条件；没有明确会话失效依据时不得认定源实体已经消失。

本版本不承诺可靠投递、离线队列、历史补发、完成全量快照的标记或实体 ACK。announce 可定期作为完整刷新，帮助状态重建；具体频率和资源上限在实现阶段确定。无法获得身份连续性的接收端应等待新 announce，而不能从 update 猜测实体。

## 7. Metadata

metadata 必填但可为空，为完整快照；某键在下一条完整状态中缺失表示该属性未知/不再提供，不能保留过期值。只允许最多32个键的扁平对象，值为 string、有限 number、boolean 或 null；不允许对象/数组。键长度1..64，符合 `[a-zA-Z][a-zA-Z0-9_.-]*`；字符串值最长256字符。

预留可选键：

| 键 | 类型 | 语义 |
| --- | --- | --- |
| health | number ≥0 | 源游戏生命值，不等于目标游戏伤害单位 |
| max_health | number >0 | 如与 health 同时提供，health 必须≤max_health |
| display_name | string | 可展示名称；可省略，不传账号标识 |
| is_alive | boolean | 最近观察到的存活状态，不替代 lifecycle |
| native_type | string | 无个人信息的原生类型名，供诊断使用 |

未知且满足类型/长度规则的键可忽略。metadata 不能携带 executable 指令、对象代码或资源加载路径，任何接收端都不执行其文本。扩展属性不表示战斗、物品或AI控制功能已经启用。

## 8. 完整消息示例

源端开始观察一个 Minecraft 玩家（仅为协议示例，不发送到现有 Bridge）：

```json
{
  "type": "entity_state",
  "version": 1,
  "source": "minecraft",
  "stream_id": "55c44b7e-82c8-461d-8c2b-d4e4e4e77fd1",
  "entity_id": "6b6d9c36-909c-4fbb-a537-d309f48f8231",
  "entity_type": "minecraft:player",
  "world_id": "demo-world-01",
  "dimension": "minecraft:overworld",
  "sequence": 1,
  "lifecycle": { "event": "announce" },
  "position": { "x": 10, "y": 64, "z": 20, "space": "minecraft" },
  "rotation": { "yaw": 90, "pitch": 0, "roll": 0 },
  "metadata": { "health": 20, "max_health": 20, "is_alive": true }
}
```

采用 scale=2、offsets=(100,-10,25)，源玩家移动至(11,64,21)后，Bridge 拟转发的完整 update：

```json
{
  "type": "entity_state",
  "version": 1,
  "source": "minecraft",
  "stream_id": "55c44b7e-82c8-461d-8c2b-d4e4e4e77fd1",
  "entity_id": "6b6d9c36-909c-4fbb-a537-d309f48f8231",
  "entity_type": "minecraft:player",
  "world_id": "demo-world-01",
  "dimension": "minecraft:overworld",
  "sequence": 2,
  "lifecycle": { "event": "update" },
  "position": { "x": 122, "y": 118, "z": 67, "space": "7dtd" },
  "rotation": { "yaw": 90, "pitch": 0, "roll": 0 },
  "metadata": { "health": 20, "max_health": 20, "is_alive": true }
}
```

源端退出世界时的 remove（无需坐标转换）：

```json
{
  "type": "entity_state",
  "version": 1,
  "source": "minecraft",
  "stream_id": "55c44b7e-82c8-461d-8c2b-d4e4e4e77fd1",
  "entity_id": "6b6d9c36-909c-4fbb-a537-d309f48f8231",
  "entity_type": "minecraft:player",
  "world_id": "demo-world-01",
  "dimension": "minecraft:overworld",
  "sequence": 3,
  "lifecycle": { "event": "remove", "reason": "world_unloaded" },
  "position": null,
  "rotation": null,
  "metadata": {}
}
```

update 示例以前面 announce 已经转换并送达接收端为前提；它展示转发格式，不是允许源 Minecraft 直接发送 space=7dtd。反方向使用 source=7dtd 和对应源类型/世界身份，入站 space=7dtd、出站 space=minecraft，生命周期规则相同。

## 9. 后续实现的校验与验收要求

沿用文本 JSON、单消息≤8192 UTF-8 字节、深度≤8；限制针对整个消息，包括 metadata 和字符串的实际编码。拒绝重复键、错误字段类型、额外结构字段、未知 version/event、身份不符、非法数值、越界旋转和转换溢出。单个字符串长度不能替代总消息大小限制。

拟定结构校验错误通过现有 error/code 响应形状报告并按现有方式结束违规会话；新 code 的最终枚举在实现阶段确定，本阶段不添加运行时错误代码。有效格式但未支持的 entity_type 可忽略并记录；乱序/重复 sequence 幂等忽略，未知实体 update 不应用并等待 announce。

后续自动验收应覆盖：

1. announce → update → remove，全部字段完整、无实体生成副作用。
2. 来源和逻辑键校验、原生ID复用、死亡后新身份、跨维度身份变化。
3. 默认/非默认映射、space 标记、防止重复映射、零或负比例限制及溢出。
4. yaw基准方向与pitch范围，各游戏原生角度/脚底参考点的适配实测。
5. 重复序号、旧 stream、未知 update、remove墓碑及重新进入观察范围。
6. 重连后新 stream 与完整 announce、断线仅标 stale，不当成死亡。
7. metadata 大小、完整替换、未知类型与字段、分片 JSON、总字节限制。
8. 现有 test 与 player_position 的格式、映射、频率和重连回归。

本阶段仅核验文档的 JSON 示例可解析、字段一致及映射示例计算；不启动新的协议测试连接、不重新编译、不重启游戏。后续设计仍需确定观察范围、速率、状态表容量、原生类型对应及会话失效通知，再进入实现。

## 10. 交付与停止点

仅新增 docs/entity_sync_protocol.md；没有修改其他工程文件。无需编译或新增运行命令；现有运行方式保持不变。

Phase 3.0 协议设计完成，到此停止，等待确认。本文未授权实体生成、实体同步实现、战斗或方块功能。

## 附录：Phase 3.1 传输子集（2026-10-03）

用户本轮指定事件名称 spawn/update/despawn。实现继续使用 lifecycle.event，优先使用这三个名称；Bridge 同时接受设计基线中的 announce/remove，分别规范化成 spawn/despawn。version 仍为1，两个旧名称作为同版本输入别名保留；输出只使用新名称。spawn 仅表示传输源状态，不表示调用实体生成 API。

Bridge 已实现 Minecraft 角色 → 七日杀角色的 entity_state 字段校验、正比例坐标映射、转发及日志。source=minecraft，入站 position.space=minecraft，出站 space=7dtd。身份、旋转和 metadata 原样保留，生命周期别名除外；despawn 无坐标。七日杀标准接收类只打印身份、事件及坐标/朝向/原因，忽略 metadata 的应用。

不支持七日杀 → Minecraft 实体消息，来自七日杀连接的实体消息返回 invalid_entity_source；Minecraft Mod 未增加实体采集或接收。后续双向实现仍需逆映射及 Minecraft 接收端。本轮默认/非默认配置的正向转换与现有 player_position 共用 CoordinateMapper，现有消息不变。

本轮不保存实体注册表、序号墓碑或活动 stream，不应用生命周期状态机，不识别重复/旧轮次，不插值、不生成或删除对象。sequence/stream_id 目前只做格式和范围校验；即使 update 在 spawn 前到达，也仅作为合法传输消息记录。主体中的顺序、重连清理、原生实体关联和去重规则继续作为后续应用层要求，不宣称已实现。Bridge 离线对端不缓存实体消息；错误策略沿用现有协议。

实现、自动测试、编译启动和限制见 [Phase 3.1 传输报告](phase3_1_entity_transport.md)。

# Phase 3.6.0：双向实体同步协议设计

日期：2026-10-03。工程根目录：`D:\wenjian\minecraft\7-M`。

## 1. 草案版本与当前实现

本阶段定义 **entity_state version=2**。source 保留为必填字段，新增必填 authority 和 origin。采用新版本是因为当前 v1 的 EntityTransport 严格拒绝额外字段，不能在 v1 下直接追加字段并宣称兼容。

本阶段只交付文档、JSON 示例、Schema 和离线验证。**两个 Mod 和 Bridge 生产逻辑完全不改，实际游戏仍使用 v1。** 当前 Bridge 不支持七日杀来源的实体消息，也不支持 v2；示例禁止发送到现有运行服务。

| 项目 | 当前运行实现 | v2 设计要求 |
| --- | --- | --- |
| 实体方向 | Minecraft → 7DTD | 两端各自导出本端原生实体 |
| authority / origin | 无显式字段 | 显式所有者和不可变来源身份 |
| 逆向坐标 | 未实现 | 7DTD → Minecraft 使用逆公式 |
| sequence | Registry 保存字段，未严格拒绝所有旧序号 | 同活动 stream 按既有 sequence 比较 |
| despawn 后状态 | 当前删除记录 | v2 草案保留轮次内墓碑 |
| 类型路由 | 已实现 marker/player 目标白名单 | 未来接收端按类型目录映射，未知类型不生成 |

以上 v2 规则不是对当前 EntityRegistry 的行为描述。测试中的 OwnershipOracle 是隔离的可执行设计参考，不是替换 Registry 的实现。

未来接入必须显式启用完整 v2 支持并验证双方能力，不能试发 v2 到旧实例或静默降级丢失所有权。v1 保留独立处理路径及现有 player_position/test/握手；本阶段不修改 welcome 或增加版本协商消息。同一源实体同一观察轮次只能选择一个版本通道，不能把 v1、v2 同时当成两个原生实体。未知 version 拒绝。

## 2. source、authority、origin

| 字段 | 类型 | 含义 |
| --- | --- | --- |
| source | minecraft 或 7dtd | 当前实体状态的游戏发布者；必须匹配入站连接角色 |
| authority | minecraft 或 7dtd | 可以修改该实体状态和生命周期的唯一游戏所有者 |
| origin | object | 原始创建游戏和源身份上下文，整个观察生命期不可变 |

origin 必含且只含四个字段：

```json
{
  "game": "minecraft",
  "world_id": "mc-demo",
  "dimension": "minecraft:overworld",
  "entity_id": "53779af5-0e40-49c3-99bb-445ac3df7ca1"
}
```

origin.game 是实体最初属于的游戏，不是代理所在游戏；entity_id 为桥接 UUID，不是目标对象编号、账号或 Steam ID。world_id/dimension 是创建该观察身份时的源上下文，不是目标存档路径。

v2 基础规则为 **source = authority = origin.game**。三者分开表达“发布者、控制者、来源”，但本草案不允许代理代发布、第三方代写或 authority 转移。因此：

- Minecraft 创建：authority=minecraft，origin.game=minecraft；只有 Minecraft 连接能发布状态。
- 7DTD 创建：authority=7dtd，origin.game=7dtd；只有 7DTD 连接能发布状态。
- Bridge 是验证和转发中介，不拥有实体，不把 source 改成 bridge 或目标游戏。

根字段 entity_id/world_id/dimension 必须分别等于 origin 中对应值。保留根字段便于继承 v1 的状态结构；不允许两份身份含义不同。entity_type 和 dimension 的命名空间必须等于 origin.game；目标映射别名不能替换源 entity_type。

## 3. 所有权、身份和代理边界

设计逻辑键为 `(origin.game, origin.world_id, origin.dimension, origin.entity_id)`。authority、entity_type 在此逻辑键的活动生命期内不可变。不能凭位置、显示名或 entity_id 单独合并实体；两款游戏恰好使用相同 UUID，仍是两个不同来源实体。

源 Mod 的未来导出器必须维护原生所有权表，只导出自己原生拥有的实体。目标创建代理时保存完整 origin 和“镜像”标记，不把代理加入原生导出表，不靠对象名称猜测是否是镜像。

双向数据流设计：

```text
Minecraft 原生实体 [authority=minecraft]
  → Bridge 验证与正向映射 → 7DTD 本地代理（不回传）

7DTD 原生实体 [authority=7dtd]
  → Bridge 验证与逆向映射 → Minecraft 本地代理（不回传）
```

保留 origin 的代理消息若从另一游戏连接反向进入，会因 source/authority/session 不匹配被拒绝。代理不得把 authority、origin、entity_type 改写成目标游戏来伪装原生实体；是否原生由源端所有权表判定，单条 JSON 自报字段不能代替这张表。

目标物理偏移、碰撞或本地显示更新不改变源权威状态。目标关闭世界可以删除本地代理，不向所有者发送假的 despawn/died。将来若需要交互，应设计独立请求与授权流程；本阶段不定义输入、战斗或 AI 消息。

跨维度用旧 origin 的 despawn(reason=dimension_changed) 和新 origin 的 spawn 表示，不直接 update 改写 origin。源死亡/真实销毁后重生采用新桥接 UUID。v2 基础版中同一 stream 的退休身份不可重新 spawn，重新观察使用新 UUID；这比历史 v1 设计的同身份重新进入规则更严格，仅是 v2 草案规则。来源重连且原生身份连续可在新轮次完整 spawn，但不能补发旧轮次积压事件。

## 4. update 与 despawn 的许可

每条消息保留 v1 的 type、version、source、stream_id、entity_id、entity_type、world_id、dimension、sequence、lifecycle、position、rotation、metadata；仅新增 authority 和 origin，并将 version 改为 2。

事件只允许 spawn/update/despawn。v1 的 announce/remove 输入别名不在 v2 中使用。

- spawn：唯一所有者发送完整状态；建立源记录，目标是否能创建代理取决于世界就绪、已实现类型和能力。
- update：同来源、身份、类型、authority 和活动 stream，完整替换状态；不得创建未知实体。
- despawn：同一所有者可以结束观察；必须 position=null、rotation=null、metadata={}，reason 沿用 died/despawned/out_of_scope/world_unloaded/dimension_changed。

桥接接受消息与目标对象真正生成仍不是事务；没有新增实体 ACK、离线补发或可靠投递承诺。未知但格式合法的类型可以保留源记录并诊断“不支持目标生成”，不能替代为其他对象。

## 5. 冲突处理表

先做结构、连接角色、authority、origin、活动 stream 校验，再做同逻辑键的生命周期判定；拒绝或忽略均不触及原生游戏状态。

| 冲突 | v2 处理 |
| --- | --- |
| source 不等于握手角色 | 拒绝，不接受自报来源 |
| 非所有者 update/despawn | 拒绝，不改状态、不转发 |
| authority 与 origin.game 不一致，或尝试转移 | 拒绝；不做抢占或最后写入者获胜 |
| 根身份与 origin 不一致 | 拒绝，不把它理解为身份迁移 |
| 活动身份更换 entity_type | 拒绝，必须退休旧身份并采用新 UUID |
| 活动 stream 与受控会话上下文不符 | 拒绝旧轮次；UUID 或墙上时间不能自行取得权威 |
| 相同 stream、sequence 小于或等于最后已应用序号 | 幂等忽略，不覆盖新状态 |
| 更大 sequence 的重复 spawn，已有活动记录 | 忽略，不创建第二个对象、不覆盖状态，也不提高已应用序号 |
| 更大 sequence 的合法 update | 应用完整状态，仅单一所有者可写 |
| 未知/已退休身份 update | 拒绝，不从 update 推测 spawn |
| 未知身份 despawn | 记录不活动墓碑，不能创建对象 |
| 已退休身份重新 spawn（同轮次） | 拒绝，使用新身份；墓碑阻止旧 spawn 复活 |
| 两游戏 UUID 相同、位置重叠 | 不冲突，按完整 origin 键隔离 |
| 代理反向回传 | 拒绝；原生导出表必须过滤镜像 |

sequence 沿用已有整数字段，范围 1..9007199254740991；不新增时间戳或另一个序号字段。以最后**实际应用**事件为高水位，不以到达时间、目标帧数或系统时钟排序。出现序号缺口时接受较新的完整 update，不等待补齐，不承诺补包。

墓碑至少保留至该 source 的受控 stream 结束。未来实现的容量预算应覆盖活动记录与墓碑，达到上限拒绝新增记录，不静默驱逐墓碑导致旧身份复活；具体容量和断线负载验收留到实现阶段，本阶段参考模型没有实现运行时容量管理。

冲突结果是语义策略，测试里的字符串标签仅用于离线断言；不新增线上 error/code 枚举或消息。

## 6. 会话与重连

expected_stream 属于受控会话上下文，不从任意 update 自动更新。未来实现须在来源连接建立或明确的受控重同步边界建立新轮次：第一条完整 spawn 绑定当前 stream，后续事件匹配该活动 stream。不能仅凭收到不同 stream_id 的普通消息就清理有效记录或取得所有权。

来源会话失效时，清理该来源的活动记录及该轮墓碑，丢弃该连接积压消息；另一个来源的原生记录不因此失去权威。目标中断只使其本地代理无效，不能伪造原生死亡。Bridge 重启则所有内存状态为空，双方重新发送完整 spawn。

v2 的受控双向轮次/来源范围清理与当前 v1 的“任一角色断线清空 Registry”不同，属于后续实现要求。本阶段没有修改现有 test 重同步、连接逻辑或 Registry；版本协商、会话失效通知及受控重同步的生产接入需单独确认和验收。

## 7. 双向坐标规则

源发布者只发送 position.space=source；Bridge 转发后仅把 space 改成目标游戏，source/authority/origin/旋转/元数据不变。根 world_id/dimension 始终描述源上下文，不改成目标世界或维度。

统一脚底中心参考点，双精度，不取整。沿用当前配置参数，实体方向要求有限 scale>0：

```text
Minecraft → 7DTD：target_axis = source_axis * scale + offsetAxis
7DTD → Minecraft：target_axis = (source_axis - offsetAxis) / scale
```

正向和逆向使用同一组参数，不能给逆向再加正向偏移。目标 Mod 不再转换。despawn 无坐标，不做位置映射。转换输入、结果必须有限；零/负比例、溢出或重复映射空间拒绝。

离线夹具取 scale=2、offset=(100,-10,25)：Minecraft (10,64,20) → 七日杀 (120,118,65)；七日杀 (120,118,65) → Minecraft (10,64,20)。这只是草案的双向数学验证，**没有给现有 CoordinateMapper 增加逆向功能**，也没有更改实际 coordinate.json。

世界配对、模型尺寸、地形对应和目标对象类型选择不由坐标公式决定，本阶段不增加这些配置或玩法。

## 8. JSON 示例与结构校验

Schema：`docs/entity_state_v2.schema.json`。这是设计附件，不是运行配置。

| 来源 | spawn | update | despawn |
| --- | --- | --- | --- |
| Minecraft | [minecraft_spawn.json](examples/entity_state_v2/minecraft_spawn.json) | [minecraft_update.json](examples/entity_state_v2/minecraft_update.json) | [minecraft_despawn.json](examples/entity_state_v2/minecraft_despawn.json) |
| 7DTD | [7dtd_spawn.json](examples/entity_state_v2/7dtd_spawn.json) | [7dtd_update.json](examples/entity_state_v2/7dtd_update.json) | [7dtd_despawn.json](examples/entity_state_v2/7dtd_despawn.json) |

Minecraft 示例的 authority/source/origin.game 均为 minecraft；七日杀示例均为 7dtd。7dtd:zombie 是协议别名示例，不宣称已实现原生实体读取、Minecraft 目标对象生成或 AI。

沿用单条 UTF-8 JSON ≤8192 字节、深度≤8、重复键拒绝、UUID 小写带连字符、rotation 范围、metadata 扁平≤32键/字符串≤256字符规则。JSON Schema 检查形状；离线语义校验补充角色/所有权、origin 对应、活动 stream、有限数、健康值关系和字节大小。raw JSON 与输出序列化均检查字节预算；不得依赖 Schema alone 判断来源权威。

## 9. 自动验证与实际结果

在工程根目录执行：

```powershell
.\tests\run-phase3_6_0.ps1
```

需要 Python 3.11+，复用已有工程内 jsonschema 固定依赖；缺失时下载到 work/phase3_4-test/python-libs，缓存和临时文件留在 work。无需启动或重启任何游戏/Bridge，也无需重新编译 Mod。

本轮结果：**21 组测试通过，0 失败、0 错误**。覆盖六个 JSON 示例、源/连接一致性、authority 冒用、origin 改写、非法字段和版本、别名拒绝、despawn 空状态、重复/旧序号、类型冲突、未知更新、墓碑、双来源同 UUID、轮次隔离、镜像回传、正/逆坐标与溢出。

测试引用的 inverse mapper 与 OwnershipOracle 只在 tests 中，是可执行设计规格，不被任何生产组件加载。21 组包含参数化子例，不把它们宣称为实时双向游戏验收。

阶段开始保存了 23 个生产源码/项目及配置文件的 SHA-256，测试结束逐项相同；没有改变 Minecraft Mod、7DTD Mod、Bridge 或现有配置。证据：work/phase3_6_0-test/tests.log、results.json、protected-before.json。首次在无 work 缓存的检出运行时，脚本会先捕获当前基线再校验；本轮保留的是编辑前基线。后续阶段若获准修改生产文件，应另建阶段基线，不能把哈希失败当作协议缺陷。

## 10. 修改文件与停止点

新增：

- docs/phase3_6_0_bidirectional_protocol.md
- docs/entity_state_v2.schema.json
- docs/examples/entity_state_v2/ 下两来源各三个事件 JSON
- tests/bidirectional-protocol/contract.py、test_contract.py
- tests/run-phase3_6_0.ps1

修改：docs/entity_sync_protocol.md 仅增加草案入口；README.md 增加本阶段链接及边界。生成离线测试证据和依赖缓存于 work。

当前可以评审双向协议基础，但还不能在游戏里使用 v2、七日杀原生实体反向同步或自动所有权仲裁。本阶段到此停止。后续生产接入、迁移与实机验收必须等待用户确认。

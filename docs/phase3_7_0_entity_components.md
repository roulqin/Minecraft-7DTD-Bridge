# Phase 3.7.0：实体状态组件扩展协议设计

日期：2026-10-03（Asia/Shanghai）。工程根目录：`D:\wenjian\minecraft\7-M`。

## 范围与兼容性

本阶段只交付设计文档、Schema、JSON 示例和离线自动验证。**Bridge、两个 Mod、实体生成代码、配置和 entity_state v2 Schema 均不修改。** 没有启动/停止游戏，没有编译或部署新产物。

现有 entity_state v2 严格拒绝未知根字段，metadata 仅允许扁平标量。因此不向 v2 根对象追加 components，也不把嵌套组件塞入 metadata；采用独立伴随消息草案 **entity_components version=1**，以 entity_state_version=2 明确关联既有生命周期协议。它的 version 是扩展自身版本，不是 entity_state 的版本升级。

当前运行服务不认识 entity_components，发送会被既有 unsupported_type 规则拒绝；**示例不得发送到现有游戏或 Bridge。** 未来接入需要单独批准和能力协商，本阶段不设计或启用握手变化。旧 v1/v2、player_position、代理显示和 metadata 行为保持原样。

Schema：[entity_components_v1.schema.json](entity_components_v1.schema.json)。它只读引用现有 [entity_state_v2.schema.json](entity_state_v2.schema.json) 的 UUID/命名空间/世界 ID 定义，离线解析、不请求网络。

## 1. state component 模型

组件是实体当前**观察状态**，不含操作命令。以完整 origin 及 stream_id 关联源身份，不能通过同名、同坐标或仅 UUID 推断实体。

| 组件键 | 完整值 | 校验与含义 |
| --- | --- | --- |
| health | `{ "current": 90, "max": 100 }` | 有限数字，current≥0、max>0、current≤max；表示源游戏原生单位，不跨游戏换算 |
| name | `{ "text": "七日杀玩家" }` | 1..128 Unicode 码点，禁止 C0/C1 控制字符；纯文本，不解释富文本、点击事件、命令或实体类型 |
| animation_state | `{ "state": "7dtd:idle" }` | 命名空间等于 origin.game，名称≤128字符；仅声明源状态，不触发动画、AI、动作或攻击 |
| custom_metadata | 扁平 object | ≤32键；键≤64字符，字母开头，仅字母/数字/下划线/点/连字符；值为有限数字、boolean、null 或≤256字符的 string |

health=0 不自动 despawn、不发伤害命令、不推导生命期；真实死亡或退出仍由 entity_state 生命周期处理。name 不改变实体身份。animation_state 允许源命名的描述值，例如 minecraft:idle、minecraft:walking、7dtd:idle、7dtd:moving；未识别状态仅可诊断，不猜测动画资源。

custom_metadata 不允许嵌套 object/array，不解释代码、资源路径、authority、AI 或控制权限。它的键即便叫 health 或 authority，也只是普通自定义数据，不能覆盖同名组件/信封字段。

组件未出现表示“未提供/未知”，不是默认满血、空名字或 idle。源状态需由源 Mod 的未来采集器提供；现有 7DTD 本地玩家发送器未增加任何组件采集。

## 2. 独立扩展信封

必含且仅含：

- type=entity_components、version=1、entity_state_version=2。
- source、authority、origin、entity_id、world_id、dimension、entity_type、stream_id：语义沿用 v2；根身份必须对应 origin。
- entity_sequence：引用已经被 Registry 接受的**当前** entity_state.sequence，不改动 v2 原字段、不分配替代生命周期序号。
- revision：状态组件通道的版本，整数1..9007199254740991；按完整 origin+stream 独立比较。
- base_revision：patch 所依赖的已应用组件版本；snapshot 固定0，patch 范围1..9007199254740991。
- mode=snapshot 或 patch；components 至多包含四个已定义组件。

扩展消息不含 position、rotation 或 lifecycle，不能创建/移动/删除实体。没有 timestamp、伤害、输入、动画执行、authority 转移或控制指令。

origin 逻辑键为 `(origin.game, origin.world_id, origin.dimension, origin.entity_id)`；组件记录键再加 stream_id。相同 UUID 属于不同源游戏/世界时仍分开。entity_type、authority 和来源身份必须等于已注册实体，不能借状态消息修改它们。

## 3. 部分更新与状态覆盖

### snapshot：完整覆盖

snapshot 建立或重新同步完整组件状态。components 中列出的组件完整替换原值；未列出的既有组件删除。组件值不允许 null；空 components={} 合法，表示清空全部组件。

base_revision 必须0；revision 必须高于同轮已应用组件版本。首个组件消息必须 snapshot，但允许在接收端缺失基线时以较高 revision 的完整 snapshot 建立基线。

完整覆盖可能删除未列出的组件，所以权限检查同时覆盖“新快照中组件”和“旧状态中组件”；没有删除权限的 snapshot 整包拒绝，不允许通过省略字段绕过权限。

### patch：按组件部分更新

patch 只修改列出的组件，未列出的保持原样。每个非 null 组件值是**该组件的完整替换**，不递归合并内部字段；例如更新 health 必须同时带 current/max，不能仅提供 current。

组件值为 null 表示删除该整个组件。删除本来不存在的组件可视为幂等删除，仍需拥有该组件权限。patch 的 components={} 禁止，以免空操作消耗版本。

custom_metadata 在 patch 中也是整张扁平表替换；其中某个键值 null 是普通 JSON 值，**不是删除键**。删除单个元数据键应提交省略该键的完整 custom_metadata 表；删除整个表使用 `"custom_metadata": null`。此模型不是 JSON Merge Patch 或 JSON Patch。

patch 必须已有 snapshot 基线，base_revision 等于最后实际应用的 revision，且新 revision 更大。允许版本跳号，但不能跳过基线依赖。依赖不匹配则拒绝，保留旧状态；未来发送端用新完整 snapshot 恢复。没有自动回滚、冲突合并或未定义的重同步消息。

### 原子性和旧版本

先校验整包结构、身份、权限、依赖与组件关系，再一次提交全部变化；任一组件失败时其它组件也不更新，revision 不提高。revision 小于或等于已应用版本时，在来源/权限等校验通过后幂等忽略，不重新应用。

snapshot 的高版本可恢复版本缺口；旧 patch 不覆盖更新的 snapshot。序号使用的是最后**实际应用**版本，不是收到的最大值或墙上时间。

entity_sequence 必须精确等于可信 Registry 当前 sequence；过旧或超前均拒绝，不排队等待、不凭状态组件创建生命周期记录。建议未来同一个 WebSocket 内先发布并应用 entity_state，再发布引用它的组件消息，Bridge 用同一生命周期锁校验关联。若较新的位置事件抢先应用，使组件引用过旧，源端重新基于当前实体序号发送 snapshot/patch。

已接受的组件状态在后续位置 update 时保持；entity_sequence 不表示每次移动都要清空健康/名字。组件不可靠投递的风险不能靠 revision 消除；本阶段没有增加 ACK、离线缓冲或应用确认协议。

## 4. 权限判断

按以下顺序进行设计校验：

1. source 等于握手角色；source=authority=origin.game，仅原生所有者可写。
2. Registry 有活动实体，origin/type/authority/stream 精确匹配；entity_sequence 等于其当前 sequence。
3. 组件类型已定义，且该源实体类型的组件发布/存储策略**明确授权**该组件；缺失、false 或不支持均拒绝。
4. 校验 revision、base_revision 及所有组件内容，再原子应用。

代理不能把自身显示状态反写为源状态。Bridge 不授予 authority，custom_metadata 不能授予权限。另一游戏发送 health/name 删除也属于写入，不能绕过所有权。

当前 entity_types.json 的 health=false，且没有 name/animation_state/custom_metadata 发布权限字段。**本阶段不把 spawn/update_rotation 或 ai=false 当作组件授权，不扩展当前类型配置。** 未来应设计独立组件发布/存储与目标显示能力，默认拒绝；源发布权限和目标表现能力分开，授权存储 animation_state 不等于允许执行动画。

离线 ComponentOracle 的 grants 参数是调用者提供的可信**测试上下文**，不从消息或 metadata 获取，不是新增运行配置。示例在“该源类型明确授权四个组件”的假设下验证，不表示生产已启用健康/名字/动画。

既有 v2 metadata.health/max_health/display_name 和扩展组件没有自动复制/合并关系。未来接入组件显示后，仅以被授权的组件通道作为组件状态来源；旧 metadata 保留原语义，禁止把两套状态做最后到达者覆盖。现有 Mod 不改展示行为。

## 5. 生命周期、重连及数据边界

组件消息只跟随活动 v2 实体，不能自身 spawn 或恢复退休身份。despawn 必须清理对应组件状态；未知/已退休实体的晚到组件拒绝。源连接/受控轮次结束清理该来源组件，不清理另一个来源；Bridge 重启丢弃内存组件，等待源重新 spawn 后发送新 snapshot。单靠新的 stream_id 不能取得身份或控制权。

沿用单条原始 UTF-8 JSON≤8192字节、深度≤8、重复键拒绝、有限数字、标准小写 UUID 的边界。Schema 校验对象结构与类型；离线语义验证补充跨字段等值、角色、权限、活动记录、序号引用、health 关系、名称控制字符、动画命名空间及字节预算。NaN/Infinity/浮点溢出和 bool 伪装 integer 都拒绝。

本阶段参考模型无生产容量管理、调度或数据持久化；未来接入需明确复用 Registry 容量和组件内存预算，不能无界缓存实体或等待乱序数据。

## 6. JSON 示例

| 来源 | 完整快照 | 部分更新 | 删除 name | 全量覆盖 |
| --- | --- | --- | --- | --- |
| Minecraft | [minecraft_snapshot.json](examples/entity_components/minecraft_snapshot.json) | [minecraft_patch.json](examples/entity_components/minecraft_patch.json) | [minecraft_remove_name.json](examples/entity_components/minecraft_remove_name.json) | [minecraft_replace.json](examples/entity_components/minecraft_replace.json) |
| 7DTD | [7dtd_snapshot.json](examples/entity_components/7dtd_snapshot.json) | [7dtd_patch.json](examples/entity_components/7dtd_patch.json) | [7dtd_remove_name.json](examples/entity_components/7dtd_remove_name.json) | [7dtd_replace.json](examples/entity_components/7dtd_replace.json) |

[clear_snapshot.json](examples/entity_components/clear_snapshot.json)：七日杀 revision=6 的空完整快照，显式清空所有组件。

同来源示例按 snapshot revision1 → patch revision2/base1 → 删除 name revision3/base2 → snapshot revision5/base0 演示。最后 snapshot 只剩 name，health/animation_state/custom_metadata 因全量覆盖而移除。entity_sequence=1 仅是假定生命周期尚未推进的离线夹具；真实接入时必须引用实际 Registry 当前序号。

七日杀部分更新核心片段（完整身份和权限关联见 JSON 文件）：

```json
{
  "type": "entity_components",
  "version": 1,
  "entity_state_version": 2,
  "mode": "patch",
  "entity_sequence": 1,
  "revision": 2,
  "base_revision": 1,
  "components": {
    "health": { "current": 90, "max": 100 },
    "animation_state": { "state": "7dtd:moving" }
  }
}
```

上述片段省略必填身份字段，仅用于讲解；不能作为完整有效消息发送。完整可验证消息均在示例目录。

## 7. 自动验证及结果

```powershell
Set-Location 'D:\wenjian\minecraft\7-M'
.\tests\run-phase3_7_0.ps1
```

使用工程既有 work/phase3_4-test/python-libs 中的 jsonschema 与 referencing；在干净环境需要先按 tests/entity-types/requirements.txt 安装到该工程内目录。Schema URI 使用 .invalid 标识符，引用通过离线 Resource 注册解析，测试不请求网络、不连接运行中的 Bridge。

**42 组测试全部通过，9 份 JSON 示例通过。** 覆盖两个来源、完整覆盖、部分更新、删除、元数据 null/替换、原子性、旧 revision、错误依赖、基线恢复、角色和所有权、活动实体、stream、entity_sequence、缺失权限、省略删除权限、数字/UTF-8预算、坏结构、控制字符、未知组件、despawn 与来源范围清理。另校验既有六份 v2 核心示例仍有效，v2 仍拒绝新增 components 根字段。

生产文件保护：测试与阶段前 SHA-256 清单比对两个 Mod 源代码/资源、Bridge 源代码、项目文件、配置及现有 v2 Schema，全部一致；目录中文件新增/删除亦需核对。清单与运行结果在 work/phase3_7_0-test/protected-before.json、results.json、tests.log。初次运行仅测试读取编码不匹配，已明确使用 UTF-8 修正并最终通过。

本阶段不需要编译或实机验收，因为没有生产代码变化；离线 Oracle 是可执行设计参考，不是 EntityRegistry 替换或 Bridge 接入实现。

## 8. 文件列表与停止点

新增：

- docs/phase3_7_0_entity_components.md
- docs/entity_components_v1.schema.json
- docs/examples/entity_components/ 下九份 JSON
- tests/state-components/component_contract.py
- tests/state-components/test_components.py
- tests/run-phase3_7_0.ps1

自动验证证据：work/phase3_7_0-test/。未修改任何生产文件、协议核心或实体生成代码。

Phase 3.7.0 完成后停止。不进入状态采集、健康应用、名字显示、动画执行、AI、战斗或下一阶段。

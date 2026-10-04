# Phase 3.7.2：Identity Component 运行时接入与验收

日期：2026-10-03。所有实现、配置、测试与证据位于 `D:\wenjian\minecraft\7-M`。

## 结果与范围

已完成 **7DTD 原生玩家身份 → Bridge → Minecraft 组件存储与日志**：初始 name/display_name/metadata 标签、partial update、权限检查、remove、恢复和正常世界退出清理。新增 74 项 Identity 检查通过；Health 和既有实体回归通过；三个组件编译成功；实机 23 项证据对照全部通过。

保留 entity_state v2 的字段、Schema、所有权与生命周期逻辑。没有修改端口、CoordinateMapper、实体类型映射、原 EntityRegistry 或双方生成代码。没有 inventory、combat、动画播放或 AI 实现。Minecraft 本阶段以接收日志呈现身份，不增加头顶名字或标签 UI，也不修改本地 Minecraft 玩家名字。

## Identity 在线表示

复用 entity_components v1 的 name 和 custom_metadata，不另造 entity_state 字段、独立 entity ID 或另一套生命周期。

```json
{
  "components": {
    "name": { "text": "原生名字", "display_name": "显示名" },
    "custom_metadata": { "tag.source": "7dtd", "tag.kind": "player", "tag.label": "bridge" }
  }
}
```

这是组件片段；完整有效消息见 [initial.json](examples/identity_components/initial.json)、[update.json](examples/identity_components/update.json)、[remove.json](examples/identity_components/remove.json)。例子的 lifecycle sequence/revision 为离线序列，真实发送必须引用该轮活动 Registry 当前序号，不可直接复制到已有实体。

- name.text 是源游戏的 canonical name，对应七日杀 `EntityPlayerLocal.EntityName`；它不是 entity_id，名字改变不能改变所有权或生成第二个实体。
- display_name 是 name 组件内可选字段，默认来自原生 `PlayerDisplayName`，无值时回退原生名字；工程配置可提供观察标签的显示名覆盖。没有改名游戏账户或写回游戏原生名字。
- 更新 name 必须仍包含 text；display_name 不出现时表示该字段未提供/移除，不递归合并旧 display_name。现有只含 text 的 v1 name 消息依然有效。
- 文本为纯文字，1..128 Unicode 码点，禁止 C0/C1 控制字符和无效代理项；不解释富文本、命令、资源路径或权限。
- metadata 标签使用 custom_metadata 内 `tag.*` 键：键≤64字符，tag. 后至少一字符，只允许字母、数字、下划线、点、连字符；值为1..256码点的纯文字，禁止控制字符。最多32键，不使用嵌套对象/数组。
- 非 tag. 的普通 custom_metadata 仍支持原有有限数字、boolean、null、长度限制字符串；这些值不能授予权限。tag. 是本阶段明确保留的字符串标签命名空间。

[entity_components_v1.schema.json](entity_components_v1.schema.json) 仅为原 name 对象新增可选 display_name；v2 核心 Schema 未改。Schema 管结构和长度，C#/Java 与离线 Oracle 补充权限、跨字段身份、控制字符和 tag. 语义。

## 共用组件状态、partial 与 remove

health/name/custom_metadata 共用同一个实体轮次的 revision/base_revision，不为 Identity 新建竞争的版本通道。Bridge 在原生命周期锁下验证并原子提交整包，Minecraft 接收器按同一顺序更新观察状态。

snapshot 完整覆盖所有组件；省略已有组件也属于删除，需要它的权限。patch 只改变列出的组件，省略的保持原样；每个对象值都是整个组件替换，不能递归 merge。更新 metadata 标签时提交整张表，省略旧键即可删除该键；普通 metadata 键值 null 是普通值，不是删除指令。

删除整个身份观察使用：

```json
{ "components": { "name": null, "custom_metadata": null } }
```

此片段必须放在有活动实体、正确权限与 base_revision 的 patch 内。仅删 Identity 时保留 health；空 snapshot 可清空全部组件，但同样检查被省略组件的权限。snapshot 不允许 null 值；健康、身份、标签任一项不合法时整包拒绝，不提高 revision。

原生世界退出先排队删除该轮全部已提供组件，再排队 entity_state despawn；接收端在 despawn 删除组件版本基线。重连仍由既有 NativePlayerPublisher 轮次产生 fresh spawn 和完整组件 snapshot。没有引入 ACK、可靠重放、持久化或新的握手消息。

## 权限与采集配置

[component_permissions.json](../config/component_permissions.json) 为已启用且映射到 Minecraft 的 7dtd:player 分别授权 health、name、custom_metadata 的 publish/store。每个组件须两项都明确 true；缺失、false、未知组件或未经活动 Registry 接受的来源均拒绝。删除和 snapshot 的省略删除也执行同样检查。

source 必须等于握手角色，且 source=authority=origin.game=7dtd；完整 origin、类型、stream 与 entity_sequence 必须匹配原生 Registry。metadata 内即便存在 authority/name 之类普通键，也不能改写信封或权限。组件权限启动时加载，变更后重启 Bridge。

现有 entity_types.json 与 gameplay health=false 保持不变；授权身份/健康观察不赋予输入、伤害、治疗或实体操作能力。animation_state 仍被运行时拒绝。

[identity_labels.json](../config/identity_labels.json) 与 [Schema](../config/identity_labels.schema.json)：

- enabled=true：采集真实名字，提供显示名和标签；enabled=false：停止提供 name/custom_metadata，发送显式 remove，health 继续发布。
- display_name=null：使用原生显示名；字符串：只覆盖同步观察的显示名。
- tags：完整 tag.* 表，变更后从七日杀 Mod 发起组件 patch。

七日杀在 GameUpdate 主线程读取名字；配置读取间隔至少500ms，组件与位置共用既有500ms采样节流，未变化不重复发送。配置无效时保留上次有效观察并记录错误，不把解析失败当作 Identity remove。此 enabled 只控制观察，不是服务器权限授权。

NativePlayerPublisher 的既有 health-only 构造/调用兼容保留。新增组合发布回调使用同一原生 FIFO，不阻塞游戏主线程做网络收发。组件序列化保留正式 JSON 对象/null，不附加 .NET __type 信息。Minecraft 接收与日志在连接线程执行，不触碰世界对象。

## 编译与自动测试

```powershell
Set-Location 'D:\wenjian\minecraft\7-M'
.\tests\run-phase3_7_2.ps1
```

使用现有 JDK21、.NET10、net48 游戏引用和工程内 Gradle/Python 依赖，不修改原游戏安装或安装其他项目依赖。

| 检查 | 结果 |
| --- | --- |
| Identity C# 状态/权限/配置采样/发布与实际 WebSocket 到 Java | 51 项通过 |
| Minecraft Identity 接收、文本验证、原子更新及删除 | 23 项通过 |
| Health 运行时回归 | 59 项 C# + 20 项 Java 通过 |
| 原 v1/v2/marker/player proxy/Java 投影回归 | 235 项通过 |
| 类型目录 Schema/adapter | 5 组通过 |
| 既有组件设计行为 | 41 组通过 |
| C# 实际组合发布序列化 | 7 份组件消息通过扩展 Schema 和 Oracle |
| 权限/Identity 配置 Schema | 通过 |
| Bridge、7DTD、Fabric 编译 | 成功；C# 0 警告、0 错误 |

覆盖：canonical/display 名字、Unicode 码点、控制字符、类型和长度、扁平标签、普通 metadata null、name-only patch 保留 health/标签、metadata 表替换、删除保留 health、权限缺失/false、删权限、snapshot 省略删除权限、authority、错误版本依赖、旧 revision、调用者对象隔离、重新提供、重连全快照、退出清理、500ms配置采样和无效配置保留。

回归命令：`.\tests\run-phase3_7_1.ps1`、`.\tests\run-phase3_6_2.ps1`。Phase 3.7.0 的历史“生产文件完全不变”测试不适用于本阶段授权的运行修改；其41组行为检查继续执行，原历史基线不覆盖。本轮另保存并核对 v2/正式网络/坐标/类型映射/生成代码 SHA-256，均不变。

证据：[work/phase3_7_2-test](../work/phase3_7_2-test/)，回归证据仍在对应 work/phase3_7_1-test、work/phase3_6_2-test。

## 真实游戏验收

Minecraft 1.21.11 / Fabric 0.18.4 / API 0.140.2+1.21.11，PID 2180；7DTD V3.2.0 B10，PID 26548；Bridge Phase372 PID 20556。继续 localhost:18771。网络/坐标验收配置复用上一阶段并复制到本轮证据目录，正式 config/network.json、coordinate.json 不变。

使用真实 MC runtime 世界和七日杀 MC7DTD-Phase3-3 存档。entity_id=`5379c3ed-876b-4702-8420-365330557e73`，stream_id=`64516623-0a28-4c89-b419-bc09e2d2847e`。

| 验收项 | 实际证据 | 结果 |
| --- | --- | --- |
| 原生初始身份 | 20:44:32，revision=1 snapshot，真实中文原生名字/显示名及 tag.label=bridge；Minecraft 同值 | 通过 |
| 健康同时存在 | 同快照 health=93/100，后续 health-only patch | 通过 |
| 显示名与标签更新 | 20:45:28，revision=3 patch，显示名 MC7DTD Identity Updated、tag.label=runtime-update；名字保留，health=94/100保留 | 通过 |
| Identity remove | 20:47:17，revision=6 patch，name=null/custom_metadata=null；Minecraft removed=true，health=96/100保留 | 通过 |
| 恢复默认配置 | 20:47:36，revision=7 patch，原生显示名与 bridge 标签恢复 | 通过 |
| 正常世界退出 | 20:48:27，revision=9 全组件 remove，随后 despawn sequence=468、world_unloaded；代理 count=0 | 通过 |
| 非断线清理 | remove 前后及退出后 /health 两端在线；完整验收区间无 disconnect/reset | 通过 |

改变的是工程内配置，由真实七日杀 Mod 主线程采集并通过实际 WebSocket 发出；没有测试进程伪装七日杀，没有直接向 Bridge 注入验收身份数据。原生名字、健康与生命周期均来自真实玩家。没有声称改动了游戏账户名或真实 HUD 上的名字。

[verification.json](phase3_7_2-runtime-evidence/verification.json) **23 项通过**：每份真实组件消息通过原生命周期序号及扩展 Oracle；Minecraft 身份日志中的全部状态与源状态逐项一致，包含健康保留、删除、正常退出、连接连续性、部署哈希及配置恢复。

[实机证据目录](phase3_7_2-runtime-evidence/) 包含两端日志、Bridge 日志、component-events.json、3份配置状态副本、连接快照、artifact-hashes.json、7dtd-identity-world.png 和 7dtd-world-exited.png。实际身份名称保留在本地 JSON/日志证据中。

复核本轮保存证据：

```powershell
$env:PYTHONPATH='D:\wenjian\minecraft\7-M\work\phase3_4-test\python-libs'
python .\tests\verify-phase3_7_2-runtime.py
```

## 启动与复验

正常退出已有两个游戏，结束旧 Bridge，每个角色只开一个实例；已有 Steam 登录、工程内 Minecraft runtime 参数和存档需可用。

```powershell
.\tests\launch-phase3_7_2.ps1 bridge
.\tests\launch-phase3_7_2.ps1 minecraft
.\tests\launch-phase3_7_2.ps1 7dtd
```

进入两款游戏的测试世界，查看 runtime/minecraft/logs/latest.log 的 `7DTD identity received:`。修改 identity_labels.json 的 display_name/tag.label，等待至少一个采样周期，确认 patch 和实际状态；将 enabled=false，确认 Identity remove 且 health 保留。恢复配置后正常退出七日杀世界，确认全组件 remove、despawn 与在线连接。

启动器会覆盖本轮运行日志，复验前另存已有证据。保存的运行验证器针对上述本轮值；重跑游戏需重新采集与更新对照证据。本轮结束时 identity_labels.json 已恢复默认字节内容。

## 修改文件列表

新增：

- bridge-server/IdentityComponent.cs
- 7dtd-mod/src/IdentitySample.cs
- minecraft-mod/src/main/java/io/mc7dtd/IdentityComponent.java
- minecraft-mod/src/test/java/io/mc7dtd/IdentityHarness.java
- config/identity_labels.json、config/identity_labels.schema.json
- tests/identity-runner/IdentityRunner.csproj、Program.cs、validate_wire.py
- tests/run-phase3_7_2.ps1、tests/launch-phase3_7_2.ps1、tests/verify-phase3_7_2-runtime.py
- docs/examples/identity_components/initial.json、update.json、remove.json
- docs/phase3_7_2_identity_runtime.md、docs/phase3_7_2-runtime-evidence/

修改：

- bridge-server/HealthComponents.cs：共用状态、逐组件权限、Identity验证；既有 Health API保持。
- bridge-server/Program.cs：组件日志名称调整，复用既有消息入口与清理路径。
- 7dtd-mod/src/BridgeClient.cs：复用同一队列发送组合组件。
- 7dtd-mod/src/NativePlayerPublisher.cs：共用 revision、增量组合采样与清理。
- 7dtd-mod/src/BridgeMod.cs：读取原生名字/显示名及标签配置。
- minecraft-mod/src/main/java/io/mc7dtd/HealthReceiver.java：共用组件状态、Identity验证与日志。
- docs/entity_components_v1.schema.json：新增可选 name.display_name。
- config/component_permissions.json、config/component_permissions.schema.json：name/custom_metadata 明确授权。
- tests/state-components/component_contract.py：显示名和保留标签语义校验。
- tests/health-runner/Program.cs：未知组件反例从已支持 name 改为 inventory，原检查仍保留。
- tests/client-harness/ClientHarness.csproj、tests/marker-runner/MarkerRunner.csproj、tests/player-proxy-runner/PlayerProxyRunner.csproj、tests/native-transport-runner/NativeTransportRunner.csproj、tests/health-runner/HealthRunner.csproj：链接同一共享 Identity DTO，供原回归编译。

构建/测试产物和检查辅助工具在工程 build、bin/obj、work；只部署到工程内 runtime 的 Mod。没有修改其他项目或原游戏安装。

## 遇到的问题与限制

本机 7DTD API 是 EntityName/PlayerDisplayName 属性，首次尝试不存在的 GetEntityName 未编译；检查本地游戏程序集公开元数据后修正，最终编译与实机通过。未做内存修改、注入或 Hook。

一次直接重跑测试时 Java 网络 Selector 因宿主临时路径失败，连接测试超时；已将测试子进程 TEMP/TMP 显式限定到工程 work，最终51项通过。源健康回归中的 name 原本是未知组件反例，本阶段成为合法组件后改用 inventory 测试拒绝，未删除这项边界检查。

实机更新的是源 Mod 的显示名/标签观察配置；不修改原生账户名，不渲染头顶文字，不反向写入游戏，不做身份持久化、自动权限协商或可靠 ACK。原生名字按500ms采样观察；本轮未尝试通过 Steam 改名。权限/恶意输入拒绝和重连基线通过自动测试；实机覆盖初始、配置变更、remove/恢复和正常世界退出。

当前七日杀停留主菜单，Minecraft 仍在真实测试世界，Bridge 保持运行。Phase 3.7.2 完成后停止，等待下一阶段。

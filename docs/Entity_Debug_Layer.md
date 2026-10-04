# Phase 3.7.4 — Entity Debug Presentation Layer v1

## 范围

实现只读 EntityInspector、Minecraft 查看命令、7DTD 调试日志和开发用世界内名称标签。名称标签附加在既有被动代理附近，不改变正式模型、动画、HUD、碰撞、AI、控制或战斗。

不增加同步组件，不修改 entity_state v2、entity_components v1、Presentation 结构、Authority、两个 Registry 的核心逻辑、坐标映射或 WebSocket 消息集合。Debug 是本地观察层，不能生成新的权威实体或改写组件。

## 状态读取

Bridge 的 EntityInspector 只镜像 **已被原 Registry 接受** 的 lifecycle 数据；读取时再次确认记录仍活动，并从原 HealthComponents/PresentationComponent 取得深复制。其缓存不是第二套生命周期状态机，不负责接受/拒绝同步事件。

一个结果包含：id/type/source、world/dimension、stream/sequence、已经映射到目标游戏的 position/rotation、metadata，以及 components 中的 identity、health、presentation、authority。

诊断字段 identity.name 来自现有 name.text，display_name 来自 name.display_name，identity.metadata 来自 custom_metadata。这个字段转换只出现在 Inspector 输出，**不是 Identity 协议变更**。authority.owner 显示原 authority；旧 v1 没有此字段时显示其 source，origin 可以为空。未实际发布的组件显示 `{}`，不伪造 Health 或 Identity；Presentation remove 后也显示 `{}`。

读取返回深复制，修改返回 JSON 不会影响组件、Registry 或后续消息。despawn、断线和已有重连清理边界同步清理诊断快照。实体身份包括 source/world/dimension/id；重复 id 必须限定范围。Inspector 只能读当前进程状态，不保证历史记录或可靠送达 ACK。

Minecraft 的 EntityInspector 保存本地收到的 7DTD 实体及已验证组件观察值。它是线程安全的本地代理视图，不把 Minecraft 自身尚未确认送达的发送状态冒充接收状态。跨两端的全局视图使用 Bridge 的实体列表。

## Bridge Inspector API

为遵守“不修改 WebSocket 通信协议”，`inspect_entity` 是**独立的本机 HTTP 调试命令**，不发送到 `/ws`，不增加新的 WebSocket 请求或回复。使用同一 localhost 监听端口 **18771**，不修改 config/network.json。

### 查询单实体

`POST http://localhost:18771/debug/inspect_entity`，Content-Type 为 application/json：

```json
{"type":"inspect_entity","entity_id":"player001"}
```

player001 是格式示例；实际项目 id 为既有 UUID，请从实体列表或日志复制。可选 source/world_id/dimension 用于消歧：

```json
{
  "type":"inspect_entity",
  "entity_id":"cd787d90-736c-4428-9c35-3b8b6c0fe4f8",
  "source":"7dtd",
  "world_id":"td-demo",
  "dimension":"7dtd:main"
}
```

返回示例（省略位置等外围字段）：

```json
{
  "id":"cd787d90-736c-4428-9c35-3b8b6c0fe4f8",
  "type":"7dtd:player",
  "components":{
    "identity":{"name":"Steve","display_name":"Steve Display","metadata":{"tag.role":"test"}},
    "health":{"current":20,"max":20},
    "presentation":{"renderer":"humanoid","model":"survivor","variant":"default","scale":1},
    "authority":{"source":"7dtd","owner":"7dtd","origin":{}}
  }
}
```

完整返回结构见 docs/entity_debug_inspector.schema.json。此 schema 仅供诊断输出，不用于同步消息校验。

响应：200=找到；404=不存在或已退出；409=同 id 多个实体，返回候选 source/world/dimension；400=无效 JSON、未知/重复字段、缺失 id、非字符串标识或超过8192字节；403=非本机或带浏览器 Origin 的请求。调用 Inspector 不能发送 spawn/update/despawn，也不能注入 authority 等额外字段。

### 查看全部 Bridge 活动实体

`GET http://localhost:18771/debug/entities` 返回 `{"entities":[...]}`，按 source/world/dimension/id 排序。未连接或暂无同步实体时为 `[]`。

```powershell
$entities = Invoke-RestMethod 'http://localhost:18771/debug/entities'
$entities.entities | Select-Object id,type,source,world_id,dimension
$selected = $entities.entities | Select-Object -First 1
$request = @{type='inspect_entity';entity_id=$selected.id;source=$selected.source;world_id=$selected.world_id;dimension=$selected.dimension} | ConvertTo-Json
Invoke-RestMethod 'http://localhost:18771/debug/inspect_entity' -Method Post -ContentType 'application/json' -Body $request | ConvertTo-Json -Depth 12
```

## Minecraft 调试命令与输出

两个命令始终注册，不要求打开实体发送测试入口，不发送网络同步消息，也不修改实体：

```text
/mc7dtd_entity_inspect
/mc7dtd_debug_entities
```

第一个读取最近收到生命周期事件的实体，输出 `======== Entity Inspector ========` 和完整诊断 JSON。第二个列出本地所有当前接收实体：id、type、model、HP 和 source world/dimension。结果写入现有聊天反馈和 Minecraft 日志，没有新增 GUI 面板。没有实体或组件时显示 no synced entities/default/HP:unknown，不能把 unknown 解释为 0 生命值。

组件观察更新会更新 Inspector 与名称标签；不必等下一次 position update。命令只读，不改变采集节奏、权限或队列。

## Debug Name Tag 与日志

config/debug.json：

```json
{"debug_name_tag":true,"debug_logging":true}
```

两个开关独立；在各进程启动时加载，修改后重启 Bridge 和两个游戏。缺失/无效配置默认关闭新增显示，配置 schema 为 config/debug.schema.json。

- debug_name_tag=false：不创建开发标签；既有代理外形和同步继续运行。
- debug_logging=false：不写新增自动 Debug 日志；原阶段的连接、Health、Identity、Presentation 日志保持。用户显式执行查看命令仍得到反馈和结果日志；HTTP Inspector 仍可只读查询。

Minecraft 使用临时 Text Display，以世界坐标附加在已有代理上方并面向相机：

```text
[实体 UUID]
survivor
HP:20
```

7DTD 使用单独的 TextMesh 开发标签 `MC_实体UUID`，跟随代理及浮动原点；它不创建 EntityAlive、UI Canvas、AI 或正式角色模型。使用现有 Unity 的 TextRenderingModule，没有大型第三方依赖。

标签由游戏主线程创建、刷新和删除，在代理 despawn、世界切换、连接重置时一起清理。Minecraft 标签通过独立适配器测试，标签创建失败不能中断核心代理同步；7DTD 字体/标签创建失败也退回原代理。

7DTD 在实际代理创建成功后输出：

```text
[Entity Debug]
Spawn Proxy
id: ...
type: minecraft:player
presentation:
renderer=humanoid model=player variant=default scale=1
```

scale 改变时：`[Entity Debug] Presentation Update entity=... scale: 1 -> 1.5`。remove 以 removed 表示，不把 null 当作 scale=0。既有拒绝/重复事件不会冒充成功生成日志。

## 编译和自动测试

在工程根目录执行：

```powershell
.\tests\run-phase3_7_4.ps1
```

产物：Minecraft JAR 保持 minecraft-mod/build/libs/mc7dtd-bridge-0.1.0.jar；Bridge 位于 bridge-server/bin/Phase374/net10.0；7DTD 位于 7dtd-mod/bin/Phase374/net48。脚本使用工程内 work 保存依赖、日志和临时网络配置，测试随机端口不改正式端口，不启动真实游戏。

本阶段测试覆盖完整 Inspector、缺失组件、返回副本只读、实体列表和同 id 消歧、HTTP 请求/重复查询、配置关闭、名称标签创建/Health刷新/删除，以及实际 C# 7DTD发送类 → Bridge → Java Minecraft接收类的数据观察。继续运行 Presentation、Health、Identity 和既有代理的回归测试。

24 份核心文件以阶段开始的 SHA-256 为基准确认未修改；另比较 Bridge 原 WebSocket 实现，除只读 Observe/Clear 挂接外保持相同。

### 本轮自动验证结果（2026-10-04）

```text
Minecraft build PASS
Bridge build PASS
7DTD build PASS

Entity Inspector             PASS
Missing Component            PASS
Debug Command                PASS
Entity List                  PASS
Debug Config                 PASS
```

Bridge 与 7DTD 为 0 错误、0 警告；Fabric BUILD SUCCESSFUL。完整 run-phase3_7_4.ps1 退出码 0。

新增验证 **94 项通过**：C# Inspector/HTTP/真实进程传输 42、Java Inspector/名称标签适配器 14、schema/受保护文件/注册检查 38。既有回归 **216 项通过**：Presentation C# 42 + Java 10，Identity Java 23，Health Java 20，NativeProxy Java 29，Identity/Health C# 51，组件契约 41；另验证7条原组件实际序列化消息。

证据在 work/phase3_7_4-test：results.json、inspector-snapshot.json、debug.log、EntityDebugHarness.log、schema.log、各 regression.log 与三份 build.log。bridge.log、minecraft.log、7dtd-client.log 是自动测试进程日志，不是本阶段真实游戏运行证明。人工验收状态：**待用户执行**。

## 用户人工验收（待执行）

本轮不使用 computer-use，也不自动启动或操作真实游戏。以下启动与操作由用户执行。

1. 退出旧游戏、结束旧 Bridge，保持 Steam 登录。在工程根目录依次运行：

```powershell
.\tests\launch-phase3_7_4.ps1 bridge
.\tests\launch-phase3_7_4.ps1 minecraft
.\tests\launch-phase3_7_4.ps1 7dtd
```

2. 进入两个测试世界。七日杀玩家正常进入后，Minecraft 应出现既有代理与开发标签；HP 是真实七日杀生命值，不固定为示例20。
3. Minecraft 输入 `/mc7dtd_entity_inspect`，检查 id/type、Presentation、Health、Identity 与 authority.source/owner=7dtd；输入 `/mc7dtd_debug_entities`，核对本地实体数量及 model/HP。需要两个方向全局列表时使用 Bridge HTTP 列表。
4. 观察 7DTD 的自动 Minecraft 玩家代理：标签应为 `MC_UUID`，日志有 `[Entity Debug] Spawn Proxy` 和 Presentation 的 model/scale。
5. 如需检查 scale 调试日志，使用上一阶段保留的 `/mc7dtd_presentation_test spawn`、`update`、`remove`、`add`、`despawn`，各间隔至少1秒。`update` 后查看 scale `1 -> 1.5`，不要求正式模型大小改变。手动启动器继续打开原有验收入口，不新增同步功能。
6. 在七日杀内用原生玩法改变生命值，Minecraft 调试标签及 Inspector 的 HP 应更新。正常退出七日杀世界后，Minecraft 代理/标签应删除，列表移除该实体；正常退出 Minecraft 世界后，七日杀代理/标签应清理。
7. 将 config/debug.json 两项改为 false，并重新启动三端。既有同步正常，两个游戏不新增开发名称标签，七日杀不出现新的 Entity Debug 日志。查看命令仍只读可用。完成后按需恢复两个 true 并重启。

新启动器只部署到工程内 runtime，日志保存在 docs/phase3_7_4-runtime-evidence；重复启动会覆盖同名日志。Minecraft 完整日志仍在 runtime/minecraft/logs/latest.log，Bridge 在 bridge-stdout.log，7DTD 在 7dtd-game.log。本轮自动测试日志位于 work/phase3_7_4-test，**不能作为游戏画面或实机可见性验收证明**。

## 本阶段修改文件

Bridge/：新增 EntityInspector.cs、InspectorEndpoints.cs；修改 Program.cs 增加只读挂接、BridgeServer.csproj 链接 DebugConfig。

Minecraft/：新增 DebugConfig.java、EntityInspector.java、EntityDebugCommands.java、DebugNameTagLayer.java、MinecraftDebugTags.java、src/test 的 EntityDebugHarness.java；修改 BridgeClient.java 的调试观察、MinecraftBridgeMod.java 的注册、MinecraftProxyScene.java 的标签挂接。

7DTD/：新增 src/EntityDebugLog.cs、DebugProxyScene.cs；修改 BridgeMod.cs、MarkerController.cs 添加可选日志/标签挂接，MC7DTD.Mod.csproj 引用共享配置与已有 Unity TextRenderingModule。

Components/：新增 components/debug/DebugConfig.cs；现有 Presentation 组件代码/schema 未修改。

Config/：新增 debug.json、debug.schema.json；网络、坐标、实体类型、组件权限配置未修改。

Tests/：新增 debug-runner/DebugRunner.csproj、Program.cs、validate.py，run-phase3_7_4.ps1、launch-phase3_7_4.ps1；修改 marker-runner、player-proxy-runner、presentation-runner 的 csproj 链接可选调试日志，presentation-runner/Program.cs 允许指定新 Bridge DLL（原默认路径保留）。

Docs/：新增本文件、entity_debug_inspector.schema.json。bin/obj/build/work 中构建及测试产物均位于指定工程目录。

Phase 3.7.4 完成后停止，等待人工验收，不进入 Phase 3.8。

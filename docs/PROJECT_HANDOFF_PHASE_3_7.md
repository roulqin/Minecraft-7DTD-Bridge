# MC7DTD-Bridge 项目交接：Phase 3.7

交接日期：2026-10-04，Asia/Shanghai。工程唯一根目录：**D:\wenjian\minecraft\7-M**。

本文是新 Codex 对话的首读文档，不需要历史聊天即可理解现状。代码开发停在 **Phase 3.7.4 / Entity Debug Presentation Layer v1**，未开始 Phase 3.8。本次交接只创建文档和工程 work 内的核对统计，不修改源码、协议、配置、Git 提交或运行中的进程。

## 0. 新对话先执行什么

1. 确认工作目录为 `D:\wenjian\minecraft\7-M`；工具默认 cwd 可能指向另一个项目，必须显式切换。不得在其他项目目录创建本项目文件。
2. 阅读本文件、`docs/Entity_Debug_Layer.md`、`docs/Entity_Presentation_System.md`，然后阅读需要改动的实现与测试。
3. 检查 Git 的未提交与未跟踪文件；最新功能大量尚未提交，不能只看 HEAD 或从远程重新克隆替代当前工作区。
4. 区分当前源码、已构建产物、实际运行进程。最新构建是 Phase374，但交接时占用18771端口的是旧 **Phase373 Bridge**。
5. 先完成用户人工 Phase 3.7.4 验收；用户明确授权进入 Phase 3.8 后再设计/实现 Equipment。不要由“下一阶段建议”推断已经获准开发。
6. 禁止 computer-use 自动验收；不要接管游戏窗口、模拟移动/点击、截图当作自动验收。提供手动步骤，等待用户操作与证据。

**文档优先级提醒：** README 当前阶段文字仍停留在 Phase 3.6.2；environment_report.md 是 Phase 0 历史快照，其中“无 Gradle Wrapper / 非 Git 仓库”等结论已过时。以本交接、当前源码/配置和最新实际构建日志为准，历史文档用于理解对应阶段。

## 1. 项目目标

开发 Windows 11 平台 Minecraft Java Edition 与 7 Days to Die 的标准 Mod 桥接，让两个沙盒世界实时互动。长期愿景是把 Minecraft 建造、物品与生存体验映射到七日杀，并实现反向世界/实体交互。

当前只完成本机通信、玩家位置、坐标映射、双向被动实体代理、组件观察与调试；**没有完成方块、物品、装备、战斗或生存系统融合**，不能称为最终可玩 Demo v1.0。

早期路线中“七日杀16×16×16世界读取、方块转换、战斗、血月融合”等是远期目标。后续实际开发改为逐步建立 Entity System，不能因阶段编号接近而把这些原计划误判为已实现。

## 2. 当前整体架构

```text
Minecraft Fabric Client Mod
  PlayerPositionSampler / PlayerProxyTracker       -- v1 / player_position -->
  BridgeClient (Java HTTP WebSocket)
                      JSON / ws://localhost:18771/ws
Bridge Server (C# ASP.NET Core / net10.0)
  原协议校验与 Authority
  CoordinateMapper
  EntityRegistry(v1) / NativeEntityRegistry(v2)
  HealthComponents(Health + Identity 观察状态)
  PresentationComponent(独立合并与存储)
  EntityInspector(只读观察镜像)
                      JSON / 同一 WebSocket
7DTD Standard Mod (C# net48 / IModApi)
  BridgeClient / NativePlayerPublisher              -- v2 + components -->
  MarkerController / UnityMarkerScene / UnityPlayerProxyScene

7DTD → Minecraft：原生本地玩家 v2生命周期 + Health/Identity → 被动 Block Display 代理
Minecraft → 7DTD：玩家 v1生命周期、marker测试 → 被动 Unity GameObject 代理

调试入口（独立于 WebSocket 消息集）：
  POST localhost:18771/debug/inspect_entity
  GET  localhost:18771/debug/entities
  Minecraft 客户端只读命令 / 开发用世界内名称标签
```

两端游戏数据访问与对象创建/移动/删除均在各自游戏主线程；网络工作线程只收发、解析、缓存或入队。Bridge 以既有 registrationGate 串行处理实体接受、组件更新、转发和连接清理；没有新增 Authority 状态机。

## 3. 已完成阶段及核心功能

“实机通过”指历史阶段报告明确记录的真实游戏证据；“自动通过”不等于当前构建已完成实机验收。

| 阶段 | 核心功能 | 当前证据/状态 |
| --- | --- | --- |
| Phase 0 | 目录初始化、Java/.NET/Git/游戏环境检测 | 完成；environment_report.md 为历史快照 |
| Phase 1 | Fabric、Bridge、7DTD 的 JSON/WebSocket 握手、test、日志 | 开发完成；phase1_architecture.md / phase1_report.md |
| Phase 1.5 | 两款真实游戏 Mod 加载与双向 test | 历史已确认通过；早期 runtime-evidence，不另假设存在同名报告 |
| Phase 2.0 | Minecraft 位置每500ms采集、Bridge转发、7DTD日志 | 实机移动、暂停恢复、Bridge重启重连通过；phase2_runtime_acceptance.md |
| Phase 2.1 | scale/offset坐标映射与自动测试 | 默认/非默认映射实机通过；phase2_1_coordinate_mapping.md / runtime_acceptance.md |
| Phase 3.0 | entity_state v1设计：身份、类型、位置、旋转、生命周期、metadata | entity_sync_protocol.md |
| Phase 3.1 | entity_state spawn/update/despawn传输、映射、7DTD日志 | phase3_1_entity_transport.md |
| Phase 3.1.5 | 增加获准的Minecraft手动验收入口，真实三事件 | 实机通过；记录在phase3_1_runtime_acceptance.md |
| Phase 3.2 | EntityRegistry、防重复spawn、已有实体update、despawn、重启清空 | 87项历史自动测试；实机含重启验证通过 |
| Phase 3.3 | 第一个marker：七日杀创建/移动/删除本地几何对象 | 自动测试和实机通过；phase3_3_marker.md / runtime_acceptance.md |
| Phase 3.4 | entity_types.json、target_mapping、capabilities、schema | 设计、自动校验、编译完成；phase3_4_entity_types.md |
| Phase 3.5.0 | minecraft:player → 7dtd:player_proxy设计与样例 | phase3_5_0_player_proxy.md |
| Phase 3.5.1 | Minecraft玩家进入/更新/退出 → 七日杀代理，位置/旋转 | 实机生成、更新、正常退出despawn和Registry清空通过；最终报告phase3_5_1_final_acceptance.md |
| Phase 3.6.0 | v2 source/authority/origin、所有权、冲突、防回传设计 | phase3_6_0_bidirectional_protocol.md |
| Phase 3.6.1 | 七日杀本地原生玩家发送v2，Bridge验证，Minecraft接收日志 | 195项历史自动测试；实机位置/yaw/pitch/正常退出通过，确认不是断线清理 |
| Phase 3.6.2 | Minecraft接收7DTD来源，在主线程显示被动代理 | 历史实机spawn/update/despawn通过；phase3_6_2_minecraft_proxy.md |
| Phase 3.7.0 | Entity Components v1、snapshot/patch/remove、权限设计 | 文档、schema、离线验证完成；phase3_7_0_entity_components.md |
| Phase 3.7.1 | Health运行时：7DTD → Bridge → Minecraft日志 | 实机100→92→93、remove、正常退出通过；phase3_7_1_health_runtime.md |
| Phase 3.7.2 | Identity运行时：name/display_name/tag.*，共享revision | 三端编译/自动测试通过，实机23项证据对照通过；phase3_7_2_identity_runtime.md |
| Phase 3.7.3 | Presentation：默认配置、字段合并、add/update/remove、两端存储日志 | 编译/自动测试完成；Entity_Presentation_System.md；不把启动日志目录视为完整实机验收 |
| Phase 3.7.4 | Debug：只读Inspector、HTTP入口、Minecraft命令、两端开发标签与日志 | 最新三端编译、94新增+216回归通过；**人工实机验收待完成** |

## 4. 当前目录结构及关键文件

```text
7-M/
├─ README.md                         # 阶段说明滞后，不能单独用于交接
├─ global.json                       # .NET SDK 10.0.400 / latestPatch
├─ build.ps1                         # 通用Release构建；与Phase374产物目录不同
├─ start-bridge.ps1                  # 启动Release DLL，不能自动代表最新Phase374
├─ minecraft-mod/
│  ├─ build.gradle / gradlew.bat / gradle/wrapper/
│  ├─ src/main/resources/fabric.mod.json
│  ├─ src/main/java/io/mc7dtd/
│  │  ├─ MinecraftBridgeMod.java / BridgeClient.java
│  │  ├─ PlayerPositionSampler.java / PlayerProxySampler.java / PlayerProxyTracker.java
│  │  ├─ NativeProxyController.java / ProxyTypeCatalog.java / MinecraftProxyScene.java
│  │  ├─ HealthReceiver.java / IdentityComponent.java / PresentationReceiver.java
│  │  ├─ EntityInspector.java / EntityDebugCommands.java / DebugConfig.java
│  │  ├─ DebugNameTagLayer.java / MinecraftDebugTags.java
│  │  └─ EntityTestCommands.java / PresentationTestCommands.java
│  ├─ src/test/java/io/mc7dtd/         # headless harness，含EntityDebugHarness
│  └─ build/libs/mc7dtd-bridge-0.1.0.jar
├─ 7dtd-mod/
│  ├─ MC7DTD.Mod.csproj / ModInfo.xml
│  ├─ src/
│  │  ├─ BridgeMod.cs / BridgeClient.cs / NativePlayerPublisher.cs
│  │  ├─ MarkerController.cs / UnityMarkerScene.cs / UnityPlayerProxyScene.cs
│  │  └─ IdentitySample.cs / EntityDebugLog.cs / DebugProxyScene.cs
│  ├─ bin/Phase374/net48/MC7DTD.Bridge.dll
│  └─ dist/MC7DTD-Bridge/             # 构建打包，Git忽略
├─ bridge-server/
│  ├─ BridgeServer.csproj / Program.cs
│  ├─ EntityTransport.cs / EntityTransportV2.cs / CoordinateMapper.cs
│  ├─ EntityRegistry.cs / NativeEntityRegistry.cs
│  ├─ HealthComponents.cs / IdentityComponent.cs / PresentationComponent.cs
│  ├─ EntityInspector.cs / InspectorEndpoints.cs
│  └─ bin/Phase374/net10.0/BridgeServer.dll
├─ components/
│  ├─ presentation/PresentationComponent.cs / presentation.schema.json
│  └─ debug/DebugConfig.cs
├─ config/
│  ├─ network.json / coordinate.json
│  ├─ entity_types.json / entity_types.schema.json
│  ├─ component_permissions.json / component_permissions.schema.json
│  ├─ identity_labels.json / identity_labels.schema.json
│  └─ debug.json / debug.schema.json
├─ docs/
│  ├─ PROJECT_HANDOFF_PHASE_3_7.md
│  ├─ Entity_Presentation_System.md / Entity_Debug_Layer.md
│  ├─ entity_state_v2.schema.json / entity_components_v1.schema.json
│  ├─ entity_debug_inspector.schema.json
│  ├─ examples/                       # v1/v2、组件、Identity、Presentation JSON
│  └─ phase*-runtime-evidence/        # 历史实机日志/截图/验证；部分启动失败或空日志
├─ tests/
│  ├─ run-phase3_7_4.ps1 / launch-phase3_7_4.ps1
│  ├─ debug-runner/                   # Inspector/HTTP/真实收发类检查
│  ├─ presentation-runner/ / identity-runner/ / health-runner/
│  ├─ native-transport-runner/ / marker-runner/ / player-proxy-runner/
│  ├─ bidirectional-protocol/ / state-components/ / entity-types/
│  └─ 其他阶段run/launch/verify脚本与runner
├─ runtime/                          # Git忽略；工程内真实游戏实例与Mod部署
│  ├─ minecraft/                     # mods、logs、world、runtime-arguments.txt
│  └─ 7dtd/Mods/MC7DTD-Bridge/        # 通过-UserDataFolder使用
├─ work/                             # Git忽略；工具缓存、测试证据、临时文件
│  ├─ phase3_7_4-test/                # 最新build/results/schema/回归日志
│  ├─ phase3_7_3-test/ / phase3_7_2-test/
│  ├─ dotnet-home/ / nuget/ / gradle-home/
│  └─ phase3_4-test/python-libs/       # 已准备的jsonschema等验证依赖
└─ logs/                             # 历史游戏日志，部分未被Git忽略
```

components/ 尚不是通用运行时组件框架：Health/Identity 实现仍在现有Bridge/Mod类中，不要为“目录统一”擅自搬动或重写它们。共享C# DTO通过csproj Link参与7DTD和多个测试项目，改动新DTO引用时必须检查全部相关runner。

## 5. 开发环境与构建版本

| 项目 | 当前锁定/历史实测 |
| --- | --- |
| 平台 | Windows 11 x64 |
| JDK | Oracle JDK21.0.12，`C:\Program Files\Java\jdk-21.0.12` |
| Minecraft | Java Edition 1.21.11 |
| Fabric Loader/API | 0.18.4 / 0.140.2+1.21.11 |
| Yarn / Fabric Loom | 1.21.11+build.2 / 1.14.10 |
| Gradle | 工程Wrapper 9.2.1，无需全局Gradle |
| Bridge | net10.0，global.json指定SDK10.0.400，允许同功能带补丁 |
| 七日杀Mod | net48 + Windows .NET Framework4.8引用程序集/游戏Mono |
| 七日杀已验收版本 | V3.2.0 (b10)，依据Phase3.7.2真实游戏启动日志 |
| 七日杀安装 | `D:\Steam\steamapps\common\7 Days To Die`，作为游戏程序集引用来源 |
| Java依赖 | 既有Gson2.13.2/SLF4J2.0.17，不增加大型依赖 |

阶段版本 Phase3.7.4 不等于JAR文件版本；Minecraft JAR仍为0.1.0。七日杀DLL名为MC7DTD.Bridge.dll，Bridge DLL名为BridgeServer.dll；不要拿错两个C#产物。

## 6. 核心通信协议

### 6.1 连接、test 与 player_position

正式地址：`ws://localhost:18771/ws`。config/network.json 当前为 host=localhost、port=18771。每个角色仅允许一个连接；握手角色绑定来源，不是由任意message字段自行授权。

```json
{"type":"minecraft_connect","client":"minecraft"}
{"type":"7dtd_connect","client":"7dtd"}
```

Bridge回复welcome，现有peer_connected触发同步边界/测试，原test消息继续双向转发。保留error/code及原错误处理，不增加ACK或自动可靠重放。网络限本机loopback，拒绝浏览器Origin；未实现TLS或账户认证，不应对外开放服务。

Minecraft旧位置采集仍保留：

```json
{"type":"player_position","source":"minecraft","x":100,"y":64,"z":200}
```

500ms一次；Bridge验证有限数值，应用映射后发七日杀。它不是EntityRegistry记录，也不能把它和entity_state混为同一生命周期。

### 6.2 entity_state v1 与 v2

共同字段：type=entity_state、version、source、stream_id、entity_id、entity_type、world_id、dimension、sequence、lifecycle、position、rotation、metadata。

**运行方向是非对称的：Minecraft目前自动发送v1；7DTD自动发送v2。** v2设计/schema含Minecraft示例不代表运行时已接受Minecraft v2；EntityTransportV2当前发送角色限定7dtd。不能在Equipment阶段随意升级Minecraft版本或改Authority以绕过限制。

v2在共同字段上增加authority及origin；完整合法的7DTD spawn（示意UUID，真实发包需要当前活动身份）：

```json
{
  "type":"entity_state","version":2,"source":"7dtd","authority":"7dtd",
  "origin":{"game":"7dtd","world_id":"td-demo","dimension":"7dtd:main","entity_id":"cd787d90-736c-4428-9c35-3b8b6c0fe4f8"},
  "stream_id":"e1396731-e7fa-412f-98ab-0b5d04d41f4d",
  "entity_id":"cd787d90-736c-4428-9c35-3b8b6c0fe4f8","entity_type":"7dtd:player",
  "world_id":"td-demo","dimension":"7dtd:main","sequence":1,
  "lifecycle":{"event":"spawn"},
  "position":{"x":120,"y":118,"z":65,"space":"7dtd"},
  "rotation":{"yaw":90,"pitch":0,"roll":0},"metadata":{},
  "components":{"presentation":{"renderer":"humanoid","model":"survivor","variant":"default","scale":1}}
}
```

事件保留spawn/update/despawn。update具有完整position/rotation；despawn的position/rotation=null、metadata={}、lifecycle.reason为既有枚举，不能附带Presentation。v1仍保留旧事件别名announce/remove的规范化兼容；不要推广到v2。

entity_id/stream_id为小写UUID；实体类型有minecraft:/7dtd:命名空间，不是任意player001字符串。sequence为正安全整数，上限9007199254740991。id在一个生命周期内固定，不是昵称，也不是两游戏原生整数ID；跨会话/重新生成不保证持久化相同ID。

rotation为共享约定的yaw/pitch/roll范围：yaw[0,360)、pitch[-90,90]、roll[-180,180)。源端适配原生旋转，CoordinateMapper只负责坐标。metadata仅允许既有受限扁平数据，不承载权限、脚本或资源执行。

### 6.3 CoordinateMapper

config/coordinate.json 当前为scale=1，三个offset=0。Minecraft→7DTD：`target = source*scale + offset`；7DTD→Minecraft：使用原v2逆映射。修改配置后重启Bridge，不能在Mod端重复映射。逆向路径需可逆比例及有限结果，不能通过改协议空间字段绕过校验。

Bridge输出的position.space对应目标游戏；Inspector读取的是已经映射的目标坐标，不是原始源坐标。正式配置与历史近距离实机配置不同，手动验收时代理可能距离较远，不能凭“没看见”直接断定网络失效。

### 6.4 entity_components v1

这是引用v2活动实体的独立sidecar；不是entity_state中的任意components通用对象。目前运行时只接入 **7dtd:player → Minecraft** 的health、name、custom_metadata。

```json
{
  "type":"entity_components","version":1,"entity_state_version":2,
  "source":"7dtd","authority":"7dtd",
  "origin":{"game":"7dtd","world_id":"td-demo","dimension":"7dtd:main","entity_id":"cd787d90-736c-4428-9c35-3b8b6c0fe4f8"},
  "entity_id":"cd787d90-736c-4428-9c35-3b8b6c0fe4f8","entity_type":"7dtd:player",
  "world_id":"td-demo","dimension":"7dtd:main","stream_id":"e1396731-e7fa-412f-98ab-0b5d04d41f4d",
  "entity_sequence":1,"revision":1,"base_revision":0,"mode":"snapshot",
  "components":{"health":{"current":100,"max":100},"name":{"text":"Survivor","display_name":"Survivor"},"custom_metadata":{"tag.source":"7dtd"}}
}
```

必须匹配活动实体的source/authority/origin/type/stream以及**当前entity_sequence**；复制历史样例的序号不能直接用于实时游戏。

- snapshot：base_revision=0，完整覆盖共享组件集合，省略的旧组件可被清掉；不能用组件null表示初始值。
- patch：base_revision精确等于已接受revision，新revision必须更大；只更新本次出现的组件，其他组件保留。
- remove：patch中对应组件=null；权限、基线同样检查。保留revision基线，直到实体despawn清理。
- **Health/Identity共用一个revision，不要各自独立递增。** patch粒度不是统一字段合并：health和name仍需完整有效对象；custom_metadata整张map替换。
- 未授权/非法包不改变状态或revision。旧包也先经过身份/权限/内容检查；旧revision随后忽略。
- 正常世界退出先排队组件remove，再despawn；不能杀进程当作生命周期验收。

Schema的animation_state是设计保留，运行时尚不支持。Presentation不是这个sidecar的已接入键；不要把presentation/equipment直接塞进HealthReceiver当前白名单。

## 7. Entity System 当前设计

### 7.1 Registry 与生命周期

- EntityKey=`(source,world_id,dimension,entity_id)`，不能只按entity_id全局覆盖。
- EntityRegistry（v1）：4096条活动记录；保存Type/Status/StreamId/Sequence/LastUpdate/映射后State；Find返回深复制。spawn防重复，update需已存在，despawn删记录，无墓碑；Sequence沿用原“记录”语义，**不新增v1单调序号拒绝规则**。
- NativeEntityRegistry（v2）：4096条记录容量含退休记录；活动计数与墓碑记录不同。绑定stream，检查类型/序号，拒绝重复/退休ID重生与过期更新；despawn后保留退休语义，直到既有连接边界清空。
- 类型/stream不匹配不更新，未知update不能生成对象，重复spawn不覆盖现有对象。
- Bridge重启从空内存开始，无持久化；对端不可用不累积离线实体事件。重连沿用原epoch/fresh spawn机制，组件需要新snapshot。

### 7.2 当前类型和实际代理

| 源entity_type | 目标映射/adapter | 当前表现 |
| --- | --- | --- |
| minecraft:marker | 7dtd:marker / marker | Unity无碰撞几何测试对象 |
| minecraft:player | 7dtd:player_proxy / player_proxy | 橙色被动几何代理，位置与旋转 |
| 7dtd:player | minecraft:player_proxy / block_display_proxy | 青色Block Display主体+金色朝向标记 |

三类型当前enabled=true，capabilities声明生成/位置/删除及玩家旋转。health/collision/ai/combat/persistence仍false；health=false指代理没有伤害/治疗应用能力，**不禁止独立health观察通道**。

只有本地原生玩家的自动采集，不含僵尸、动物、多玩家、专用服务器或通用类型工厂。docs/examples/entity_state_v2/7dtd_spawn.json中7dtd:zombie是设计样例，不是已启用自动僵尸实现。

## 8. Component列表与实际含义

| 概念 | 在线表示/实现 | 状态与边界 |
| --- | --- | --- |
| Health | sidecar.components.health={current,max}；HealthComponents/HealthReceiver | 7DTD真实采集、观察与日志；不写回Minecraft，不做战斗 |
| Identity | sidecar.components.name={text,display_name?} + custom_metadata的tag.* | 7DTD原生EntityName/PlayerDisplayName；可用identity_labels覆盖观察显示名/标签 |
| Presentation | entity_state.components.presentation={renderer,model,variant?,scale} | 类型默认值+字段合并；日志/存储，不加载正式资源 |
| Authority | v2核心source/authority/origin；Inspector映射source/owner | 不可写的所有权信息，不是新可变组件 |
| Debug Inspector | 本地只读派生视图/HTTP/命令/临时标签 | 不作为同步组件发出，不取得所有权 |
| animation_state | 原组件设计schema保留 | 未接入运行时、无播放 |
| Equipment/Inventory | 无运行时组件 | 尚未开始；不能宣称已同步 |

Health值为源游戏原生数值（七日杀常见100），不归一化成Minecraft20。health组件部分更新不意味着可只发current：旧规则要求完整current/max对象。

Identity是逻辑名称；真实sidecar键是name，字段是text，不是identity.name。Inspector转换成identity.name只为显示，不能据此改在线协议。tag.*值要求合法文本，custom_metadata仍为受限扁平map；名字改变不改变entity_id或authority。

## 9. Authority与组件权限

v2当前校验握手角色=source=authority=origin.game；origin world/dimension/entity_id必须与消息完整身份一致，类型/维度命名空间需匹配源游戏。当前7DTD源的owner固定7dtd，没有抢占、迁移、仲裁、优先级覆盖或跨来源写操作。

Minecraft v1来源由原角色/来源校验限制，仅走既有Minecraft发送路径。不能为了让新组件工作而放宽EntityTransportV2、NativeEntityRegistry或component权限检查。

component_permissions.json只显式授权7dtd:player，source=7dtd、target=minecraft，health/name/custom_metadata的publish/store均true；还要匹配已启用entity_types映射。缺失授权、false、未知组件拒绝；metadata不能自授予能力。Inspector/标签只读，不为组件增加任何publish权。

防回传：接收的镜像状态只进入本地代理/观察器，不重新发布为原生状态；NativePlayerPublisher只采集七日杀本地原生玩家，PlayerProxySampler只采集Minecraft自身玩家。

## 10. Presentation机制

- Bridge中的PresentationComponent负责验证、默认配置、Resolve/Commit存储；components/presentation/PresentationComponent.cs是共享7DTD DTO，Java PresentationReceiver保存接收状态。
- spawn先使用entity_types.presentation_default，再合并消息显式字段；缺配置使用renderer=unknown、model=default、scale=1，不必注入variant。
- update对象按字段合并，例如仅scale=1.5保留model/renderer/variant；省略组件保持不变。
- update时presentation=null删除，仅移除Presentation，Health/Identity与实体继续存在；后续对象update相当于add，在通用默认值上重新建立。
- add/remove是组件操作含义，不是新的lifecycle.event。despawn禁止Presentation字段；已有Registry接受事件后才Commit。
- renderer/model/variant为1–64位小写标识字符`a-z0-9_.:-`，不是路径/URL；scale有限且0<scale≤16，字段null/未知键/重复键拒绝。
- 当前默认model：minecraft:marker=default、minecraft:player=player、7dtd:player=survivor。
- Bridge启动加载默认配置；没有热更新或真实3D资源解析。实际代理大小仍不随scale更改。

## 11. Debug Layer机制

Bridge EntityInspector镜像已接受事件，读取时确认Registry仍活动，并深复制真实组件；不Apply生命周期、不修改组件。HTTP查询与同步共用既有gate，重复读取不改变状态。

```text
POST /debug/inspect_entity
  {"type":"inspect_entity","entity_id":"实际UUID","source":"7dtd","world_id":"...","dimension":"7dtd:main"}
GET /debug/entities
```

单实体200；无实体404；同id多个范围且未限定409；非法请求400；非loopback/带Origin403。**不要把inspect_entity发送到/ws：它是独立HTTP调试API，WebSocket原消息集未扩展。** `/health`仍只是连接健康状态，不是实体Health组件，也不是版本鉴定接口。

Minecraft命令始终注册：`/mc7dtd_entity_inspect`、`/mc7dtd_debug_entities`。它们读取本地接收实体视图，输出到现有聊天与日志，不创建新GUI；跨两端完整列表用Bridge HTTP。

config/debug.json目前两开关true，启动时读取：debug_name_tag控制临时名称标签，debug_logging控制新增自动Debug日志。false后需重启三端；原Health/Identity/Presentation日志继续保留，用户显式查看命令仍可输出结果。

Minecraft标签为临时Text Display：`[UUID] / model / HP:current`；7DTD标签为TextMesh `MC_UUID`，跟随浮动原点。主线程创建/刷新/删除，Health变化可独立刷新标签，despawn/世界切换/重连一起清理。字体/标签失败退回既有代理，不替换玩家HUD或正式模型。

7DTD主线程代理生成后输出Entity Debug Spawn Proxy；scale变化输出旧值→新值。空组件为{}或HP:unknown，不能把缺失值当0。Inspector的identity/authority是派生字段，不是新增同步协议。

## 12. 最新编译与测试状态

最近一次完整自动验证：2026-10-04约08:36–08:37（文件本机时区）。命令为`tests/run-phase3_7_4.ps1`，最终退出码0。交接本身不重新运行构建/游戏测试。

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

Bridge/7DTD均0错误、0警告；Fabric BUILD SUCCESSFUL。最新产物见目录树的Phase374目录，Minecraft JAR仍0.1.0。

| 最新检查 | 数量 | 保存证据（work/phase3_7_4-test/） |
| --- | --- | --- |
| C# Debug/Inspector/HTTP/实际收发类 | 42 | debug.log、results.json、inspector-snapshot.json |
| Java Debug Inspector/标签适配器 | 14 | EntityDebugHarness.log |
| debug schema/保护文件哈希/原WebSocket比较 | 38 | schema.log |
| 新增合计 | **94** | 三组全部通过 |
| Presentation C#/Java回归 | 42+10 | presentation-runner-regression.log、PresentationHarness.log |
| Identity/Health Java回归 | 23+20 | IdentityHarness.log、HealthHarness.log |
| NativeProxy Java回归 | 29 | NativeProxyHarness.log |
| Identity/Health C#及真实WebSocket回归 | 51 | identity-runner-regression.log |
| 原组件契约回归 | 41 | identity-schema-regression-stderr.log |
| 回归合计 | **216** | 另验证7条原组件实际序列化消息 |

合计310项不同类别的验证检查通过，另7条序列化消息校验；这是最新套件统计，不是全部历史测试数量相加，也不是310次实机操作。24份受保护核心文件哈希未变，原WebSocket实现除只读Observe/Clear挂接外保持相同。

### 12.1 自动测试重新运行

```powershell
Set-Location 'D:\wenjian\minecraft\7-M'
.\tests\run-phase3_7_4.ps1
```

脚本设置工程内DOTNET_CLI_HOME、NUGET_PACKAGES、GRADLE_USER_HOME、TEMP/TMP、PYTHONPATH及JDK21，测试使用独立随机端口，不改正式18771。不要把TEMP恢复到长宿主路径后直接运行Java网络runner；历史曾因Windows网络Selector临时路径失败而超时。

Presentation/Identity runner支持MC7DTD_TEST_BRIDGE指定最新DLL，原默认路径保留。部分旧runner仍输出到work/phase3_7_3-test或phase3_7_2-test，不要误以为全部测试证据都在最新目录。某些旧设计测试含“生产文件未变化”的阶段冻结断言，不能无差别重跑旧阶段并把合法的新阶段扩展判为失败；以当前套件和边界回归为准。

### 12.2 已验证与尚待验证

历史实机已验证通信、位置移动/暂停/重连、非默认坐标、marker生成移动删除、Minecraft玩家代理位置与旋转、两向正常退出despawn、7DTD原生Health/Identity变化及组件清理。

最新自动测试已验证Presentation初始/字段合并/remove/兼容，两向实际C#/Java收发类传输，Inspector深复制只读、多实体/重复id、HTTP状态码、配置关闭、标签适配器生命周期与Health-only刷新、既有代理/Health/Identity回归。

**尚未有完整Phase3.7.4真实游戏验收结论**：字体与名称标签可见性、游戏内命令实际反馈、关闭配置后的真实显示、最新版本生命周期都需用户手动验证。已有phase3_7_3/phase3_7_4-runtime-evidence目录不等于通过。

## 13. 交接现场与已知问题

交接时只读核对发现：

- localhost:18771的IPv4/IPv6监听者是PID **16524**，命令行指向 `bridge-server/bin/Phase373/net10.0/BridgeServer.dll config/network.json`，不是最新Phase374。
- 2026-10-04 08:40:05，phase3_7_4-runtime-evidence/bridge-stdout.log与bridge-stderr.log显示 **address already in use / SocketException10048**，新Bridge未启动成功。
- 对应Phase3.7.4 Minecraft stdout、7dtd-game.log均为空，不能据此标记真实游戏验收通过。交接时进程枚举未发现java.exe/7DaysToDie.exe运行。
- Phase3.7.3启动目录也有同类端口绑定失败记录；有游戏输出只能说明曾启动过，不能保证它们连接的是该轮最新Bridge。

以上PID和进程状态是2026-10-04约08:54的瞬时快照，新对话必须重新核对，**不要照抄PID直接杀进程**。本次没有结束进程或修复端口；应由用户正常停止已确认的旧Bridge，再启动最新Phase374，不更换端口规避问题。/health返回phase=1是旧通用字段，不能用来识别实际二进制阶段。

## 14. 用户手动验收步骤

先正常关闭旧游戏和已确认的旧Bridge，Steam保持登录。用户在工程根目录依次执行：

```powershell
.\tests\launch-phase3_7_4.ps1 bridge
.\tests\launch-phase3_7_4.ps1 minecraft
.\tests\launch-phase3_7_4.ps1 7dtd
```

启动脚本将JAR/DLL部署到工程runtime而非原游戏安装。先检查Bridge没有bind失败，再进入两个世界。Minecraft执行：

```text
/mc7dtd_entity_inspect
/mc7dtd_debug_entities
```

核对真实7DTD实体Identity/Health/Presentation/Authority，标签模型与HP；七日杀观察MC_UUID标签及Spawn Proxy。可使用原验收入口`/mc7dtd_presentation_test spawn/update/remove/add/despawn`检查scale1→1.5及清理，**每条都需完整命令前缀**。

Health变化使用七日杀原生玩法，不通过桥接写血量。正常退出世界检查despawn、代理与标签删除，不能杀进程替代。配置两项false后重启验证关闭；恢复配置也需重启。使用Bridge HTTP全局列表核对空状态/作用域。

原验收发送命令需要MC7DTD_ENTITY_TEST=1；新launch脚本已设置。Debug查看命令无需该开关。MC7DTD_ROOT指向工程根目录。

日志保存于docs/phase3_7_4-runtime-evidence与runtime/minecraft/logs/latest.log；启动器覆盖同名文件，复验前在工程内备份需要保留的证据。人工确认后记录新报告，不覆盖历史“自动通过”证据冒充实机。

## 15. 未实现功能与真实限制

未实现Equipment/Inventory/物品映射、正式模型/skin/渲染替换、动画播放、AI/NPC控制、战斗/伤害同步、方块放置转换、世界区域读取、红石/TNT/合成融合、天气/温度/血月互通、最终Demo1.0。

也未完成多人/专用服务器、所有实体类型采集、多客户端路由、真实维度对应/地形对齐、持久化、所有权转移、可靠ACK/重放、运行时能力协商及通用配置热加载。当前采集的是本地玩家，代理是显示对象，不是可控真人或活体NPC。

Health/Identity运行方向只为7DTD→Minecraft观察，Minecraft玩家仍v1；不能宣称完整双向组件应用。配置模型标识不代表真实模型已加载。代码已编译不代表Mod已部署或当前进程用最新DLL。

## 16. 下一阶段Phase3.8 Equipment System建议（尚未授权执行）

建议先开展Phase3.8.0设计，再分步骤接入，避免把装备、背包、模型渲染和战斗一次混进现有系统。

1. **先确定边界：** 建议第一版7DTD→Minecraft装备观察与日志/Inspector，暂不写回游戏、不做战斗、不渲染装备、不同步完整inventory。
2. **定义独立Equipment Component契约：** 明确slot、来源命名空间item标识、空slot、组件整体remove、snapshot及slot partial update。示例与schema先行，字段缺省/未知slot/空对象/删除规则必须清楚。
3. **保留原身份与Authority：** 使用既有entity_id/stream/原origin与权限；装备不产生新entity、不更改owner、不改变CoordinateMapper。
4. **先设计扩展入口：** 现有HealthComponents、HealthReceiver、ComponentPermissions和schema均有受支持键白名单，不是把`equipment`塞进去就能工作。应设计组合/策略式组件扩展，保持Health/Identity现有校验及State API含义；未经明确批准不重写旧逻辑。
5. **保持共享版本语义：** 若复用entity_components v1 sidecar，必须共享原revision/base_revision、引用当前entity_sequence；避免独立equipment revision与Health/Identity竞争。snapshot省略组件可能清掉其他状态，必须有测试保护。
6. **明确兼容目标：** 旧消息/旧组件行为必须保留；旧版接收器不认识equipment，不能把它与health混包后声称旧客户端仍能解析。先明确接入范围/过滤策略，不默改现有握手或Authority。
7. **权限配置：** 为批准的源类型增加独立publish/store授权；仍按原所有权判断，不从metadata授予权限，remove也需要授权。
8. **采集方式：** 使用本机游戏SDK公开API及主线程，不猜不存在的装备API、不读内存、不Hook；先检查本地游戏程序集公开类型，再编译最小观察采集。
9. **测试优先：** 缺组件旧v1/v2、初始装备、单slot更新保留其他slot、slot清空、remove不影响Health/Identity/Presentation、未授权整包拒绝、过期revision/错误stream/错误entity_sequence、despawn/重连清理、两向旧消息回归。
10. **调试扩展也需设计：** Inspector当前diagnostic schema只列四组组件；若添加equipment观察，明确可选扩展并保持旧输出兼容，不把Inspector变成装备写接口。

下一阶段不应顺带实现正式装备模型、animation、inventory、combat、所有权迁移或方块功能。先完成当前人工验收，再等待用户对Phase3.8具体任务的授权。

## 17. 持续开发约束

- 所有源码、配置、文档、测试、构建及临时核心文件继续保存在D:\wenjian\minecraft\7-M；不得改其他已有项目。
- 不破坏旧entity_state v1/v2、player_position、test、Health/Identity/Presentation语义及连接逻辑；新组件以明确的可选扩展接入，不擅自更改根协议。
- **不修改Authority逻辑**；保持原source/owner/origin、角色绑定、状态冲突、权限和防回传边界。
- 不为扩展功能改EntityRegistry核心生命周期、CoordinateMapper或18771端口。
- 保持Component扩展模式；区分Health/Identity共享sidecar与Presentation inline字段合并，不把所有patch规则统一化。
- 自动测试优先；使用当前run-phase3_7_4基线，改动后针对变更测试并回归受影响模块，避免无目的反复全套重跑。
- **禁止computer-use自动验收**；只提供用户手动启动/命令/移动/退出步骤，未取得实机证据就明确写待验收。
- 禁止DLL注入、内存修改、Cheat Engine、进程Hook、破解；7DTD DLL必须通过正常IModApi加载，不使用Harmony补丁。
- 保持主线程游戏API访问、异步网络、有界队列和已有重连行为，不通过同步网络阻塞游戏线程。
- 不引入大型第三方依赖，不用正式GUI/HUD替换做调试，不扩大为AI/战斗/装备渲染。
- 每阶段报告文件清单、功能、编译/测试、限制、下一步；完成后停止等待用户，不自动进入下一阶段。
- 保留现有未提交修改；不擅自git reset/clean、删除日志、丢弃未跟踪源码、commit或push。本次交接未操作这些事项。

## 18. Git状态与修改文件统计（交接时快照）

统计时间：2026-10-04T09:03:26+08:00。仓库根目录：D:\wenjian\minecraft\7-M。分支：`main`。

- HEAD：`69d9662aa14386b1a83c1f6e03d08d397e6b7d94`，提交说明：`Complete Phase 3.5.1 player proxy entity`（最后已提交阶段为Phase3.5.1）。
- origin：`https://github.com/roulqin/Minecraft-7DTD-Bridge.git`。
- 相对**本地已保存**origin/main引用：ahead=1，behind=0；未fetch，不能据此判断远程服务器此刻状态。
- 已跟踪变化文件：**27**；其中已暂存 **0**，未暂存 **27**。
- 未跟踪文件：**220**（用`--untracked-files=all`展开目录逐文件统计）。
- Git显示变化文件合计：**247**，包含本交接文档1个；冲突文件：**0**。
- 已跟踪文件相对HEAD的diff：增加 **13943** 行、删除 **1324** 行，包含运行日志；不含未跟踪文件的新增行。
- 排除logs/及docs运行证据后，已跟踪变更为19个文件、+481 / -70行。此数仍是累计相对HEAD，不是Phase3.7.4单阶段统计。

这些变化包含历史阶段未提交实现、文档、运行证据和部分生成文件，不能当作本次交接引入的代码变化。本次新建的项目文档只有`docs/PROJECT_HANDOFF_PHASE_3_7.md`；核对辅助工具/机器统计在被Git忽略的work/内。runtime/、work/、bin/obj/build/.gradle及7dtd-mod/dist均不计入Git变化文件数。

| 目录 | 已跟踪变化 | 未跟踪文件 | 合计 |
| --- | ---: | ---: | ---: |
| (根目录文件) | 1 | 0 | 1 |
| 7dtd-mod | 4 | 4 | 8 |
| bridge-server | 2 | 7 | 9 |
| components | 0 | 3 | 3 |
| config | 2 | 6 | 8 |
| docs | 8 | 144 | 152 |
| logs | 1 | 2 | 3 |
| minecraft-mod | 2 | 19 | 21 |
| tests | 7 | 35 | 42 |

重要：Phase3.6及之后大量运行时代码、schema、测试和证据尚未进入Git提交。仅切到HEAD或重新clone远程会缺少这些功能；保留完整当前工程目录和忽略的runtime/work所需依赖/验收证据。不要未经请求丢弃、清理、提交或推送。

完整Git状态（展开未跟踪目录；` M`表示工作区修改，`??`表示未跟踪）：

```text
 M 7dtd-mod/MC7DTD.Mod.csproj
 M 7dtd-mod/src/BridgeClient.cs
 M 7dtd-mod/src/BridgeMod.cs
 M 7dtd-mod/src/MarkerController.cs
 M README.md
 M bridge-server/BridgeServer.csproj
 M bridge-server/Program.cs
 M config/entity_types.json
 M config/entity_types.schema.json
 M docs/entity_sync_protocol.md
 M docs/runtime-evidence/7dtd-game.log
 M docs/runtime-evidence/7dtd-process.json
 M docs/runtime-evidence/7dtd-stdout.log
 M docs/runtime-evidence/bridge-stdout.log
 M docs/runtime-evidence/minecraft-process.json
 M docs/runtime-evidence/minecraft-stderr.log
 M docs/runtime-evidence/minecraft-stdout.log
 M logs/latest.log
 M minecraft-mod/src/main/java/io/mc7dtd/BridgeClient.java
 M minecraft-mod/src/main/java/io/mc7dtd/MinecraftBridgeMod.java
 M tests/client-harness/ClientHarness.csproj
 M tests/entity-runner/Program.cs
 M tests/entity-types/validate_catalog.py
 M tests/marker-runner/MarkerRunner.csproj
 M tests/marker-runner/Program.cs
 M tests/player-proxy-runner/PlayerProxyRunner.csproj
 M tests/player-proxy-runner/Program.cs
?? 7dtd-mod/src/DebugProxyScene.cs
?? 7dtd-mod/src/EntityDebugLog.cs
?? 7dtd-mod/src/IdentitySample.cs
?? 7dtd-mod/src/NativePlayerPublisher.cs
?? bridge-server/EntityInspector.cs
?? bridge-server/EntityTransportV2.cs
?? bridge-server/HealthComponents.cs
?? bridge-server/IdentityComponent.cs
?? bridge-server/InspectorEndpoints.cs
?? bridge-server/NativeEntityRegistry.cs
?? bridge-server/PresentationComponent.cs
?? components/debug/DebugConfig.cs
?? components/presentation/PresentationComponent.cs
?? components/presentation/presentation.schema.json
?? config/component_permissions.json
?? config/component_permissions.schema.json
?? config/debug.json
?? config/debug.schema.json
?? config/identity_labels.json
?? config/identity_labels.schema.json
?? docs/Entity_Debug_Layer.md
?? docs/Entity_Presentation_System.md
?? docs/PROJECT_HANDOFF_PHASE_3_7.md
?? docs/entity_components_v1.schema.json
?? docs/entity_debug_inspector.schema.json
?? docs/entity_state_v2.schema.json
?? docs/examples/entity_components/7dtd_patch.json
?? docs/examples/entity_components/7dtd_remove_name.json
?? docs/examples/entity_components/7dtd_replace.json
?? docs/examples/entity_components/7dtd_snapshot.json
?? docs/examples/entity_components/clear_snapshot.json
?? docs/examples/entity_components/minecraft_patch.json
?? docs/examples/entity_components/minecraft_remove_name.json
?? docs/examples/entity_components/minecraft_replace.json
?? docs/examples/entity_components/minecraft_snapshot.json
?? docs/examples/entity_state_v2/7dtd_despawn.json
?? docs/examples/entity_state_v2/7dtd_spawn.json
?? docs/examples/entity_state_v2/7dtd_update.json
?? docs/examples/entity_state_v2/minecraft_despawn.json
?? docs/examples/entity_state_v2/minecraft_spawn.json
?? docs/examples/entity_state_v2/minecraft_update.json
?? docs/examples/identity_components/initial.json
?? docs/examples/identity_components/remove.json
?? docs/examples/identity_components/update.json
?? docs/examples/presentation/add.json
?? docs/examples/presentation/despawn.json
?? docs/examples/presentation/remove.json
?? docs/examples/presentation/spawn.json
?? docs/examples/presentation/update.json
?? docs/phase3_6_0_bidirectional_protocol.md
?? docs/phase3_6_1-runtime-evidence/7dtd-after-world-exit.png
?? docs/phase3_6_1-runtime-evidence/7dtd-before-move.png
?? docs/phase3_6_1-runtime-evidence/7dtd-game.log
?? docs/phase3_6_1-runtime-evidence/artifact-hashes.json
?? docs/phase3_6_1-runtime-evidence/before-movement.json
?? docs/phase3_6_1-runtime-evidence/bridge-before.log
?? docs/phase3_6_1-runtime-evidence/bridge-entity-extract.log
?? docs/phase3_6_1-runtime-evidence/bridge-pid.txt
?? docs/phase3_6_1-runtime-evidence/bridge-stderr.log
?? docs/phase3_6_1-runtime-evidence/bridge.log
?? docs/phase3_6_1-runtime-evidence/entity-events.json
?? docs/phase3_6_1-runtime-evidence/health-after-exit.json
?? docs/phase3_6_1-runtime-evidence/health-after-settle.json
?? docs/phase3_6_1-runtime-evidence/health-before-exit.json
?? docs/phase3_6_1-runtime-evidence/health-in-world.json
?? docs/phase3_6_1-runtime-evidence/minecraft-latest.log
?? docs/phase3_6_1-runtime-evidence/movement-rotation.json
?? docs/phase3_6_1-runtime-evidence/processes-after-exit.json
?? docs/phase3_6_1-runtime-evidence/processes.json
?? docs/phase3_6_1-runtime-evidence/source-before.json
?? docs/phase3_6_1-runtime-evidence/verification.json
?? docs/phase3_6_1_7dtd_entity_transport.md
?? docs/phase3_6_1_runtime_acceptance.md
?? docs/phase3_6_2-runtime-evidence/7dtd-game.log
?? docs/phase3_6_2-runtime-evidence/alignment.json
?? docs/phase3_6_2-runtime-evidence/artifact-hashes.json
?? docs/phase3_6_2-runtime-evidence/before-move.json
?? docs/phase3_6_2-runtime-evidence/bridge-pid.txt
?? docs/phase3_6_2-runtime-evidence/bridge-stderr.log
?? docs/phase3_6_2-runtime-evidence/bridge.log
?? docs/phase3_6_2-runtime-evidence/coordinate.json
?? docs/phase3_6_2-runtime-evidence/entity-events.json
?? docs/phase3_6_2-runtime-evidence/health-after-despawn.json
?? docs/phase3_6_2-runtime-evidence/health-after-settle.json
?? docs/phase3_6_2-runtime-evidence/health-before-despawn.json
?? docs/phase3_6_2-runtime-evidence/minecraft-despawn.png
?? docs/phase3_6_2-runtime-evidence/minecraft-latest-raw.log
?? docs/phase3_6_2-runtime-evidence/minecraft-latest.log
?? docs/phase3_6_2-runtime-evidence/minecraft-spawn.png
?? docs/phase3_6_2-runtime-evidence/minecraft-update.png
?? docs/phase3_6_2-runtime-evidence/movement-rotation.json
?? docs/phase3_6_2-runtime-evidence/network.json
?? docs/phase3_6_2-runtime-evidence/preserved-source.json
?? docs/phase3_6_2-runtime-evidence/proxy-events.log
?? docs/phase3_6_2-runtime-evidence/verification.json
?? docs/phase3_6_2_minecraft_proxy.md
?? docs/phase3_7_0_entity_components.md
?? docs/phase3_7_1-runtime-evidence/7dtd-game.log
?? docs/phase3_7_1-runtime-evidence/7dtd-health-initial.png
?? docs/phase3_7_1-runtime-evidence/7dtd-health-update.png
?? docs/phase3_7_1-runtime-evidence/7dtd-process.json
?? docs/phase3_7_1-runtime-evidence/7dtd-stderr.log
?? docs/phase3_7_1-runtime-evidence/7dtd-stdout.log
?? docs/phase3_7_1-runtime-evidence/7dtd-world-exited.png
?? docs/phase3_7_1-runtime-evidence/artifact-hashes.json
?? docs/phase3_7_1-runtime-evidence/bridge-process.json
?? docs/phase3_7_1-runtime-evidence/bridge-stderr.log
?? docs/phase3_7_1-runtime-evidence/bridge-stdout.log
?? docs/phase3_7_1-runtime-evidence/connections-after-exit.json
?? docs/phase3_7_1-runtime-evidence/connections-before-exit.json
?? docs/phase3_7_1-runtime-evidence/coordinate.json
?? docs/phase3_7_1-runtime-evidence/health-events.json
?? docs/phase3_7_1-runtime-evidence/minecraft-latest.log
?? docs/phase3_7_1-runtime-evidence/minecraft-process.json
?? docs/phase3_7_1-runtime-evidence/minecraft-stderr.log
?? docs/phase3_7_1-runtime-evidence/minecraft-stdout.log
?? docs/phase3_7_1-runtime-evidence/network.json
?? docs/phase3_7_1-runtime-evidence/verification.json
?? docs/phase3_7_1_health_runtime.md
?? docs/phase3_7_2-runtime-evidence/7dtd-game.log
?? docs/phase3_7_2-runtime-evidence/7dtd-identity-world.png
?? docs/phase3_7_2-runtime-evidence/7dtd-process.json
?? docs/phase3_7_2-runtime-evidence/7dtd-stderr.log
?? docs/phase3_7_2-runtime-evidence/7dtd-stdout.log
?? docs/phase3_7_2-runtime-evidence/7dtd-world-exited.png
?? docs/phase3_7_2-runtime-evidence/artifact-hashes.json
?? docs/phase3_7_2-runtime-evidence/bridge-process.json
?? docs/phase3_7_2-runtime-evidence/bridge-stderr.log
?? docs/phase3_7_2-runtime-evidence/bridge-stdout.log
?? docs/phase3_7_2-runtime-evidence/component-events.json
?? docs/phase3_7_2-runtime-evidence/connections-after-exit.json
?? docs/phase3_7_2-runtime-evidence/connections-after-remove.json
?? docs/phase3_7_2-runtime-evidence/connections-before-remove.json
?? docs/phase3_7_2-runtime-evidence/coordinate.json
?? docs/phase3_7_2-runtime-evidence/identity-labels-original.json
?? docs/phase3_7_2-runtime-evidence/identity-labels-remove.json
?? docs/phase3_7_2-runtime-evidence/identity-labels-update.json
?? docs/phase3_7_2-runtime-evidence/minecraft-latest.log
?? docs/phase3_7_2-runtime-evidence/minecraft-process.json
?? docs/phase3_7_2-runtime-evidence/minecraft-stderr.log
?? docs/phase3_7_2-runtime-evidence/minecraft-stdout.log
?? docs/phase3_7_2-runtime-evidence/network.json
?? docs/phase3_7_2-runtime-evidence/verification.json
?? docs/phase3_7_2_identity_runtime.md
?? docs/phase3_7_3-runtime-evidence/7dtd-game.log
?? docs/phase3_7_3-runtime-evidence/7dtd-process.json
?? docs/phase3_7_3-runtime-evidence/7dtd-stderr.log
?? docs/phase3_7_3-runtime-evidence/7dtd-stdout.log
?? docs/phase3_7_3-runtime-evidence/bridge-process.json
?? docs/phase3_7_3-runtime-evidence/bridge-stderr.log
?? docs/phase3_7_3-runtime-evidence/bridge-stdout.log
?? docs/phase3_7_3-runtime-evidence/minecraft-process.json
?? docs/phase3_7_3-runtime-evidence/minecraft-stderr.log
?? docs/phase3_7_3-runtime-evidence/minecraft-stdout.log
?? docs/phase3_7_4-runtime-evidence/7dtd-game.log
?? docs/phase3_7_4-runtime-evidence/7dtd-process.json
?? docs/phase3_7_4-runtime-evidence/7dtd-stderr.log
?? docs/phase3_7_4-runtime-evidence/7dtd-stdout.log
?? docs/phase3_7_4-runtime-evidence/bridge-process.json
?? docs/phase3_7_4-runtime-evidence/bridge-stderr.log
?? docs/phase3_7_4-runtime-evidence/bridge-stdout.log
?? docs/phase3_7_4-runtime-evidence/minecraft-process.json
?? docs/phase3_7_4-runtime-evidence/minecraft-stderr.log
?? docs/phase3_7_4-runtime-evidence/minecraft-stdout.log
?? logs/2026-10-03-2.log.gz
?? logs/2026-10-04-1.log.gz
?? minecraft-mod/src/main/java/io/mc7dtd/DebugConfig.java
?? minecraft-mod/src/main/java/io/mc7dtd/DebugNameTagLayer.java
?? minecraft-mod/src/main/java/io/mc7dtd/EntityDebugCommands.java
?? minecraft-mod/src/main/java/io/mc7dtd/EntityInspector.java
?? minecraft-mod/src/main/java/io/mc7dtd/HealthReceiver.java
?? minecraft-mod/src/main/java/io/mc7dtd/IdentityComponent.java
?? minecraft-mod/src/main/java/io/mc7dtd/MinecraftDebugTags.java
?? minecraft-mod/src/main/java/io/mc7dtd/MinecraftProxyScene.java
?? minecraft-mod/src/main/java/io/mc7dtd/NativeProxyController.java
?? minecraft-mod/src/main/java/io/mc7dtd/PresentationReceiver.java
?? minecraft-mod/src/main/java/io/mc7dtd/PresentationTestCommands.java
?? minecraft-mod/src/main/java/io/mc7dtd/ProxyTypeCatalog.java
?? minecraft-mod/src/test/java/io/mc7dtd/EntityDebugHarness.java
?? minecraft-mod/src/test/java/io/mc7dtd/HealthHarness.java
?? minecraft-mod/src/test/java/io/mc7dtd/HealthReceiverHarness.java
?? minecraft-mod/src/test/java/io/mc7dtd/IdentityHarness.java
?? minecraft-mod/src/test/java/io/mc7dtd/NativeProxyHarness.java
?? minecraft-mod/src/test/java/io/mc7dtd/NativeProxyReceiverHarness.java
?? minecraft-mod/src/test/java/io/mc7dtd/PresentationHarness.java
?? tests/bidirectional-protocol/__pycache__/contract.cpython-311.pyc
?? tests/bidirectional-protocol/contract.py
?? tests/bidirectional-protocol/test_contract.py
?? tests/debug-runner/DebugRunner.csproj
?? tests/debug-runner/Program.cs
?? tests/debug-runner/validate.py
?? tests/entity-types/__pycache__/validate_catalog.cpython-311.pyc
?? tests/health-runner/HealthRunner.csproj
?? tests/health-runner/Program.cs
?? tests/health-runner/validate_wire.py
?? tests/identity-runner/IdentityRunner.csproj
?? tests/identity-runner/Program.cs
?? tests/identity-runner/validate_wire.py
?? tests/launch-phase3_7_1.ps1
?? tests/launch-phase3_7_2.ps1
?? tests/launch-phase3_7_3.ps1
?? tests/launch-phase3_7_4.ps1
?? tests/native-transport-runner/NativeTransportRunner.csproj
?? tests/native-transport-runner/Program.cs
?? tests/native-transport-runner/validate_sender.py
?? tests/presentation-runner/PresentationRunner.csproj
?? tests/presentation-runner/Program.cs
?? tests/presentation-runner/validate.py
?? tests/run-phase3_6_0.ps1
?? tests/run-phase3_6_1.ps1
?? tests/run-phase3_6_2.ps1
?? tests/run-phase3_7_0.ps1
?? tests/run-phase3_7_1.ps1
?? tests/run-phase3_7_2.ps1
?? tests/run-phase3_7_3.ps1
?? tests/run-phase3_7_4.ps1
?? tests/state-components/component_contract.py
?? tests/state-components/test_components.py
?? tests/verify-phase3_7_1-runtime.py
?? tests/verify-phase3_7_2-runtime.py
```


## 19. 新对话建议阅读顺序

1. 本文与Entity_Debug_Layer.md：版本、当前端口占用、人工待验收、只读边界。
2. Entity_Presentation_System.md、phase3_7_2_identity_runtime.md、phase3_7_1_health_runtime.md：组件实际wire及语义差异。
3. docs/entity_state_v2.schema.json、entity_components_v1.schema.json，config/component_permissions.json、entity_types.json：契约与授权。
4. Bridge Program、EntityTransport/V2、两个Registry、HealthComponents、PresentationComponent：数据验证/应用顺序，不轻率改动。
5. NativePlayerPublisher/IdentitySample、Java BridgeClient/HealthReceiver/PresentationReceiver：真实源采集与目标观察。
6. tests/run-phase3_7_4.ps1及最新work结果：复现命令、受保护文件、兼容性基线。
7. 用户完成Phase3.7.4手动验收并明确授权后，再进入Equipment设计。

当前工程具备：**Entity + Identity + Health + Presentation + Authority + Debug Inspector**。这是被动代理与组件观察基础，不是已完成装备/战斗/最终融合游戏。

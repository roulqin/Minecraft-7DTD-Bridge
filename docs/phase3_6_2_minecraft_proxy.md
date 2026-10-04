# Phase 3.6.2：Minecraft 代理实体接收与实机验收

日期：2026-10-03（Asia/Shanghai）。工程根目录：`D:\wenjian\minecraft\7-M`。

**本阶段完成。** Minecraft 接收 source=7dtd 的 v2 spawn/update/despawn，按已启用类型映射在本地客户端世界创建、更新、删除静态显示代理。自动测试、三端编译与真实双游戏画面验收通过，完成后停止。

## 架构与既有模块

```text
7DTD 原生玩家 → 现有 entity_state v2 → Bridge 既有 NativeEntityRegistry
  → 现有 CoordinateMapper 的逆公式 → Minecraft BridgeClient
  → 有界队列 → END_CLIENT_TICK → NativeProxyController → MinecraftProxyScene
```

Bridge 的 v1 EntityRegistry、v2 NativeEntityRegistry、EntityTransport、EntityTransportV2、CoordinateMapper 和 7DTD Mod 均未改动；它们继续决定权威状态及坐标转换。Minecraft 的 records 只是已批准源状态与本地显示对象的索引，不是另一套跨游戏权威服务。

Minecraft 网络线程只接收/验证来源并入队，不直接改 ClientWorld。客户端主线程按 spawn/update/despawn 应用对象变化。收到已映射的 position.space=minecraft 后不再次换算。origin 与 authority 保留，代理无发送回调；原 PlayerProxySampler 仍只采集 Minecraft 本地真实玩家，不把 Block Display 导出为原生实体。

v1 的 Minecraft → 7DTD 玩家/marker、player_position、握手、test 和固定网络端口保持兼容。entity_state v2 Schema 未改。

## 类型配置

config/entity_types.json 保留原两条 Minecraft 来源定义，新增启用的：

```json
{
  "entity_type": "7dtd:player",
  "enabled": true,
  "target_mapping": {
    "game": "minecraft",
    "entity_type": "minecraft:player_proxy",
    "adapter": "block_display_proxy"
  },
  "capabilities": {
    "spawn": true,
    "update_position": true,
    "despawn": true,
    "update_rotation": true,
    "health": false,
    "collision": false,
    "ai": false,
    "combat": false,
    "persistence": false
  }
}
```

Minecraft 启动时加载 ProxyTypeCatalog；未启用、未知类型、未知目标或未实现 adapter 不创建代理。危险/未实现能力在此 adapter 上拒绝启用。配置不热加载。离线 validate_catalog 同步登记新 adapter，既有 Schema 无需扩展。

## 显示和生命周期

每个逻辑代理包含两个原生 Block Display：青色主体 0.6×1.8×0.3，以及金色朝向标记。目标 alias minecraft:player_proxy 并非玩家/NPC 类型；实际底层为客户端 BlockDisplayEntity。对象不发送到服务器、不保存到世界、不创建 EntityPlayer、MobEntity、控制器或动画器。

显示尺寸用于渲染裁剪，不是碰撞范围。Block Display 不作为活体实体；无重力、网络插值时长=0，直接应用当前位置及 yaw/pitch/roll 仿射旋转，无走路/攻击/骨骼动画。坐标未按 Minecraft 地形调整，代理可能悬空或穿过地形，这是纯显示层的边界。

- spawn：创建唯一逻辑代理；重复 spawn 不创建第二份。
- update：更新已有对象的坐标和旋转；旧序号、身份/stream 变化、无 spawn 的 update 拒绝或忽略。
- despawn：主线程从对应 ClientWorld 移除两个对象，保留本轮退休记录防旧消息复活。
- Minecraft 无世界：保留最近已批准快照，不创建对象；进入世界后创建。
- Minecraft 世界切换：删除旧世界显示对象，按仍活动源快照重建当前世界显示，不伪造源 despawn。
- 连接 reset：清理快照、积压和显示对象，等待受控新 spawn。

队列上限 512；溢出触发连接重建，避免丢弃关键生命周期事件后继续套用 update。本地索引上限 256（含退休记录），满后记录错误，不静默驱逐退休记录。

API 依据：[Fabric Yarn BlockDisplayEntity 文档](https://maven.fabricmc.net/docs/yarn-1.21.11%2Bbuild.1/net/minecraft/entity/decoration/DisplayEntity.BlockDisplayEntity.html)。最终使用本机 Yarn 1.21.11+build.2 及 Fabric API 的实际编译接口，并经真实游戏加载验证。

## 修改文件列表

新增：

- minecraft-mod/src/main/java/io/mc7dtd/ProxyTypeCatalog.java
- minecraft-mod/src/main/java/io/mc7dtd/NativeProxyController.java
- minecraft-mod/src/main/java/io/mc7dtd/MinecraftProxyScene.java
- minecraft-mod/src/test/java/io/mc7dtd/NativeProxyHarness.java
- minecraft-mod/src/test/java/io/mc7dtd/NativeProxyReceiverHarness.java
- tests/run-phase3_6_2.ps1
- docs/phase3_6_2_minecraft_proxy.md
- docs/phase3_6_2-runtime-evidence/（独立验收配置、JSON 对照、日志、截图）

修改：

- minecraft-mod/src/main/java/io/mc7dtd/BridgeClient.java：保持原构造方法，新增接收/reset 回调，保留原日志。
- minecraft-mod/src/main/java/io/mc7dtd/MinecraftBridgeMod.java：加载类型目录，登记主线程应用与停止清理。
- config/entity_types.json：新增反向 player 映射。
- tests/entity-types/validate_catalog.py：登记反向显示 adapter。
- tests/native-transport-runner/Program.cs：支持指定 Java 代理接收器、输出目录及测试 Bridge，并增加代理回调断言。
- README.md、docs/entity_sync_protocol.md：更新当前阶段与文档入口。

构建输出在 minecraft-mod/build、bridge-server/bin/Phase362、7dtd-mod/bin/Phase362、7dtd-mod/dist、tests/*/bin/Phase362；证据和依赖仍在工程内。未修改原游戏安装或其他项目。

## 编译和自动测试

```powershell
Set-Location 'D:\wenjian\minecraft\7-M'
.\tests\run-phase3_6_2.ps1
```

| 检查 | 结果 |
| --- | --- |
| Minecraft 接收/本地投影控制 | 29 项通过 |
| 原 Minecraft 玩家采集 | 16 项通过 |
| v2 验证、真实 WebSocket 通道与实际 Java 代理回调 | 53 项通过 |
| v1 生命周期与原通信回归 | 87 项通过 |
| marker 接收/删除回归 | 24 项通过 |
| 七日杀玩家代理接收/旋转/删除回归 | 26 项通过 |
| 类型配置 Schema / adapter 校验 | 5 组通过 |
| Fabric、Bridge、7DTD 编译 | 全部成功 |

共 235 项自动检查和 5 组目录验证通过。测试覆盖同世界内显式 despawn 删除，区分世界切换/断线 reset；含禁用类型、非法能力、错误 authority、未知类型、错误坐标空间、旧序号、不可变队列样本、队列满、重连和 Bridge 重启。

证据：[work/phase3_6_2-test](../work/phase3_6_2-test/)，包含各 Harness、transport.log、目录验证、三项 v1 regression、Fabric/Bridge/7DTD 构建日志。C# 测试中的假 Scene 用于自动验证生命周期；以下实机验收另外证明了真实 ClientWorld 对象与画面。

## 真实游戏验收

Minecraft 1.21.11 / Fabric 0.18.4 / API 0.140.2+1.21.11，PID 26984；7DTD V3.2.0 B10，PID 23692；Bridge 使用 Phase362，同端口 18771。新 JAR 与 runtime 部署哈希一致；7DTD 复用已验证 Mod，Bridge 与 Registry 代码哈希保持原样。

本轮使用工程内 docs/phase3_6_2-runtime-evidence/network.json 及相邻 coordinate.json，不覆盖正式 config/network.json / coordinate.json。独立偏移用于把源玩家放到 Minecraft 玩家附近：scale=1，offsetX=-228.25407713651657，offsetY=-9.920001983642578，offsetZ=373.039901091275。

entity_id：`1d96b662-c615-4729-a431-d3c5806bac72`；stream_id：`87f4fae0-b0b2-408f-bcab-c03d8733070c`。

| 验收项 | 实际证据 | 结果 |
| --- | --- | --- |
| 七日杀进入世界 | 19:37:17，真实 source=7dtd 的 spawn 到达 Minecraft | 通过 |
| Minecraft 创建 | body=-1000000、nose=-1000001，Render thread 创建，count=1；画面青色代理可见 | 通过 |
| 玩家移动 | 映射后位置变为 x=-45.339886、y=71.000000、z=75.136246；同对象 update | 通过 |
| 玩家转向 | yaw=154.427429、pitch=2.875000；主线程应用并可见金色朝向标记 | 通过 |
| 七日杀正常退出世界 | 19:41:08，despawn sequence=460、reason=world_unloaded | 通过 |
| Minecraft 删除 | 同 body/nose removed=true，Render thread，proxy despawned/count=0；同视角画面不再有代理 | 通过 |
| 排除断线清理 | 三次 /health 均有两端；生成至删除无 reset、Bridge 无断线日志 | 通过 |

同实体源发送与 Minecraft 接收各 460 条（1 spawn、458 update、1 despawn），逐条核对映射及旋转。Bridge 的 v2 活动 count=0；仍按既有规则保留轮次墓碑。Minecraft 收到状态不回传为 v2。

初次重启时旧窗口没有立即退出，新实例短暂遇到 duplicate_client，旧窗口正常关闭后恢复。两个实例曾短暂写同一个 latest.log，产生空字节；保留 minecraft-latest-raw.log 原始快照，minecraft-latest.log 是仅移除空字节的可读副本，验证程序使用可读副本。正式本轮 Bridge 从 19:36 后重新建立干净会话，代理生成后的区间没有断线/reset。没有用此启动异常替代正常 despawn 验证。

证据目录：[phase3_6_2-runtime-evidence](phase3_6_2-runtime-evidence/)。verification.json 的 12 项运行对照全部通过；artifact-hashes.json、preserved-source.json、alignment.json、entity-events.json、movement-rotation.json、proxy-events.log、两端日志与 Bridge 日志保留。截图：minecraft-spawn.png、minecraft-update.png、minecraft-despawn.png。

## 启动和复验

编译后退出旧 Minecraft 进程，确认其已结束，再复制新 JAR 到工程 runtime/minecraft/mods；避免同时启动同角色实例。七日杀继续使用工程内 Mods。

使用正式配置：`dotnet .\bridge-server\bin\Phase362\net10.0\BridgeServer.dll .\config\network.json`。
复用本轮近距离验收配置：`dotnet .\bridge-server\bin\Phase362\net10.0\BridgeServer.dll .\docs\phase3_6_2-runtime-evidence\network.json`。两者端口相同，只启动一个 Bridge。

进入 Minecraft 世界及七日杀测试世界；查看 Minecraft 青色代理、主线程 created/count=1。移动和转向七日杀玩家，核对同一对象 updated；正常退出七日杀世界，核对 deleted/removed=true、despawned/count=0，并确认 /health 两端仍在线。

## 限制与停止点

代理是几何显示对象，不是可操作玩家、NPC 或活体实体。当前配置仅实现 7dtd:player；没有添加僵尸/动物采集或类型生成器。只在当前 Minecraft 客户端世界显示，不做维度路由、地形适配、持久化或服务器广播。roll=0 的真实源数据没有验证动态 roll，数学应用路径覆盖完整旋转。

不实现 AI、战斗、动画、装备、输入或玩家控制。真实实机已覆盖 spawn/update/despawn，重连和世界切换通过自动测试；不宣称这些附加场景也完成了真实游戏验收。

本轮结束时七日杀处于主菜单，Minecraft 仍在测试世界，Bridge 使用验收配置保持在线。正式坐标配置未改变。Phase 3.6.2 完成后停止，等待下一阶段确认。

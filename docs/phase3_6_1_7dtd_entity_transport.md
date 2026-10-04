# Phase 3.6.1：7DTD 实体发送接入报告

日期：2026-10-03。工程根目录：`D:\wenjian\minecraft\7-M`。

## 完成范围

已实现七日杀原生实体的 v2 发送接口及首个自动采集来源：当前本地原生玩家。进入世界发送 spawn，游戏主线程读取位置/旋转，每 500ms 发送完整 update；退出世界发送 despawn。Minecraft 接收后只记录完整消息，没有创建对象、调用原生实体管理或再次发送收到的状态。

```text
7DTD 本地原生玩家
  → NativePlayerPublisher → BridgeClient 有界发送队列
  → JSON/WebSocket → EntityTransportV2 → NativeEntityRegistry
  → 逆向坐标映射后的 JSON → Minecraft BridgeClient 接收日志

Minecraft 原有 v1/player_position → Bridge → 7DTD 原有接收/代理逻辑
```

端口继续读取 `config/network.json`，当前为 localhost:18771。没有修改 network.json、coordinate.json、CoordinateMapper、EntityRegistry 或 entity_state v2 Schema。原有握手、welcome、peer_connected 和 test 保留。

## 协议和所有权

- v1 继续使用原 EntityTransport / EntityRegistry，方向 Minecraft → 7DTD。
- v2 当前只接受 7DTD 连接发布，source=authority=origin.game=7dtd。
- entity_id、stream_id 必须为小写标准 UUID；根身份字段必须等于 origin 中对应值。
- entity_type、dimension 使用 7dtd 命名空间；生命周期、旋转、有限数值、metadata、字段集合、重复字段及 8192 字节上限均验证。
- Bridge 保留 source、authority、origin、entity_type、rotation，只转换位置及 position.space。
- 逆公式：Minecraft 轴坐标 = (7DTD 轴坐标 - 对应 offset) / scale；实体映射要求 scale>0，溢出拒绝。
- 结构/所有权错误沿用现有 error 消息并结束该连接；生命周期冲突记录或返回 error，不转发错误状态。

NativeEntityRegistry 是独立的 v2 状态表，所有生产访问都在 Bridge 的 registrationGate 内串行执行。第一条有效 spawn 绑定 stream；update 不建立未知身份；重复 spawn 不覆盖记录、不提高序号；旧序号、类型变化及错误 stream 不转发。despawn 保留轮次内墓碑，同轮退休身份不得复活。活动记录与墓碑共同占用 4096 条容量。

未知 despawn 只有在有效 stream 已绑定后才能记录墓碑；它不生成对象，也不向 Minecraft 转发未知删除。未绑定会话中的非 spawn 不能取得 stream 权威。

## 原生采集与防回传

仅从游戏主线程的 World.GetPrimaryPlayer() 采集 EntityPlayerLocal。既有 marker / 玩家代理是独立 Unity GameObject，不进入这一采集来源；不会按对象名称判断或导出镜像。

world_id 是游戏世界名与存档名组合的 SHA-256 摘要；entity_id 是每次观察创建的新 UUID，不使用 Steam ID 或游戏整数编号。stream_id 每次连接/受控接收端重连轮次重新创建；sequence 使用协议现有字段。

七日杀 Unity yaw 取负并归一化到 [0,360)，pitch 转为有符号角，roll 归一化到 [-180,180)。Bridge 不做二次旋转转换。真实游戏里的朝向和世界坐标精度仍需实机复验。

发送队列最多 64 条，所有网络发送都在连接工作线程执行；超过容量或帧大小中止连接，重新建立轮次，不在主线程等待网络。队列最多每约 100ms 被调度，采样间隔为 500ms，不承诺准确到毫秒的送达延迟。

## 重连行为

来源 7DTD 断线清理 v2 活动记录、墓碑及 stream；Bridge 重启后状态为空。Minecraft 断线时不伪造原生死亡；重新注册 Minecraft 是明确的接收端重同步边界，清理旧 v2 镜像快照状态，通过既有 peer_connected 令 7DTD 丢弃积压并发送新 stream 的完整 spawn。

没有增加序号字段、协议消息、ACK、离线补发或版本协商。需要同时使用本阶段的 Bridge 和两个 Mod；旧 Bridge 不能接收 v2，不能静默降级成 v1。

## 文件修改列表

新增：

- `bridge-server/EntityTransportV2.cs`
- `bridge-server/NativeEntityRegistry.cs`
- `7dtd-mod/src/NativePlayerPublisher.cs`
- `tests/native-transport-runner/NativeTransportRunner.csproj`
- `tests/native-transport-runner/Program.cs`
- `tests/native-transport-runner/validate_sender.py`
- `tests/run-phase3_6_1.ps1`
- `docs/phase3_6_1_7dtd_entity_transport.md`

修改：

- `bridge-server/Program.cs`：按版本选择验证、Registry 和目标连接。
- `7dtd-mod/src/BridgeClient.cs`：v2 DTO、序列化、发送队列和连接轮次。
- `7dtd-mod/src/BridgeMod.cs`：原生玩家采集及退出世界生命周期。
- `minecraft-mod/src/main/java/io/mc7dtd/BridgeClient.java`：v2 接收日志，不生成对象。
- `tests/entity-runner/Program.cs`
- `tests/marker-runner/Program.cs`
- `tests/player-proxy-runner/Program.cs`：以上三个仅允许测试指定独立构建，默认路径保留。
- `README.md`
- `docs/entity_sync_protocol.md`：更新当前实现入口，保留 v1 历史主体。

构建产物位于 bridge-server/bin/Phase361、7dtd-mod/bin/Phase361、7dtd-mod/dist、minecraft-mod/build 及 tests/*/bin/Phase361；测试配置和证据均在 work/phase3_6_1-test。没有部署到原游戏安装目录，没有启动或停止现有游戏会话。

## 编译与自动测试

在工程目录运行：

```powershell
Set-Location 'D:\wenjian\minecraft\7-M'
.\tests\run-phase3_6_1.ps1
```

脚本编译 Bridge、七日杀 net48 Mod、Fabric Java 21 Mod 和测试客户端；使用独立 Phase361 输出，避免覆盖正在运行的旧 Bridge。首次普通 Release 构建曾因旧 Bridge 进程锁住 DLL 而失败；独立构建成功，未强制结束旧会话。

环境依赖沿用工程：.NET SDK 10、.NET Framework 4.8 引用程序集、本机七日杀 Assembly-CSharp / Unity 程序集、JDK 21、工程内 Gradle 缓存。Python Schema 依赖沿用 work/phase3_4-test/python-libs；若新机器没有依赖，参照 tests/entity-types/requirements.txt 安装到该工程内目录。

最终自动测试结果：

| 验证 | 结果 |
| --- | --- |
| v2 验证、冲突、原生生命周期及真实 WebSocket 传输 | 49 项通过 |
| C# 实际序列化 JSON / Java 实际接收 JSON 的既有 v2 Schema 与语义校验 | 9 项通过 |
| v1 EntityTransport / EntityRegistry / 原有通信回归 | 87 项通过 |
| marker 接收及清理回归 | 24 项通过 |
| 玩家代理接收及旋转/清理回归 | 26 项通过 |
| Bridge / 7DTD / Fabric 编译 | 全部成功 |

合计 195 项检查通过。日志：work/phase3_6_1-test/{tests.log,schema-tests.log,bridge-build.log,7dtd-build.log,fabric-build.log,entity-runner-regression.log,marker-runner-regression.log,player-proxy-runner-regression.log}。

端到端测试运行生产 Bridge、实际 C# BridgeClient 与实际 Java BridgeClient，自动提供原生玩家快照。验证 spawn/update/despawn 各一次到达、非默认逆映射、无回传、Bridge 重启、Minecraft 单独重连，以及真实 WebSocket 入站伪造 authority 被拒绝。它不是两个真实游戏世界的运行验收。

## 后续实机复验方法（本阶段未执行）

1. 退出旧测试进程，执行 tests/prepare-runtime.ps1，将新 JAR 与 dist DLL 放入工程内隔离 runtime。
2. 在工程目录启动新 Bridge：`dotnet .\bridge-server\bin\Phase361\net10.0\BridgeServer.dll .\config\network.json`。现有 start-bridge.ps1 默认运行 Release 产物，本次独立构建应明确选 Phase361。
3. 启动 Minecraft 隔离 runtime 并进入世界，再启动使用工程内 Mods 的七日杀测试存档。
4. 查看七日杀 `7DTD entity sent:`，Bridge `Entity state forwarded: 7dtd -> minecraft`，Minecraft `7DTD entity received:`。
5. 七日杀进入世界应有 spawn，移动/转向后应有 update，退出世界应有 despawn（position/rotation=null，metadata={}）。对照 CoordinateMapper 逆公式核对 Minecraft 日志。
6. 重启 Bridge 或 Minecraft，七日杀应重新发送新 stream 的 spawn；Minecraft 始终不生成对象。

## 当前限制和停止点

仅自动导出本地原生玩家，未遍历僵尸、动物、多人玩家或专用服务器实体。通用 PublishNative 接口可以发送符合协议的 7DTD 类型，但没有为这些来源增加采集器。接近 4096 条容量的负载、拥塞以及 net48 在真实 Unity 运行时的发送表现未做实机验收。

采样使用游戏 GameUpdate；退出世界的正常生命周期可发送 despawn，强制杀进程不保证发送，依赖连接清理。没有可靠投递/ACK或持久化。类型目录未接入通用运行时生成。

不生成 Minecraft 实体；不实现 AI、战斗、输入、动画、装备、方块同步。本阶段完成后停止，等待下一阶段确认。

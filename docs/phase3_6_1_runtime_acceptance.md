# Phase 3.6.1 最终实机验收报告

日期：2026-10-03（Asia/Shanghai）。工程根目录：`D:\wenjian\minecraft\7-M`。

**Phase 3.6.1 本轮实机验收通过。** 真实七日杀玩家的 spawn、位置 update、yaw/pitch 变化和正常退出世界的 despawn 均到达真实 Minecraft runtime。退出后两端 WebSocket 仍在线，活动实体移除来自明确的 despawn，不依赖断线清理。

## 环境与边界

- Bridge：Phase361 构建，PID 13948，localhost:18771/ws；本轮最终退出测试全过程保持运行。
- Minecraft：真实 Java 游戏进程 PID 12820，Minecraft 1.21.11、Fabric Loader 0.18.4、Fabric API 0.140.2+1.21.11、JDK 21。
- 七日杀：真实游戏进程 PID 23692，V3.2.0 B10，存档 MC7DTD-Phase3-3。
- 本轮坐标配置：scale=1，offsetX/offsetY/offsetZ=0；未更改配置。
- 运行中的 JAR/DLL 与本阶段构建哈希相同，见 [artifact-hashes.json](phase3_6_1-runtime-evidence/artifact-hashes.json)。七日杀加载路径为工程内 runtime/7dtd/Mods/MC7DTD-Bridge。
- Minecraft 保持主菜单且 Mod 已连接；实际游戏接收线程记录 v2 消息，不是 Java 测试客户端。未进入 Minecraft 世界，未生成 Minecraft 实体。

前一轮按实体 Esc 后自动操作停止，保留了移动与转向证据。本轮只正常退出七日杀世界并完成最终生命周期核对，没有重启 Bridge 或游戏、没有修改源代码或协议。

## 同一实体生命周期

entity_id：`d8e7c437-fca2-4ccd-82e9-ad13fdaf1690`。
stream_id：`5cf26971-f748-4142-b4cd-04d762e18d59`。
类型：7dtd:player；source=authority=origin.game=7dtd。

| 项目 | 真实证据 | 结果 |
| --- | --- | --- |
| 进入七日杀世界 | 19:17:19，sequence=1，七日杀发送 spawn，Bridge spawned/count=1，Minecraft 接收 | 通过 |
| 移动玩家 | 同一身份的 update 位置发生变化，并由 Minecraft 接收 | 通过 |
| 左右/上下转动视角 | yaw/pitch 均有实际变化；发送与接收 rotation 相同 | 通过 |
| 正常退出世界 | 19:20:56，点击暂停菜单“退出”，返回七日杀主菜单 | 通过 |
| 七日杀发送 despawn | sequence=432，reason=world_unloaded，position=null、rotation=null、metadata={} | 通过 |
| Bridge 应用并转发 | Entity registry: despawned，count=0；7dtd → minecraft despawn 转发 | 通过 |
| Minecraft 接收 | 相同 entity_id、stream_id、sequence 和完整 despawn JSON | 通过 |
| 排除断线清理 | 退出前、退出后及再次检查 /health 均列出两端；Bridge 无断线/reset/重连日志 | 通过 |

移动前坐标（sequence=60）：x=-273.161224，y=61.079998，z=448.565155。
移动后样本（sequence=200）：x=-273.554077，y=61.079998，z=448.819153。
移动后样本 yaw=109.177429、pitch=-3.125000；观察窗口 yaw 范围 [53.67742919921875, 153.80242919921875]，pitch 范围 [-8.625, 0]。roll 保持 0，本次不宣称验证了 roll 动态变化。

七日杀发送与 Minecraft 接收各 **432 条**同实体事件：1 条 spawn、430 条 update、1 条 despawn。逐条按 sequence 对照，位置符合坐标逆公式，rotation 原样一致；最后一条为 despawn，退出后没有该身份的后续 update 或复活 spawn。

## 活动记录移除与墓碑的区别

本阶段 v2 NativeEntityRegistry 在 despawn 后将记录标记为 inactive 并保留轮次墓碑，活动计数归零。已验证活动实体不再注册/转发；**并未物理删除墓碑或清空整个 v2 字典**。保留墓碑是现有协议的防旧消息复活规则，本轮没有修改这一实现。

活动记录移除依据为同身份的 Bridge despawned/count=0 日志，与实际源端发送和目标端接收交叉核对；未新增查询接口、读取游戏内存或更改状态机。退出后所有三个进程 PID 不变，两个游戏仍在主菜单，Bridge 继续运行。

## 证据与验证

目录：[docs/phase3_6_1-runtime-evidence](phase3_6_1-runtime-evidence/)。

- [verification.json](phase3_6_1-runtime-evidence/verification.json)：16 项日志/状态对照全部通过，passed=true。
- [7dtd-game.log](phase3_6_1-runtime-evidence/7dtd-game.log)：spawn 第 805 行；正常退出 despawn 第 1348 行。
- [minecraft-latest.log](phase3_6_1-runtime-evidence/minecraft-latest.log)：收到 despawn 第 551 行。
- [bridge.log](phase3_6_1-runtime-evidence/bridge.log) 与 [实体日志摘录](phase3_6_1-runtime-evidence/bridge-entity-extract.log)：生命周期、活动计数及转发。
- [entity-events.json](phase3_6_1-runtime-evidence/entity-events.json)：432 条源端与接收端消息逐条对应。
- [movement-rotation.json](phase3_6_1-runtime-evidence/movement-rotation.json)：移动前后及旋转样本。
- health-before-exit.json、health-after-exit.json、health-after-settle.json：退出前后两端持续在线。
- processes.json、processes-after-exit.json：同一进程的运行证据。
- source-before.json：与本轮结束时哈希对照，源代码、配置和 v2 Schema 全部未变。
- 7dtd-before-move.png、7dtd-after-world-exit.png：真实世界和正常退出后主菜单截图。

本次无需重新编译或重复运行既有 195 项自动测试；使用已编译且哈希核对一致的产物进行真实游戏验收。此处 16 项为运行证据对照，不是新增游戏功能测试或生产代码。

## 修改文件与复验方法

新增本报告 `docs/phase3_6_1_runtime_acceptance.md`；新增/更新以上 `docs/phase3_6_1-runtime-evidence/` 证据文件。游戏既有日志继续自然写入 runtime/minecraft/logs/latest.log 和 docs/runtime-evidence/7dtd-game.log。无源代码、协议、配置或构建文件修改。

复验：保持三端相同版本和 Bridge 在线，让七日杀进入存档，观察 spawn；移动并左右/上下转向，核对 update；正常退出世界后核对三端相同身份的 despawn、Bridge 活动 count=0，并确认 /health 仍包含 minecraft 和 7dtd。必须通过游戏“退出世界”验证，不能杀进程替代。

## 停止点与限制

Phase 3.6.1 最终生命周期验收已完成，停止并等待用户确认。不生成 Minecraft 实体，不实现 AI、战斗、动画或下一阶段功能。只验证当前本地七日杀玩家和既有日志接收通道，不推广为多人/专用服务器、高负载或所有实体类型验收。

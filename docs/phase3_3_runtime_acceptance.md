# Phase 3.3 marker 实机验收

日期：2026-10-03（北京时间）。核心 spawn → 移动后 update → despawn 实机验收通过。

## 实际环境

Minecraft Java 1.21.11、Fabric Loader 0.18.4、Fabric API 0.140.2+1.21.11、Java 21.0.12；真实 Minecraft 演示单人世界，使用既有 MC7DTD_ENTITY_TEST=1 可选入口。

七日杀 V3.2.0 b10、Unity 2022.3.62f2，标准 IModApi 加载工程内 DLL。首次进程 PID 32212，在工程内 runtime/7dtd/Saves/Navezgane/MC7DTD-Phase3-3 创建并保存世界。Minecraft PID 32404。Bridge 在 localhost:18771/ws，测试偏移配置下 PID 5436。端口及协议没有变化。

首次验收后补充 WorldShuttingDown 回调，以确保世界退出时显式清理；最终 DLL 已编译并部署，七日杀重启进程 PID 18584。首轮 DLL 保存在证据目录，最终 DLL 哈希另行保存，不将两个构建混为同一文件。

## 核心结果

| 事件 | 时间 | Minecraft 原始位置 | 七日杀映射后位置 | Registry / 本地对象数 |
| --- | --- | --- | --- | --- |
| spawn | 14:56:10 | (-57.5,66,60.5) | (-276,61,447) | 1 / 1 |
| 移动后 update | 15:01:35 | (-62.786383669744914,68,58.900589186292144) | (-281.28638366974491,63,445.40058918629217) | 1 / 1 |
| despawn | 15:05:36 | null | null | 0 / 0 |

marker 类型为 minecraft:marker，身份 f017a595-0a5c-43ad-a66e-5877ca9bc7fd，Unity instance=-991736。仅创建一次，更新沿用同一对象；接收应用日志 thread=1，确认在游戏主线程执行。创建日志确认 active=True、renderer=True、colliderEnabled=False。

七日杀画面实际可见青色立方体，移动后的新位置也已截图，despawn 后相同视角对象消失。删除日志确认同一 instance 已禁用并调用 Unity Destroy（按 Unity 帧结束语义调度），没有宣称直接扫描了 Unity 全部对象或内存。

首次对象在七日杀玩家视角后方，默认视角中不可见；通过游戏自带调试坐标与调整视角找到对象，未因此修改对象逻辑。验收时临时使用 scale=1、offsetX=-218.5、offsetY=-5、offsetZ=386.5，使两款游戏的位置接近。实机对照逐轴按既有 CoordinateMapper 公式验证，容差 1e-10。

用户手动移动 Minecraft 并发送 update/despawn。一次自动输入被当作普通聊天文本，没有实体事件；通过真实发送日志确认后由用户重发，未计入成功事件。

## 最终构建复核与恢复

最终 DLL（SHA-256 见 final-dll-hashes.json）由七日杀 PID 18584 正常加载。15:12:41 再次 spawn 成功，Unity instance=-987132，15:13:14 update 成功；场景中再次可见 marker，见 [最终对象截图](phase3_3-runtime-evidence/final-marker-visible.jpg)。该轮没有移动玩家，移动验证仍采用首轮实机证据。

最终轮自动 despawn 输入期间，Minecraft 测试玩家被原生怪物击杀，命令没有发送成功；不将其记作通过的删除事件，也不是桥接造成伤害。测试玩家随后重生，游戏已暂停；验收用存档中的这次原生死亡确实发生，未恢复死亡前的物品或位置。

15:15:47 在仍有活动 marker 时正常退出七日杀世界，WorldShuttingDown 回调删除 instance=-987132，并记录 Marker reset: count=0。这覆盖最终构建的实际对象销毁和新增清理回调；首轮完整 despawn 的实现仍由前述三事件证据证明。见 [最终七日杀日志](phase3_3-runtime-evidence/7dtd-final-process.log)与[最终复核结果](phase3_3-runtime-evidence/final-runtime-checks.json)。

测试结束后 coordinate.json 按原文件逐字节恢复为 scale=1、三个偏移=0，并重启 Bridge（PID 25924，15:16:38），内存注册表重新为空。两款游戏自动重连，health 显示 minecraft 和 7dtd 均在线；七日杀停在主菜单、Minecraft 停在暂停菜单，本地 marker 数为 0。见 [恢复后 Bridge 日志](phase3_3-runtime-evidence/bridge-restored.log)、[连接状态](phase3_3-runtime-evidence/health-final.json)与[保留文件哈希](phase3_3-runtime-evidence/preserved-files.json)。

## 自动检查与证据

- marker 自动检查：24 项通过，failure=null；使用假场景后端和实际 Bridge/接收客户端 WebSocket，不能代替 Unity 对象实机验收。
- 原 Phase 3.2 回归：87 项通过，failure=null，包含 EntityRegistry、传输、坐标映射及现有消息回归。
- Fabric 构建成功；七日杀及 Bridge Release 构建均 0 警告、0 错误。
- [三端日志对照](phase3_3-runtime-evidence/runtime-comparison.json)：passed=true、gameRuntimeTested=true、同一对象创建一次、映射与状态数量通过。
- [创建截图](phase3_3-runtime-evidence/marker-spawn.jpg)、[更新截图](phase3_3-runtime-evidence/marker-update.jpg)、[删除截图](phase3_3-runtime-evidence/marker-despawn.jpg)。
- [Minecraft 原始日志](phase3_3-runtime-evidence/minecraft-latest.log)、[七日杀首轮日志](phase3_3-runtime-evidence/7dtd-game-first-round.log)、[Bridge 日志](phase3_3-runtime-evidence/bridge-test.log)。
- [自动结果](phase3_3-runtime-evidence/automatic-results.json)、[原生命周期回归结果](phase3_3-runtime-evidence/lifecycle-regression-results.json)、[最终 DLL 哈希](phase3_3-runtime-evidence/final-dll-hashes.json)。

保存日志后可在项目根执行 `python tests/verify-phase3_3-runtime.py` 复验映射、身份、创建/删除日志与三事件数量。编译、启动、手动命令及清理方法见 [marker 实现说明](phase3_3_marker.md)。

## 文件变化

新增：

- 7dtd-mod/src/MarkerController.cs
- 7dtd-mod/src/UnityMarkerScene.cs
- tests/marker-runner/MarkerRunner.csproj
- tests/marker-runner/Program.cs
- tests/run-phase3_3.ps1
- tests/verify-phase3_3-runtime.py
- docs/phase3_3_marker.md
- docs/phase3_3_runtime_acceptance.md
- docs/phase3_3-runtime-evidence/ 下的日志、截图、配置快照及核对结果。

修改：

- 7dtd-mod/MC7DTD.Mod.csproj：引用游戏自带 Unity 模块，不复制游戏 DLL。
- 7dtd-mod/src/BridgeClient.cs：保留接收日志，增加可选实体/重置回调。
- 7dtd-mod/src/BridgeMod.cs：注册主线程消费及关闭清理事件。
- minecraft-mod/src/main/java/io/mc7dtd/EntityTestCommands.java：新增 marker 手动验收命令，保留玩家测试命令。
- README.md：增加本阶段文档链接及范围说明。

原部署脚本刷新工程内 runtime 的 JAR/DLL；原启动脚本刷新进程记录和运行日志。work/inspect 用于只读检查游戏公开 ModEvents/Origin 接口。验收配置临时调整，不修改 CoordinateMapper、EntityRegistry、EntityTransport 或 entity_state 协议文档。

## 限制

marker 是七日杀场景中的本地视觉对象，不是原生生物实体或地形方块，不参与网络复制、AI、伤害、战斗和存档。仅一个身份完成移动实机验收；没有多实体压力测试、浮动原点迁移实测、旋转应用或目标对象确认协议。

清理与连接边界详见实现说明：源独立离线没有即时 peer_disconnected 通知；Bridge 注册表与目标对象创建不具备事务保证。不会把未就绪世界、容量拒绝或对象创建失败宣称为目标生成成功。

完成后停止，不进入下一阶段。

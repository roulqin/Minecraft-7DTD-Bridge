# Phase 3.5.1 最终实机验收

日期：2026-10-03，北京时间。工程：`D:\wenjian\minecraft\7-M`。

**本轮玩家退出生命周期验收通过。** 结合首轮已验证的生成及位置/旋转更新，Phase 3.5.1 要求的核心生命周期已完成。不进入下一阶段。

## 环境与启动

- Bridge Release：PID 29744，localhost:18771/ws；坐标配置保持默认 scale=1、三个偏移=0。
- Minecraft runtime：PID 25968，Minecraft 1.21.11、Fabric Loader 0.18.4、Fabric API 0.140.2+1.21.11、JDK 21。旧实例正常退出后加载最终 JAR。
- 七日杀：V3.2.0 B10，PID 8288，复用已进入 MC7DTD-Phase3-3 世界的真实实例；运行中的 DLL 与最终构建 SHA-256 一致，不重复启动同角色游戏。

原部署脚本成功更新 Minecraft JAR；随后尝试覆盖七日杀 DLL 时因游戏已映射文件而失败。没有强制覆盖或修改安装目录。核对已有 DLL 与最终构建相同后复用该进程继续验收。所有运行数据和证据保存在工程内。

Minecraft 构建/部署 JAR SHA-256 均为 `63685c9d78882fdcf62e6034e690b57fbeeb834e414acc18b98ebf7fb8b0c7c9`；七日杀构建/部署 DLL 均为 `0c82923c4273a0be50b2ca82455d78e26e0f64286585e0a6c925027537f2006f`。完整对照保存在 verification.json。

## 三端证据

同一 entity_id：`03515d65-b74b-4691-8710-070d53b1135f`。
同一 stream_id：`5e818f99-bd9d-4a7f-9e39-f44656f19abf`。
七日杀 Unity instance：`-1023704`。

| 验收步骤 | 时间 | 结果 |
| --- | --- | --- |
| Minecraft 进入世界 | 18:34:25 | 自动 spawn，类型 minecraft:player；Bridge spawned/count=1 |
| 七日杀创建代理 | 18:34:25 | 同一身份仅创建一次，五部件，colliderEnabled=false；本地 count=1，thread=1 |
| 常规更新 | 进入至退出前 | 自动 update 在三端日志出现，同一身份与 stream 保持不变 |
| Minecraft 保存并退出世界 | 18:35:33 | 自动 despawn，reason=world_unloaded，sequence=63；position/rotation=null，metadata={} |
| Bridge 应用和转发 | 18:35:33 | Entity registry: despawned，count=0；转发相同身份的 despawn |
| 七日杀删除代理 | 18:35:33 | 同一 instance 禁用并调用 Unity Destroy，随后 Player proxy despawned/count=0、thread=1 |

退出后 /health 显示 Minecraft 和 7DTD 均仍在线。这次删除来自 Minecraft 正常退出世界的 despawn，不是断线、Bridge 重启或七日杀世界关闭造成的 Reset。

删除证据证明同一对象已设为 inactive，并按 Unity 帧末销毁语义调度 Destroy；没有通过内存扫描推断对象状态。Registry 清空依据为服务实际 despawned/count=0 日志，并与同身份的转发及接收删除日志交叉验证。

## 证据文件

目录：`docs/phase3_5_1-runtime-evidence/final-exit/`。

- [自动对照结果](phase3_5_1-runtime-evidence/final-exit/verification.json)：passed=true、registryEmpty=true、singleObjectCreated=true。
- [Minecraft 日志](phase3_5_1-runtime-evidence/final-exit/minecraft-latest.log)：真实自动 spawn/update/despawn。
- [Bridge 日志](phase3_5_1-runtime-evidence/final-exit/bridge.log)：Registry 1→0 和事件转发。
- [七日杀日志](phase3_5_1-runtime-evidence/final-exit/7dtd-game.log)：同一 Unity instance 创建、禁用、删除及接收计数。
- [退出后连接状态](phase3_5_1-runtime-evidence/final-exit/health-after-exit.json)：两个客户端仍在线。
- minecraft-before-restart.log、7dtd-before-test.log：保留本次开始前的现场日志。

首轮的位置和旋转变化证据继续保留在 [首轮验收记录](phase3_5_1_runtime_acceptance.md)。该记录中的中断和旧 JAR 限制属于历史现场；最终 JAR 已在本轮重新加载并完成退出验收。

## 复现方法

1. 使用已有 build.ps1 构建，正常退出待更新游戏后用 tests/prepare-runtime.ps1 部署。相同哈希的运行中 DLL 可以复用，不能强制覆盖被游戏映射的文件。
2. 按 tests/launch-runtime.py bridge、minecraft、7dtd 启动；两款游戏进入测试世界，等待 Player proxy spawned/count=1。
3. Minecraft 菜单选择“保存并退回到标题屏幕”，保持游戏进程及七日杀世界在线。
4. 核对 Minecraft despawn、Bridge despawned/count=0、七日杀同对象删除及本地 count=0，三个日志的 entity_id 必须一致。

## 文件修改与限制

本轮未修改源码、协议、类型配置、CoordinateMapper 或 EntityRegistry，没有新增功能。只部署已有最终 JAR，更新报告与 README，并新增上述实机证据。没有修改网络端口或坐标配置，也没有重新执行已通过的自动测试。

当前 Bridge 和两个游戏连接保留；Minecraft 已退出世界，七日杀测试世界仍运行，代理与 Registry 数量为 0。

验收范围为一个本地静态玩家代理。仍不包含输入控制、AI、动画、装备、碰撞、战斗、存档或多人对象复制；强制终止源进程的清理边界沿用实现说明。本轮未新增可视截图结论，也未单独验收完整游戏关闭回调；这些不作为本次“退出世界”生命周期通过依据。

Phase 3.5.1 到此停止，等待用户确认。

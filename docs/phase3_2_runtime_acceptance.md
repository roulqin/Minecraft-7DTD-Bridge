# Phase 3.2 实机验收报告

验收日期：2026-10-03（北京时间）。结论：本次要求的实体生命周期及 Bridge 重启验收通过。

## 环境与范围

- Minecraft Java 1.21.11，Fabric Loader 0.18.4，Fabric API 0.140.2+1.21.11，Java 21.0.12；真实单人世界，进程 PID 32092。
- 七日杀 V3.2.0 b10，真实游戏进程 PID 32180；Mod 在主菜单运行，仅接收并记录日志。
- Bridge 使用既有 Release/net10.0 构建，localhost:18771/ws；重启前 PID 4256，重启后 PID 25812。
- 使用既有 Minecraft 手动验收入口；用户在真实游戏中执行命令并移动玩家。没有新增入口、协议字段或序号逻辑，没有生成游戏实体。

## 生命周期结果

| 时间 | 操作 | Bridge 结果 | 注册数 | 七日杀收到事件 |
| --- | --- | --- | --- | --- |
| 14:31:00 | spawn | spawned | 1 | spawn |
| 14:31:33 | 重复 spawn | duplicate_spawn | 1 | 无 |
| 14:31:43 | 移动后 update | updated | 1 | update |
| 14:32:02 | despawn | despawned | 0 | despawn |
| 14:32:11 | 再次 spawn，留存记录 | spawned | 1 | spawn |
| 14:33:38 | Bridge 重启后先 update | entity_not_found | 0 | 无 |
| 14:33:43 | 新 spawn | spawned | 1 | spawn |
| 14:33:50 | despawn 清理 | despawned | 0 | despawn |

注册表状态通过正式 Bridge 的状态转换日志及 count 核对，没有新增查询接口或直接导出内存对象。记录数量依次为 **1、1、1、0、1、0、1、0**。重复 spawn 没有覆盖或重复转发；不存在的实体 update 没有自动创建记录。

真实玩家从约 `(-19.031463, 64, 26.533262)` 移动到 `(-14.941493, 63, 23.003418)`。七日杀日志中的 update 坐标与 Minecraft 发送值在默认映射下相符，逐轴比较容差为 1e-12；朝向和身份字段也完成对照。

## Bridge 重启结果

重启前最后一条状态为 spawned、count=1。14:32:49 重启既有 Bridge，14:32:52 两款游戏自动重连，原测试消息恢复双向转发。两款游戏进程未重启。

重启后第一次 update 与重启前活动记录具有相同的 source、world、dimension、entity_id、entity_type 和 stream_id，却返回 entity_not_found、count=0；Minecraft 日志也记录该错误。这证明旧记录已清空，而非通过更换身份造成查询失败。随后新 spawn 正常建立，最终 despawn 清理为 0。

本轮 Minecraft 共发送 8 个事件，七日杀收到 6 个有效事件。既有 sequence 值用于日志关联：收到 4、6、7、8、10、11；重复 spawn 的 5 和重启后无记录 update 的 9 未转发。没有新增序号系统，也未验证乱序过滤。

## 证据

所有证据位于项目内 [phase3_2-runtime-evidence](phase3_2-runtime-evidence/)。

- [日志对照结果](phase3_2-runtime-evidence/lifecycle-comparison.json)：passed=true，8 次状态转换、发送及接收明细、最终注册数 0。
- [重启前 Bridge 日志](phase3_2-runtime-evidence/bridge-before-restart.log)及[重启后日志](phase3_2-runtime-evidence/bridge-after-restart.log)。
- [Minecraft 日志](phase3_2-runtime-evidence/minecraft-latest.log)及[七日杀日志](phase3_2-runtime-evidence/7dtd-game.log)。
- [重启记录](phase3_2-runtime-evidence/restart.json)、[新进程记录](phase3_2-runtime-evidence/new-process.json)及[最终连接状态](phase3_2-runtime-evidence/health-final.json)。
- [验收前基线](phase3_2-runtime-evidence/baseline.json)用于排除上阶段已有的 3 条实体事件；本次未新增截图。
- [文件核对](phase3_2-runtime-evidence/source-after.json)：纳入比较的 38 个源码、构建/测试脚本、配置及协议文件全部未变；[运行构建哈希](phase3_2-runtime-evidence/runtime-hashes.json)记录实际使用的文件。

## 复验方法

1. 使用项目原有 start-bridge.ps1 启动 Bridge；已有实例运行时不要重复启动。
2. 按原运行环境启动 Minecraft（MC7DTD_ENTITY_TEST=1）及七日杀，确认两端连接。
3. Minecraft 依次执行 `/mc7dtd_entity_test spawn`、再次 spawn、移动后 update、despawn、再次 spawn。
4. 确认状态为 spawned、duplicate_spawn、updated、despawned、spawned，注册数为 1、1、1、0、1。
5. 只重启 Bridge，等待两款游戏重连；Minecraft 依次执行 update、spawn、despawn。
6. 确认 entity_not_found/count=0、spawned/count=1、despawned/count=0；七日杀不收到被拒绝事件。

本轮没有重新编译或修改源代码；自动测试 87 项通过是上一实施阶段的结果，本次新增的是实机日志验收和证据对照。

## 修改文件与限制

新增本报告及 phase3_2-runtime-evidence 下的日志、进程、基线、哈希、连接状态和对照结果；补充 phase3_2_entity_lifecycle.md 的验收结论。既有 runtime-evidence 中 Bridge 启动日志及进程记录由原启动脚本刷新，游戏日志自然追加。

本次覆盖一个真实 Minecraft 玩家身份、默认坐标映射及 Bridge 进程重启。七日杀验证接收日志，没有验证世界中的对象行为；没有直接检查完整注册对象或 LastUpdate 内存值。本轮未另做游戏独立断线、乱序、容量和多实体压力测试。

验收结束时 Bridge 与两款游戏保持运行，注册表已由最终 despawn 清至 0。到此停止，不进入实体生成或后续阶段。

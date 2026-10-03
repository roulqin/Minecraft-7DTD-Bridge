# Phase 2.0 — 最小玩家位置同步

日期：2026-10-03（Asia/Shanghai）。工程根目录：D:\wenjian\minecraft\7-M。

## 实现范围

Minecraft 客户端读取当前玩家 x/y/z，每约 500ms 发送一条位置消息，经 Bridge 转发到七日杀；七日杀只输出坐标日志。
没有朝向、血量、移动状态、方块、实体生成或战斗逻辑。
原有 localhost:18771/ws、握手、welcome、测试消息、连接重试与目录结构保留。

```json
{
  "type": "player_position",
  "source": "minecraft",
  "x": 100,
  "y": 64,
  "z": 200
}
```

## 采集与转发

- PlayerPositionSampler 注册 Fabric END_CLIENT_TICK，在游戏线程读取 player.getX/getY/getZ。
- 使用 System.nanoTime 控制 500ms 间隔。正常 20 TPS 时约每秒两条；低帧率或卡顿会延迟，恢复后不补发积压采样。
- 主菜单、未进入世界或客户端暂停时不发送；重新进入世界开始新采样。
- 后台发送队列最多一个待发样本，拥塞时用新样本替换旧样本；游戏线程不等待网络。
- 已有 test 与新增 position 共享同一 WebSocket。写操作串行执行，避免并发发送；重连期间不发送坐标，重新 welcome 后恢复。
- Bridge 要求已注册角色及 source 都是 minecraft，x/y/z 必须为有限 JSON 数字。零值、负数及小数原样转发；缺失、字符串、null、布尔和无限大拒绝。
- 七日杀再次校验 source 和数值，仅调用日志接口：`Minecraft player: x=100 y=64 z=200`。使用固定数字文化格式，不受 Windows 小数分隔符影响。
- 对端离线仍使用原有 peer_unavailable 响应，无持久队列。

## 构建说明

```powershell
Set-Location -LiteralPath 'D:\wenjian\minecraft\7-M'
.\build.ps1
```

为直接访问游戏玩家 API，在原 minecraft-mod 项目增加 Loom 1.14.10、Yarn 1.21.11+build.2、Fabric API 0.140.2+1.21.11；Gradle 更新为该 Loom 支持的 9.2.1。
Java 21、Minecraft 1.21.11、Loader 0.18.4 不变，不移动 Phase 1 源码或拆分通信项目。
net10.0 Bridge 和 net48 七日杀编译目标不变。
发行 Mod 是经过 remapJar 重映射的 minecraft-mod/build/libs/mc7dtd-bridge-0.1.0.jar；不要把 build/devlibs 中开发包部署到游戏。
所有下载、缓存和生成文件均位于工程内。

## 自动测试

```powershell
.\tests\run-phase1.ps1
.\tests\run-phase2.ps1
```

Phase 1 回归验证双向测试、重连及输入限制，原先“所有 Phase 2 类型禁止”的断言现改为继续禁止 block。
Phase 2 使用 Mod 的同一 Java 网络发送代码每 500ms 发布测试坐标，真实 Bridge 转发给同一 net48 C# 客户端代码，检查坐标日志、发送间隔、服务重启后位置恢复。
另外验证零值/负数/小数、位置后继续发送 test、错误来源、缺失及非法数值。

结果文件：work/phase1-test/results.json、work/phase2-test/results.json。
间隔文件：work/phase2-test/sender-intervals.json；过程日志：对应目录 process-*.log。
自动测试不包含 Minecraft 世界采集；真实采集须按下一节操作。

## 游戏内验收方法

1. 正常退出旧的工程测试游戏实例和 Bridge，以确保新代码被重新加载。不要复制 Mod 后继续使用旧进程作验收。
2. 运行 `.\tests\prepare-runtime.ps1`；会在工程内 runtime/minecraft/mods 放入桥接发行 JAR 和匹配的 Fabric API，并更新 runtime/7dtd/Mods/MC7DTD-Bridge。
3. 启动 `.\start-bridge.ps1`，再启动该独立 Fabric 实例，最后启动七日杀；详细启动方式仍见 tests/phase1_test.md。
4. Minecraft 主菜单应保持连接而不发送坐标。进入一个工程测试世界，站立观察 3 秒，再移动并改变高度。
5. Bridge 应约每 500ms 出现 `Player position forwarded: minecraft -> 7dtd (...)`；七日杀应有 `Minecraft player: x=... y=... z=...`。移动后日志应反映真实新坐标。
6. 退出世界后坐标日志停止；双向测试连接继续保持。Bridge 重启后自动重连，世界内的位置日志恢复。

只检查接收日志；七日杀中不应生成实体或修改世界。

## 文件变更

新增：

- minecraft-mod/src/main/java/io/mc7dtd/PlayerPositionSampler.java
- tests/run-phase2.ps1
- docs/phase2_player_sync.md

修改：

- minecraft-mod/settings.gradle
- minecraft-mod/build.gradle
- minecraft-mod/gradle/wrapper/gradle-wrapper.properties
- minecraft-mod/src/main/resources/fabric.mod.json
- minecraft-mod/src/main/java/io/mc7dtd/MinecraftBridgeMod.java
- minecraft-mod/src/main/java/io/mc7dtd/BridgeClient.java
- minecraft-mod/src/test/java/io/mc7dtd/ClientHarness.java
- bridge-server/Program.cs
- 7dtd-mod/src/BridgeClient.cs
- 7dtd-mod/ModInfo.xml
- tests/phase1-runner/Program.cs
- tests/prepare-runtime.ps1
- README.md

config/network.json 未修改，端口仍为 18771。构建输出和测试缓存不列为手写源码变更。

## 验证结果

本次 build.ps1 成功：Bridge/net10.0、七日杀/net48 与测试客户端均为 0 警告、0 错误；Minecraft compileJava、remapJar、build 和测试类编译均成功。

Phase 1 回归：14 项通过。Phase 2 集成：25 项通过；两份 results.json 的 failure=null。
连续五条测试位置的四个接收间隔为 502.9659、511.5689、509.2418、509.9503ms，符合约 500ms 节奏。
C# 客户端实际日志出现 Minecraft player: x=100 y=64 z=200，Bridge 重启后持续恢复坐标转发。

本次新增采集代码已编译和重映射，但未重新启动正在运行的旧版游戏实例，也未自动进入世界或生成实体。
真实玩家坐标采集和移动验收仍需按“游戏内验收方法”重启后执行；不能把测试程序生成的坐标当作实际游戏移动记录。
Phase 1.5 双游戏通信状态采用本轮用户确认。

构建期间官方 Gradle 下载重定向未完成，改用镜像获取同一 ZIP，并核对官方 SHA-256：72f44c9f8ebcb1af43838f45ee5c4aa9c5444898b3468ab3f4af7b6076c5bc3f。
Minecraft 依赖首次下载曾失败，重试后成功。Gradle 9 对仅含进程测试入口的 test 目录要求明确允许没有 JUnit 用例；实际断言仍由上述 14/25 项集成测试执行。

## 当前限制与停止点

只同步当前 Minecraft 客户端的坐标，仍只有一个 minecraft/7dtd 角色。没有玩家 ID、跨维度映射、时间戳、坐标缩放或可靠投递承诺。
慢网、暂停或卡顿会跳过样本，优先保持游戏线程响应；不追赶历史位置。
本阶段只做到七日杀日志，不影响实体或方块。完成 Phase 2.0 后停止。

参考：[Fabric 1.21.11 构建说明](https://www.fabricmc.net/2025/12/05/12111.html)、[Fabric 客户端 tick 事件](https://wiki.fabricmc.net/tutorial:event_index)。


## 2026-10-03 实机验收补充

真实玩家移动、单人暂停/恢复、Bridge 重启自动重连均已通过，原双向测试消息保留。实机证据和范围限制见 [Phase 2.0 实机验收报告](phase2_runtime_acceptance.md)。本次未修改源代码，27 个源码/脚本/配置 SHA-256 均一致；早先尚需真实移动验收的状态已由本次实机结果更新。没有进入 Phase 2.1。

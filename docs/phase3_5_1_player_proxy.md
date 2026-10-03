# Phase 3.5.1：玩家代理实现

工程：`D:\wenjian\minecraft\7-M`。日期：2026-10-03。

## 实现范围

Minecraft 自动采集当前客户端的本地玩家。进入世界发送 entity_state/spawn；未暂停时每 500ms 发送完整位置与 yaw/pitch/roll 更新；退出世界发送 despawn。桥接身份为生命期随机 UUID，不发送账号 UUID。world_id 为源存档路径或服务器地址的本地确定性 UUID 摘要，不发送路径/地址文本；不是跨游戏世界路由配置。

Bridge 继续使用原 EntityTransport、CoordinateMapper、EntityRegistry 验证、转换、管理和转发。生产服务代码、网络端口及协议均未修改。player_position 独立保留，只有 entity_state 驱动代理。

七日杀使用五个静态几何部件组成橙色人形（身体、头、鼻子、两腿）。代理是本地 Unity GameObject，不是 EntityAlive、本地登录玩家或 NPC；不参与 AI、碰撞、输入、动画、装备、伤害、多人复制或存档。鼻子标示正前方，便于验收朝向。

## 主线程应用与旋转

复用原 marker 的有限队列控制器，增加可选源类型和日志标签，原构造参数继续表示 marker。仅 player_proxy 场景实现 IRotatingProxyScene。网络线程复制消息，七日杀 GameUpdate 在主线程创建、移动、旋转、删除；重复 spawn、未知 update、stream 不匹配、非法坐标/旋转均不会建立或覆盖对象。

position 为脚底中心，使用已经映射的世界坐标减去 Origin.position；没有第二次比例/偏移。每帧刷新本地坐标以兼容浮动原点。朝向按现有协议计算 forward：`(-sin(yaw)*cos(pitch), -sin(pitch), cos(yaw)*cos(pitch))`，再用 roll 旋转 up 并设置根对象 Quaternion。Minecraft 来源的 roll 固定为 0，不实现动画。

队列和对象容量沿用 256，每轴场景坐标限制 ±1,000,000。世界关闭、目标连接中断和 Minecraft 重新连接时沿用 Reset 清理机制。

## 启动与重连

Minecraft 不在游戏线程等待网络。自动消息复用已有串行实体发送队列，限定连接轮次，避免旧队列跨新连接发送；发送失败或自动队列满时中止当前连接，走已有重连流程重新建立完整 spawn。没有新增 ACK 或可靠投递协议。

目标游戏进入世界后，通过原有 test 消息发送 `MC7DTD player proxy resync` 请求。Minecraft 收到后在客户端线程退休旧逻辑键并发送新生命期完整 spawn，解决源游戏先进入世界、目标尚未就绪时的 spawn 丢失。已有七日杀连接上线也触发刷新；Bridge 重启后 Registry 清空，源发送新 spawn。配置中的能力是类型清单，接收端本轮使用两个编译类型白名单，不实现通用配置热加载或任意适配器分发。

正常退出世界可发送 despawn。客户端正常停止事件也排入 despawn，关闭网络前最多等两秒排空实体发送队列。强制终止/崩溃无法保证送出 despawn；原协议没有 peer_disconnected，源独立异常离线可能残留代理，直到源重连、目标断线或世界退出。该边界没有通过新增协议或超时系统扩展。

## 编译、自动测试和启动

在工程根目录执行：

```powershell
.\tests\run-phase3_5_1.ps1
.\tests\prepare-runtime.ps1
python tests/launch-runtime.py bridge
python tests/launch-runtime.py minecraft
python tests/launch-runtime.py 7dtd
```

端口仍为 localhost:18771/ws。重新部署之前需保存并正常退出运行中的测试游戏。启动脚本把游戏用户数据和 Mod 放在工程内 runtime，使用安装目录的游戏可执行文件及只读依赖。不要同时启动占用同一角色的旧实例。

自动测试使用独立临时监听端口，生产 network.json 不改：

- Java 玩家生命周期：16 项，通过真实 PlayerProxyTracker 检查进入/退出、500ms 节流、暂停/恢复、身份、旋转采集、跨维度、目标就绪刷新、重连与队列拒绝。
- 代理接收：26 项，假场景后端加真实 Bridge/接收客户端 WebSocket，包含实际 Registry/CoordinateMapper 路径、主线程约束、位置/旋转快照、创建删除和断线清理。
- marker 回归：24 项；生命周期回归：87 项。
- 类型配置：9 组；原协议示例离线验证：29 项。

所有检查通过；完整组件编译成功。证据在 work/phase3_5_1-test；真实游戏结果另见 phase3_5_1_runtime_acceptance.md。自动假场景结果不代替 Unity 实机验收。

## 文件修改列表

新增：

- minecraft-mod/src/main/java/io/mc7dtd/PlayerProxyTracker.java
- minecraft-mod/src/main/java/io/mc7dtd/PlayerProxySampler.java
- minecraft-mod/src/test/java/io/mc7dtd/PlayerProxyHarness.java
- 7dtd-mod/src/UnityPlayerProxyScene.cs
- tests/player-proxy-runner/PlayerProxyRunner.csproj、Program.cs
- tests/run-phase3_5_1.ps1
- docs/phase3_5_1_player_proxy.md、phase3_5_1_runtime_acceptance.md
- docs/phase3_5_1-runtime-evidence/ 实机证据

修改：

- MinecraftBridgeMod.java：注册自动采集。
- BridgeClient.java：实体连接轮次、串行自动发送及停止排空。
- MarkerController.cs：可选类型/场景旋转接口，保留 marker。
- BridgeMod.cs：代理接收与世界清理、就绪刷新。
- BridgeClient.cs：复用 test 的目标世界就绪请求。
- config/entity_types.json：启用已实现的 player_proxy，update_rotation=true；其他受限能力仍为 false。
- tests/entity-types/validate_catalog.py、test_player_proxy.py：校验已实现能力和未知适配器拒绝。
- README.md：实现与验收文档入口。

源协议、EntityRegistry、CoordinateMapper 未改。验收坐标配置的临时调整及恢复会单独记录。完成本阶段后停止，不进入下一阶段。

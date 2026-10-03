# Phase 3.3：测试 marker 对象

本阶段仅实现 `minecraft:marker` → 七日杀本地可见对象。它是一米青色 Unity 立方体，作为跨游戏实体协议的第一个测试对象；不是七日杀 EntityAlive、玩家或 NPC，不带碰撞、AI、生命值、战斗或存档数据，也不修改地形方块。

## 架构

Minecraft 的既有可选验收入口增加 `/mc7dtd_marker_test spawn|update|despawn`，读取真实玩家位置作为测试 marker 的位置，使用独立 UUID 和既有格式的 sequence。原 `/mc7dtd_entity_test` 与 player_position 保留。

Bridge 继续执行 EntityTransport、CoordinateMapper 和 EntityRegistry，未修改这些模块、entity_state 字段、版本、端口或握手。类型名采用现有规则允许的 minecraft:marker，不新增消息类型。有效 spawn/update/despawn 经既有注册表决定是否转发。

七日杀 BridgeClient 保留原日志，并将合法实体消息交给 MarkerController。网络线程只复制不可变快照到有锁队列；标准 ModEvents.GameUpdate 在游戏线程消费。UnityMarkerScene 使用 Unity 原生 [GameObject.CreatePrimitive](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/GameObject.CreatePrimitive.html) 创建对象，update 更新同一对象的位置，despawn 先禁用再调用 Destroy，并销毁独立材质。

接收端只为 minecraft:marker 建立对象。minecraft:player 和其他合法类型仍打印日志，绝不自动替换成 marker。对象键沿用 source/world_id/dimension/entity_id，stream 不匹配的更新或删除不应用；没有新增序号排序系统。

协议 position 是底部中心，单位立方体的中心使用 `(x,y+0.5,z)`；Unity transform 再减去七日杀的 Origin.position，避免浮动原点偏移。每帧刷新已有对象的 Unity 位置，不重复执行 CoordinateMapper。marker 对称，不应用旋转；旋转字段保持传输与日志兼容。

## 清理与边界

- 只在七日杀世界及本地玩家就绪时创建对象；更新回调发现世界未就绪时丢弃待处理消息。复验时应先进入世界再发送 spawn，避免世界加载期间的消息影响验收。
- 七日杀连接丢失、Minecraft 重新连接、世界关闭和游戏退出时清理本地 marker。Bridge 原注册表仍按原连接规则清空。
- 队列及对象上限各 256；队列溢出清空待处理消息并清理对象，日志要求重新 spawn。每轴坐标限制在 ±1,000,000，避免异常数值进入 Unity 单精度场景。
- 不增加对象确认/查询消息。Bridge 注册表与目标对象并非事务：未进入世界、容量限制或对象创建失败时，Bridge 记录可能存在而本地对象不存在。应在世界就绪后先 despawn 再 spawn，或重建连接后 spawn。
- 原协议没有 peer_disconnected 通知；Minecraft 独立离线时，七日杀可能保留 marker，直至源重新上线、七日杀断线或世界退出。没有新增超时或离线协议。
- 不支持双向生成、多人网络复制、保存加载、插值、相机控制、地图轴旋转或任意原生实体类型生成。

## 编译与自动验证

全部命令在 D:\wenjian\minecraft\7-M 执行。使用原 `build.ps1` 编译三个组件；`tests/prepare-runtime.ps1` 部署到工程内 runtime，修改游戏 Mod 后须正常退出并重新启动游戏才能加载新 DLL/JAR。

```powershell
Set-Location 'D:\wenjian\minecraft\7-M'
.\build.ps1
.\tests\run-phase3_3.ps1
.\tests\run-phase3_2.ps1
.\tests\prepare-runtime.ps1
```

自动检查覆盖创建/更新/删除、重复事件、未知更新、类型白名单、stream 隔离、映射只执行一次、消息快照隔离、主线程约束、主菜单丢弃、断线/世界退出清理、队列及对象容量、非法数值以及完整 WebSocket 收发。对象操作使用可观察的假场景；Unity 实际对象另以真实游戏日志和截图验收，不把自动检查冒充游戏测试。

自动结果位于 work/phase3_3-test/results.json，实机结果见 [实机验收报告](phase3_3_runtime_acceptance.md)。

本轮结果：marker 自动检查 24 项、原 Phase 3.2 回归 87 项全部通过。真实游戏完成单对象创建、移动、删除，并在最终 DLL 下再次验证创建/更新及世界关闭清理。CoordinateMapper、EntityRegistry、EntityTransport、协议文档及 network.json 保持不变；验收偏移配置已恢复。

## 实机复验

1. 启动原 Bridge，启动配置了 MC7DTD_ENTITY_TEST=1 的 Minecraft，启动部署了新版 Mod 的七日杀。
2. 两款游戏进入各自世界，记录位置，按需在 coordinate.json 调整偏移后重启 Bridge，使 marker 在七日杀玩家可见范围内。配置只在 Bridge 启动时读取。
3. Minecraft 执行 `/mc7dtd_marker_test spawn`。预期 Bridge 显示 spawned/count=1；七日杀出现 Marker Unity created 和 Marker spawned，场景中可见青色对象。
4. 移动 Minecraft 玩家后执行 `/mc7dtd_marker_test update`。预期同一对象移动，无第二次 Unity created。
5. 执行 `/mc7dtd_marker_test despawn`。预期场景对象消失，Unity deleted 和 Marker despawned/count=0。
6. 完成后恢复测试前 coordinate.json，并重启 Bridge。未完成 despawn 时须先清理对象。

到此停止，不进入玩家实体、NPC AI、战斗或方块同步。

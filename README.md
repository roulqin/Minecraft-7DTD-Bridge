# MC7DTD-Bridge

Minecraft Fabric ↔ JSON/WebSocket Bridge ↔ 7 Days to Die Mod，Windows 11 本地通信项目。
工程根目录：D:\wenjian\minecraft\7-M。

当前为 Phase 3.6.2：Minecraft 从 entity_types.json 读取已启用的 7dtd:player 映射，接收 v2 spawn/update/despawn，在客户端世界显示青色静态代理和金色朝向标记。
既有 Minecraft → 七日杀 v1 marker/玩家代理、player_position 和 test 消息继续保留。不实现 AI、战斗、动画、玩家控制或方块同步。

## 编译与运行

```powershell
Set-Location 'D:\wenjian\minecraft\7-M'
.\build.ps1
.\tests\run-phase1.ps1
.\tests\run-phase2.ps1
.\tests\run-phase2_1.ps1
.\tests\run-phase3_1.ps1
.\tests\run-phase3_2.ps1
.\start-bridge.ps1
```

需要 JDK 21、.NET SDK 10.0.400（允许同功能带补丁）、Windows .NET Framework 4.8 运行时与本地七日杀程序集。
游戏路径不同可使用 build.ps1 -GameDir 参数。依赖自动恢复到工程 work/，不需要全局 Gradle。
默认 ws://localhost:18771/ws，三端读取 config/network.json。Ctrl+C 停止 Bridge。
Bridge 另读取与 network.json 同目录的 coordinate.json，支持 scale、offsetX、offsetY、offsetZ；修改后重启 Bridge 生效，两个 Mod 无需修改。

## 文档

- [Phase 1 测试说明](tests/phase1_test.md)：编译、三端启动、预期日志与手动验收。
- [架构与协议](docs/phase1_architecture.md)：连接、转发、重连与限制。
- [交付报告](docs/phase1_report.md)：文件变更与验证结果。
- [Phase 2 玩家坐标报告](docs/phase2_player_sync.md)：采集、验证和游戏验收方式。
- [Phase 2.0 实机验收](docs/phase2_runtime_acceptance.md)：移动、暂停恢复和自动重连证据。
- [Phase 2.1 坐标映射报告](docs/phase2_1_coordinate_mapping.md)：配置、转换、自动测试及限制。
- [实体状态协议](docs/entity_sync_protocol.md)：设计基线与 Phase 3.1 传输子集。
- [Phase 3.1 实体传输报告](docs/phase3_1_entity_transport.md)：日志接收、自动测试及部署限制。
- [Phase 3.1 实机验收](docs/phase3_1_runtime_acceptance.md)：真实 Minecraft 手动三事件、七日杀日志和复验方法。
- [Phase 3.2 生命周期报告](docs/phase3_2_entity_lifecycle.md)：注册表、重连设计、状态转换和测试。
- [Phase 3.3 marker 对象](docs/phase3_3_marker.md)：主线程创建、移动、删除及自动测试。
- [Phase 3.3 实机验收](docs/phase3_3_runtime_acceptance.md)：真实游戏对象与证据。
- [Phase 3.4 实体类型设计](docs/phase3_4_entity_types.md)：类型映射、能力声明、离线配置校验与编译验证。
- [Phase 3.5.0 玩家代理设计](docs/phase3_5_0_player_proxy.md)：禁用的类型定义、能力边界、玩家消息示例与自动验证。
- [Phase 3.5.1 玩家代理实现](docs/phase3_5_1_player_proxy.md)：自动生命周期采集、主线程代理、位置与旋转。
- [Phase 3.5.1 最终实机验收](docs/phase3_5_1_final_acceptance.md)：最终 JAR 的退出 despawn、同对象删除与 Registry 清空通过。
- [Phase 3.5.1 首轮历史记录](docs/phase3_5_1_runtime_acceptance.md)：创建、位置/朝向更新及当时的 Esc 中断现场。
- [Phase 3.6.0 双向实体协议设计](docs/phase3_6_0_bidirectional_protocol.md)：v2 草案、所有权、冲突规则、六个 JSON 示例与离线验证。
- [Phase 3.6.1 七日杀实体发送](docs/phase3_6_1_7dtd_entity_transport.md)：v2 接入、v1 兼容、构建、自动测试与实机复验步骤。
- [Phase 3.6.2 Minecraft 代理接收](docs/phase3_6_2_minecraft_proxy.md)：类型配置、主线程显示、测试、实机验收与限制。
- [Phase 0 历史环境快照](docs/environment_report.md)：保留原始检测结果。

## 目录与边界

minecraft-mod：Fabric 客户端；7dtd-mod：七日杀标准 Mod；bridge-server：本地服务。
config：共享配置；docs：文档；tests：自动测试和验收步骤。
work：工具、依赖缓存和测试日志；runtime：准备脚本生成的工程内隔离验收目录。
所有项目文件均保存在工程根目录内；不修改其他项目或原游戏安装。

不使用 DLL 注入、内存修改、Cheat Engine、破解或进程 Hook。七日杀 DLL 通过标准 IModApi 加载，不使用 Harmony 补丁。
每阶段结束等待确认后继续；Phase 3.6.2 自动测试、编译和真实双游戏代理显示验收已完成。当前反向仅自动采集七日杀本地原生玩家；类型配置需重启 Minecraft 后读取，无通用实体类型生成或热加载。

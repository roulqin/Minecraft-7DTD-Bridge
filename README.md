# MC7DTD-Bridge

Minecraft Fabric ↔ JSON/WebSocket Bridge ↔ 7 Days to Die Mod，Windows 11 本地通信项目。
工程根目录：D:\wenjian\minecraft\7-M。

Phase 2.0 已完成实机验收：Minecraft 每约 500ms 发送玩家坐标，七日杀接收并写日志。
当前 Phase 2.1 在 Bridge 增加坐标映射：输出坐标 = 输入坐标 × scale + 对应轴偏移，默认保持原坐标。
Phase 3.1 增加 entity_state 的 spawn/update/despawn 正向传输，七日杀只打印日志；Minecraft Mod 仍只采集 player_position。
实机验收经用户授权新增默认关闭的手动实体测试命令，只有 MC7DTD_ENTITY_TEST=1 时启用；不自动采集实体或应用生命周期。
Phase 3.2 在 Bridge 管理基础实体生命周期：防重复spawn、更新已有记录、despawn删除，断线清空并要求重新spawn；两个Mod仍不生成对象。
本阶段不实现方块、实体或战斗同步。

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
- [Phase 0 历史环境快照](docs/environment_report.md)：保留原始检测结果。

## 目录与边界

minecraft-mod：Fabric 客户端；7dtd-mod：七日杀标准 Mod；bridge-server：本地服务。
config：共享配置；docs：文档；tests：自动测试和验收步骤。
work：工具、依赖缓存和测试日志；runtime：准备脚本生成的工程内隔离验收目录。
所有项目文件均保存在工程根目录内；不修改其他项目或原游戏安装。

不使用 DLL 注入、内存修改、Cheat Engine、破解或进程 Hook。七日杀 DLL 通过标准 IModApi 加载，不使用 Harmony 补丁。
每阶段结束等待确认后继续；当前为 Phase 3.5.1。已实现本地静态玩家代理，自动测试及核心生命周期实机验收通过。类型目录不支持通用运行时分发或热加载；不实现 AI、输入控制、动画、装备、战斗或方块同步。

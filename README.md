# MC7DTD-Bridge

Minecraft Fabric ↔ JSON/WebSocket Bridge ↔ 7 Days to Die Mod，Windows 11 本地通信项目。
工程根目录：D:\wenjian\minecraft\7-M。

当前已完成 Phase 1 基础通信实现与自动集成测试，真实游戏内加载验收待手动执行。
没有玩家、方块、实体或战斗同步；不进入 Phase 2。

## 编译与运行

```powershell
Set-Location 'D:\wenjian\minecraft\7-M'
.\build.ps1
.\tests\run-phase1.ps1
.\start-bridge.ps1
```

需要 JDK 21、.NET SDK 10.0.400（允许同功能带补丁）、Windows .NET Framework 4.8 运行时与本地七日杀程序集。
游戏路径不同可使用 build.ps1 -GameDir 参数。依赖自动恢复到工程 work/，不需要全局 Gradle。
默认 ws://localhost:18771/ws，三端读取 config/network.json。Ctrl+C 停止 Bridge。

## 文档

- [Phase 1 测试说明](tests/phase1_test.md)：编译、三端启动、预期日志与手动验收。
- [架构与协议](docs/phase1_architecture.md)：连接、转发、重连与限制。
- [交付报告](docs/phase1_report.md)：文件变更与验证结果。
- [Phase 0 历史环境快照](docs/environment_report.md)：保留原始检测结果。

## 目录与边界

minecraft-mod：Fabric 客户端；7dtd-mod：七日杀标准 Mod；bridge-server：本地服务。
config：共享配置；docs：文档；tests：自动测试和验收步骤。
work：工具、依赖缓存和测试日志；runtime：准备脚本生成的工程内隔离验收目录。
所有项目文件均保存在工程根目录内；不修改其他项目或原游戏安装。

不使用 DLL 注入、内存修改、Cheat Engine、破解或进程 Hook。七日杀 DLL 通过标准 IModApi 加载，不使用 Harmony 补丁。
每阶段结束等待确认后继续；当前停止于 Phase 1。

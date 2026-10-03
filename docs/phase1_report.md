# Phase 1 交付报告

日期：2026-10-03（Asia/Shanghai）。工程根目录：D:\wenjian\minecraft\7-M。

完成基础通信实现、三端编译和自动集成测试；未执行真实双游戏加载验收。停止于 Phase 1，没有玩家/方块同步。

## 修改文件列表

Modified：README.md。

Created（以下路径相对工程根目录；工作缓存与生成产物另列）：
- .gitignore
- 7dtd-mod\MC7DTD.Mod.csproj
- 7dtd-mod\ModInfo.xml
- 7dtd-mod\src\BridgeClient.cs
- 7dtd-mod\src\BridgeMod.cs
- bridge-server\BridgeServer.csproj
- bridge-server\Program.cs
- build.ps1
- config\network.json
- docs\phase1_architecture.md
- global.json
- minecraft-mod\bootstrap-gradle.ps1
- minecraft-mod\build.gradle
- minecraft-mod\gradle.properties
- minecraft-mod\gradle\wrapper\gradle-wrapper.jar
- minecraft-mod\gradle\wrapper\gradle-wrapper.properties
- minecraft-mod\gradlew
- minecraft-mod\gradlew.bat
- minecraft-mod\settings.gradle
- minecraft-mod\src\main\java\io\mc7dtd\BridgeClient.java
- minecraft-mod\src\main\java\io\mc7dtd\MinecraftBridgeMod.java
- minecraft-mod\src\main\resources\fabric.mod.json
- minecraft-mod\src\test\java\io\mc7dtd\ClientHarness.java
- start-bridge.ps1
- tests\client-harness\ClientHarness.csproj
- tests\client-harness\Program.cs
- tests\phase1_test.md
- tests\phase1-runner\Phase1Runner.csproj
- tests\phase1-runner\Program.cs
- tests\prepare-runtime.ps1
- tests\run-phase1.ps1
- docs/phase1_report.md（本报告）

额外工程内文件：work/inspect/inspect.csproj、work/inspect/Program.cs 为只读检查游戏程序集的辅助源码。
生成文件位于 work/（工具、依赖、集成测试日志）、各项目 bin/obj/build/.gradle、7dtd-mod/dist/、runtime/。
Phase 0 的 environment_report.md 与 .gitkeep 保留，未改动其他既有项目或游戏文件。

## 架构与功能

Fabric 客户端入口和七日杀 IModApi 在初始化时后台连接本地 Bridge。
Bridge 使用 ASP.NET Core WebSocket 接收 JSON、验证角色和类型、回复 welcome、通知对端上线、双向转发 test 并记录日志。
两端自动输出测试消息接收日志，断线每 3 秒重试。没有世界、玩家、物品、实体或方块操作。

地址 ws://localhost:18771/ws，由 config/network.json 配置；仅本机回环。
详细协议见 phase1_architecture.md。

## 编译与产物

工程根目录执行：

```powershell
.\build.ps1
```

实际结果：Bridge net10.0、七日杀 Mod net48、net48 客户端测试程序均编译成功，0 警告、0 错误；Gradle 8.14.3 build/writeTestClasspath 成功。
Minecraft 本阶段没有 Minecraft 混淆类引用，使用普通 Java Gradle 插件，无需 Loom 重映射。
Gradle Wrapper 的 Java 网络下载在本机多次不完整，已采用 PowerShell 预下载至 Wrapper 缓存并保留官方 SHA-256 验证；统一 build.ps1 已复跑成功。

产物：

- bridge-server/bin/Release/net10.0/BridgeServer.dll
- minecraft-mod/build/libs/mc7dtd-bridge-0.1.0.jar
- 7dtd-mod/dist/MC7DTD-Bridge/MC7DTD.Bridge.dll
- 7dtd-mod/dist/MC7DTD-Bridge/ModInfo.xml

已检查 JAR 包含 fabric.mod.json 与实际入口类。
已执行 tests/prepare-runtime.ps1，将本项目产物放入工程内 runtime/minecraft/mods 与 runtime/7dtd/Mods/MC7DTD-Bridge；没有向现有游戏目录部署。

## 测试方法和结果

```powershell
.\tests\run-phase1.ps1
```

14 项自动检查通过，包括真实 Java/.NET Framework 网络源码双向收发、Bridge 重启重连、非法输入、重复角色、对端离线、分片中文、伪造来源、Phase 2 消息拒绝、消息长度与 Origin 拒绝。
结果：work/phase1-test/results.json，failure=null，gameRuntimeTested=false。
过程：work/phase1-test/process-*.log。测试已停止自身启动的全部子进程。

真实游戏启动顺序：先 .\start-bridge.ps1，再启动隔离 Fabric 实例，最后以工程内 UserDataFolder 启动七日杀。
完整步骤与预期日志见 ../tests/phase1_test.md。
预期 Bridge connected、Minecraft connected、7DTD connected，以及两端 Test received。

## 当前限制

1. 尚未在实际 Minecraft/Fabric 和七日杀 Unity/Mono 中启动验证；自动测试不证明完整游戏内加载兼容性。
2. 只支持一名 minecraft 角色和一名 7dtd 角色，不是多玩家架构。
3. 只做握手及测试消息；无持久队列、可靠投递承诺、认证、TLS 或跨机器访问。
4. net48 Mod 针对本机七日杀 buildid 24994517 的程序集编译，换游戏版本需重编译、复核 API。
5. 七日杀 C# Mod 需要支持 Mod 的非 EAC 本地启动；本项目没有修改反作弊或游戏进程。
6. 只在初始化读取配置，改动后需要重启组件。首次构建需要联网。

## 下一步

建议先按 tests/phase1_test.md 完成真实双游戏 Phase 1 验收，收集日志；此项属于 Phase 1，不等于进入 Phase 2。
当前工作停止，等待用户下一步指示。

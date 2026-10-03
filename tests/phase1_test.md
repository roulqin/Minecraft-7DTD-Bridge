# Phase 1 编译、启动与测试

所有命令在 D:\wenjian\minecraft\7-M 执行。仅测试通信，不进入 Phase 2。

## 编译

```powershell
Set-Location -LiteralPath 'D:\wenjian\minecraft\7-M'
.\build.ps1
```

需要 JDK 21、.NET SDK 10.0.400（global.json 允许同功能带补丁）、联网恢复依赖和本地七日杀程序集。
游戏路径不同时：`.\build.ps1 -GameDir '实际安装目录'`。
net48 参考程序集由 NuGet 获取到 work/，不安装系统开发包。Windows 运行客户端测试需要 .NET Framework 4.8。
Gradle 首次通过 PowerShell 下载并验证官方 SHA-256，然后使用标准 Wrapper。

产物：

- bridge-server/bin/Release/net10.0/BridgeServer.dll
- minecraft-mod/build/libs/mc7dtd-bridge-0.1.0.jar
- 7dtd-mod/dist/MC7DTD-Bridge/MC7DTD.Bridge.dll 与 ModInfo.xml
- tests/client-harness/bin/Release/net48/ClientHarness.exe

## 自动通信测试（不启动游戏）

```powershell
.\tests\run-phase1.ps1
```

测试分配空闲回环端口，配置保存 work/phase1-test/network.json，不改正式端口。启动真实 Bridge、复用 Mod 网络源码的 Java 与 .NET Framework 客户端，验证双向消息和服务重启后重连。
另测非法 JSON/角色/握手、二进制、超长消息、重复角色、离线对端、分片中文、来源伪造、Phase 2 消息拒绝、文本长度、Origin 拒绝。
预期输出 PASS，退出码 0；work/phase1-test/results.json 的 failure 为 null。process-*.log 保存子进程输出；测试清理自身启动的进程。
该测试覆盖真实网络源码，但不覆盖 Fabric/Unity 游戏内加载行为。

## 真实游戏准备（尚未执行）

```powershell
.\tests\prepare-runtime.ps1
```

该脚本只复制本项目产物到工程内 runtime/，不启动游戏或修改原游戏目录。

Minecraft：用启动器建立独立 Minecraft 1.21.11 + Fabric Loader 0.18.4 实例，JDK 选 21，将**实际 gameDir** 设置为 D:\wenjian\minecraft\7-M\runtime\minecraft。注意版本隔离设置，最终 mods 目录必须是该目录下的 mods。本项目 JAR 已由脚本放入其中。
新实例的启动器配置、下载和运行文件也应放在工程 runtime/ 内，不修改现有 Voxy 实例。本 Mod 不要求 Fabric API，若安装它必须匹配 1.21.11。

七日杀：使用下文 UserDataFolder，工程内 runtime/7dtd/Mods/MC7DTD-Bridge 已准备好。
使用支持 C# Mod 的非 EAC 本地模式；ModInfo 有 SkipWithAntiCheat 标记，EAC 模式可能跳过该 Mod。
无需 Harmony 或注入；已有游戏 Mods 仍可能一起加载，本项目不改写它们。

## 启动顺序

### 1. Bridge Server

第一个 PowerShell：

```powershell
Set-Location 'D:\wenjian\minecraft\7-M'
.\start-bridge.ps1
```

预期 Bridge connected、ws://localhost:18771/ws。保持运行，日志写 work/logs/bridge.log（每次启动覆盖）。

### 2. Minecraft

通过启动器启动上文独立 Fabric 实例，无需进入世界。runtime/minecraft/logs/latest.log 应出现：

```text
Bridge connected
Minecraft connected
```

Bridge 控制台也应出现 Minecraft connected。

### 3. 7DTD

Steam 正常登录后，第二个 PowerShell 直接使用非 EAC 游戏入口并指定用户目录和日志：

```powershell
Set-Location 'D:\wenjian\minecraft\7-M'
& 'D:\Steam\steamapps\common\7 Days To Die\7DaysToDie.exe' '-UserDataFolder=D:\wenjian\minecraft\7-M\runtime\7dtd' '-logfile=D:\wenjian\minecraft\7-M\runtime\7dtd\phase1-game.log'
```

若路径不同调整 EXE；若启动器重新启用 EAC，使用游戏官方启动器的非 EAC 模式并保留参数。无需进入存档，Mod 初始化后日志应出现：

```text
[MC7DTD] Bridge connected
[MC7DTD] 7DTD connected
[MC7DTD] Test received from minecraft: Hello from Minecraft
```

Minecraft 日志应出现 Test received from 7dtd: Hello from 7DTD。Bridge 应有 7DTD connected 和双向 Test forwarded。

### 4. 验证与停止

```powershell
Invoke-RestMethod 'http://localhost:18771/health'
```

预期 clients 包含 minecraft、7dtd。Ctrl+C 停止 Bridge，再启动；两端应自动重连并重新交换测试消息。
退出两款游戏后 clients 应为空，关闭 Bridge 完成测试。

## 故障定位与限制

- 端口占用：在 config/network.json 改端口，重启三端，不结束未知进程。
- 没有 Mod 日志：检查实际 gameDir、Mods 层级、ModInfo、版本及 EAC。
- 配置失败：检查两端读取同一工程配置；MC7DTD_ROOT 若设置需正确。
- duplicate_client：关闭重复客户端，每种角色只允许一个。
- Gradle 下载校验失败：保持校验开启，重试 build.ps1；不要使用不匹配的 ZIP。
- 自动测试通过不代表游戏引擎兼容性已验证，游戏内加载失败应检查游戏日志和其他 Mod 冲突。

本次未启动两款游戏，真实双游戏加载、Mono 下 WebSocket 与退出事件仍待手动验收。
不支持玩家/方块同步、多玩家、持久队列、跨机器或公网认证。完成后仍停在 Phase 1。

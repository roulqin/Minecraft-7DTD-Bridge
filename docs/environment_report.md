# MC7DTD-Bridge — Phase 0 环境检测报告

检测日期：2026-10-03（Asia/Shanghai）  
工程根目录：D:\wenjian\minecraft\7-M  
状态：Phase 0 完成；Phase 1 未开始。开发环境部分就绪，尚未进行构建和游戏联调。

## 范围与方法

使用 PowerShell 命令、系统信息、Minecraft 实例 JSON、Fabric API JAR 内元数据、已有游戏日志、Steam 清单及文件存在性检测。
未启动游戏、安装软件、下载依赖、修改环境变量或现有游戏文件。未扫描全部磁盘，“未发现”不代表绝对不存在。

工具会话初始目录与指定工程目录不同；本次所有新增项目文件均位于上述工程根目录。检测前该目录已存在且为空，目录及其上级未发现 AGENTS.md。

## 已安装组件与状态

| 组件 | 实测结果 | 当前状态 |
| --- | --- | --- |
| Windows | Windows 11 家庭版 中文版，10.0.26200，64 位 | 满足平台要求 |
| Java | Oracle Java 21.0.12 LTS，build 21.0.12+7-LTS-205 | java -version 成功 |
| JDK 编译器 | javac 21.0.12 | 满足 JDK 21 要求 |
| .NET SDK | 10.0.400，MSBuild 18.9.6 | 满足 SDK 8+；未发现 SDK 8 |
| .NET Runtime | NETCore 与 ASP.NET Core 8.0.30、10.0.11，另有部分 7/9 运行时 | Runtime 不等于 SDK，也不证明七日杀 Mod 框架兼容 |
| Git | 2.55.0.windows.5 | 可执行 |
| Gradle | PATH 中未发现 | 工程也没有 Wrapper，构建入口待补齐 |
| Minecraft Java Edition | 1.21.11，实例 1.21.11-Voxy | 实例 JSON、游戏 JAR 和历史启动日志存在 |
| Fabric Loader | 0.18.4 | 实例元数据及历史日志确认 |
| Fabric API | 0.140.2+1.21.11 | 实际 JAR 内 fabric.mod.json 确认 |
| 7 Days to Die | Steam app 251570，buildid 24994517，public 分支 | EXE、Assembly-CSharp.dll、Mods 目录存在；菜单版本待确认 |
| Visual Studio | Community 2022 17.14.40；Community 2026 18.9.2 | vswhere 显示完整、可启动；具体工作负载未核验 |
| VS Code | code 不在 PATH，常见用户级及系统级路径未发现 | 可选；自定义路径未排除 |
| WinGet | 命令存在 | 未运行安装 |

### 实际路径

- Java/Javac 命令入口：C:\Program Files\Common Files\Oracle\Java\javapath\
- JDK：C:\Program Files\Java\jdk-21.0.12，bin\javac.exe 存在。
- .NET：C:\Program Files\dotnet\dotnet.exe
- Git：D:\wenjian\xuexi\Git\cmd\git.exe
- Minecraft 实例：D:\wenjian\minecraft\.minecraft\versions\1.21.11-Voxy
- Fabric API：上述实例的 mods\fabric-api-0.140.2+1.21.11.jar
- 七日杀：D:\Steam\steamapps\common\7 Days To Die
- 七日杀程序集：上述目录的 7DaysToDie_Data\Managed\Assembly-CSharp.dll
- Steam 清单：D:\Steam\steamapps\appmanifest_251570.acf

默认 %APPDATA%\.minecraft 仅发现 runtime；实际游戏在自定义目录。报告未保存账号标识、令牌或存档。

### Minecraft 兼容性依据

实例 mainClass 为 net.fabricmc.loader.impl.launch.knot.KnotClient，javaVersion.majorVersion 为 21，intermediary 为 1.21.11，fabric-loader 为 0.18.4。游戏 JAR 大小为 31,152,600 字节。

Fabric API 元数据依赖如下，现有版本符合这些声明：

```json
{
  "fabricloader": ">=0.17.3",
  "java": ">=21",
  "minecraft": ">=1.21.11- <1.21.12-"
}
```

已有 latest.log 包含 `Loading Minecraft 1.21.11 with Fabric Loader 0.18.4`。
这是历史启动证据，不代表本次启动验证成功或全部现有 Mod 均兼容。

### Git 与环境变量

工程目录执行 git rev-parse --show-toplevel 返回 not a git repository。本次仅初始化目录结构，未执行 git init、提交或配置远程仓库。

JAVA_HOME 与 GRADLE_HOME 未设置。Java/javac 可通过 PATH 调用；JAVA_HOME 未设置本身不代表 JDK 不可用。后续构建需确认实际使用 JDK 21。

## 缺失组件、安装方式与待确认事项

1. **Gradle 构建入口待补齐。** Phase 1 确定 Minecraft/Fabric Loom 版本后，采用兼容的 Gradle Wrapper 并保存到项目。已有 Wrapper 的项目无需全局安装 Gradle。如需安装，按照 [Gradle 官方安装说明](https://docs.gradle.org/current/userguide/installation.html) 下载兼容版本并设置 PATH；参见 [Wrapper 文档](https://docs.gradle.org/current/userguide/gradle_wrapper.html)。本阶段未生成 Wrapper。
2. **JDK 21 已满足。** 无需重装；重建机器时可从 [Oracle 官方 JDK 下载页](https://www.oracle.com/java/technologies/downloads/#java21) 获取 Windows x64 JDK，并按需设置 JAVA_HOME。本次未修改环境变量。
3. **.NET SDK 8+ 已满足。** 如后续需指定版本，按 [Microsoft Windows 安装文档](https://learn.microsoft.com/dotnet/core/install/windows) 安装 SDK，不能仅安装 Runtime。Bridge 的目标框架与 SDK 固定策略待确定；七日杀 Mod 的目标框架需根据游戏运行时和引用程序集确认，不能直接由系统 SDK 版本推断。
4. **Minecraft/Fabric 已发现。** 如需独立测试实例，可使用 [Fabric 官方安装器](https://fabricmc.net/use/installer/) 选择目标 Minecraft/Loader，再将匹配版本的 Fabric API 放入实例 mods。本阶段未选定项目最终支持版本，未修改现有 Voxy 实例。
5. **Git 已满足。** 新环境可参照 [Git Windows 安装页](https://git-scm.com/install/windows)。仓库初始化和远程配置尚未执行。
6. **七日杀运行和 Mod 兼容性待验证。** 已找到安装文件，但未确认游戏显示版本、世界加载、Mod 入口和目标框架。若安装损坏，可通过 Steam 库验证文件完整性；本次未执行修复。
7. **VS Code 可选。** 已有 Visual Studio，不阻塞 Phase 0；如需安装，可使用 [VS Code 官方网站](https://code.visualstudio.com/)。

以上安装说明是后续选项，不代表已经执行安装。

## 文件变更与功能说明

Created（相对工程根目录）：

- README.md
- docs/environment_report.md
- minecraft-mod/.gitkeep
- 7dtd-mod/.gitkeep
- bridge-server/.gitkeep
- config/.gitkeep
- tests/.gitkeep

新增目录：minecraft-mod、7dtd-mod、bridge-server、config、docs、tests。Modified：无。

只创建结构与文档，明确各组件职责并记录可复核的环境事实，供后续选定兼容构建版本；未实现通信、Mod 或映射配置。

## 启动、验证与预期结果

Phase 0 无应用可启动。在 PowerShell 中执行：

```powershell
Set-Location -LiteralPath 'D:\wenjian\minecraft\7-M'
Get-ChildItem -Force
Get-Content '.\docs\environment_report.md' -Encoding utf8
java -version
javac -version
dotnet --info
dotnet --list-sdks
git --version
Get-Command gradle -ErrorAction SilentlyContinue
git rev-parse --show-toplevel
```

预期：六个目录和 README 存在，报告可读取；Java/javac 为 21.0.12，SDK 包含 10.0.400，Git 为 2.55.0.windows.5。Gradle 查询无输出，仓库查询返回 not a git repository，与本次状态一致。

本次实际执行了版本命令、元数据与文件存在性检测，并复核了新增目录和报告。未执行编译、Gradle 构建、游戏启动、通信或性能测试。
当前不会显示 Minecraft connected / Bridge connected / 7DTD connected，这属于 Phase 1 验收。

## 下一阶段建议

用户确认后，Phase 1 先固定 Minecraft/Fabric/API/Loom/Gradle 版本组合，核对七日杀运行时及 Mod API，再实现最小 JSON 接收、验证、转发与日志，以及三端连通性测试。

停止点：Phase 0。未经用户确认，不进入 Phase 1。

# Phase 3.8.2.2 Hotfix — Entity Debug Navigation Tools

实现完成；未进入下一阶段。未执行游戏内人工验收、未关闭当前游戏/Bridge、未覆盖运行中的游戏 Mod。

## 修改文件列表

项目根目录：`D:\wenjian\minecraft\7-M`。

| 分类 | 文件 | 变更 |
|---|---|---|
| Minecraft | `minecraft-mod/src/main/java/io/mc7dtd/EntityDebugNavigation.java` | 新增：实体选择、距离排序、作用域检查、偏移搜索、Follow 状态 |
| Minecraft | `minecraft-mod/src/main/java/io/mc7dtd/MinecraftDebugNavigation.java` | 新增：四个客户端命令、集成服务器传送、安全落点检查、客户端 tick |
| Minecraft | `minecraft-mod/src/main/java/io/mc7dtd/DebugConfig.java` | 扩展可选 navigation 配置；兼容旧两字段构造器及配置 |
| Minecraft | `minecraft-mod/src/main/java/io/mc7dtd/MinecraftBridgeMod.java` | 注册导航命令和 tick |
| Components | `components/debug/DebugConfig.cs` | 兼容、校验可选 debug_navigation，不改变已有调试字段行为 |
| Config | `config/debug.json` | 新增 debug_navigation=true，原有两开关仍为 false |
| Config | `config/debug.schema.json` | 定义可选 boolean 字段；缺省关闭 |
| Tests | `minecraft-mod/src/test/java/io/mc7dtd/EntityNavigationHarness.java` | 新增导航自动测试 |
| Tests | `tests/run-phase3_8_2_2-navigation.ps1` | 新增独立 Hotfix 构建及测试脚本 |
| Tests | `tests/debug-runner/Program.cs` | 增加共享 C# Debug Config 兼容与非法值测试 |
| Tests | `tests/launch-phase3_8_2_2.ps1` | 增加可选 BuildConfiguration，原默认行为不变 |
| Docs | `docs/phase3_8_2_2_debug_navigation_hotfix.md` | 本报告 |

共新增 5 个文件、修改 7 个文件。未修改 Entity/Equipment 协议、Authority、Presentation、Bridge 通信、7DTD 采集器或 Appearance 渲染实现。共享 C# 配置解析器调整仅用于避免新字段导致整个 debug.json 被拒绝。

## 命令行为

- `/mc7dtd_entity_nearest`：读取当前同步的 7DTD 实体，按 Minecraft 空间中的三维欧氏距离选最近实体，输出 ID、类型、位置和距离。
- `/mc7dtd_entity_list`：列出具有 Minecraft 映射坐标的远端同步实体，按距离排序；排除本地 Minecraft 玩家。空列表明确输出 none。
- `/mc7dtd_entity_teleport <id>`：检查唯一 ID，对重复跨世界/stream ID 拒绝执行；停止当前 Follow，在集成服务器线程上将玩家传送到安全落点。
- `/mc7dtd_entity_follow <id>`：启动/切换跟踪目标，同一 ID 再执行一次停止；每 10 个客户端 tick 更新一次安全落点。目标 despawn、Inspector reset、已收到 peer_unavailable 或 Minecraft 世界切换时停止。跟踪绑定完整 source/world/dimension/id/stream，不继承新 stream。

四个命令只在启动时读取的 `debug_navigation=true` 时注册。false、缺省、配置读取失败时均不可用。该开关独立于 debug_name_tag/debug_logging。修改配置后须重启 Minecraft，不提供热加载。

Inspector 是只读事实镜像，没有增加新的网络消息或 revision。既有位置采样会自然反映传送后的本地玩家位置，未修改该采样或通信逻辑。

## 安全落点

候选点：实体水平偏移 3、4、5 格，八个方向；初始高度为 entity.y+3。集成服务器查询地表高度后，将落点调整到附近地面，而非直接把玩家悬空放在 y+3。

检查世界边界、世界高度、完整玩家碰撞空间、脚下支撑、流体及常见危险块（岩浆块、仙人掌、火、营火、细雪、甜浆果丛）。候选地表与实体高度差超过 8 格时拒绝。全部候选不安全则取消，输出原因。实体旁水平偏移避免与代理重叠。

Teleport/Follow 当前限定单人世界的集成服务器。多人远程服务器明确拒绝，不尝试客户端 setPosition 导致回弹，也不自动申请 OP/作弊权限。Nearest/List 不需要集成服务器。

Follow 使用现有连接可用性信号。源端异常掉线但尚未收到现有通知时，仍有原系统通知延迟；未新增 Bridge 心跳。

## 编译结果

| 项目 | 结果 |
|---|---|
| Minecraft | build PASS，真实 Minecraft 1.21.11 API 编译通过 |
| Bridge | build PASS，仅共享 DebugConfig 兼容调整；通信未修改 |
| 7DTD | build PASS，仅共享 DebugConfig 兼容调整；采集未修改 |
| C# debug-runner | build PASS |

独立 C# 配置：`Phase3822Navigation`，避免正在运行的 Phase3822 DLL 文件锁。构建日志位于 `work/phase3822-navigation-test/`。首次尝试旧配置的构建曾因运行中的 DLL 锁失败，改用独立目录后通过；未强制结束旧进程。

## 测试结果

```text
Nearest Entity PASS
Entity List PASS
Teleport安全偏移 PASS
Follow启动/停止 PASS
Debug关闭禁止使用 PASS
```

导航自动测试 22 项；既有 Java 回归 171 项；C# Debug/Inspector 集成回归 44 项。包括：空列表、三维距离、替代偏移、无落点拒绝、Follow 位置更新/停止/清理、peer_unavailable 保留事实但停止导航、跨 scope ID 歧义拒绝、本地玩家排除、事实组件及 revision 不变、旧配置兼容、新配置独立开关、非法配置关闭。

Java 导航测试验证实际查询与偏移策略，使用落点判定替身覆盖安全/阻塞路径。Minecraft 具体碰撞、地表与传送 API 已编译通过，但尚未通过游戏人工运行验证；自动测试 PASS 不等同于实际游戏传送验收。

复现：`powershell -File .\tests\run-phase3_8_2_2-navigation.ps1`。

## 启动与人工检查

正常保存并退出旧游戏，关闭旧 Bridge 后，在项目目录执行：

```powershell
.\tests\launch-phase3_8_2_2.ps1 -BuildConfiguration Phase3822Navigation
```

该脚本会部署本次构建的 Minecraft jar 和 7DTD DLL。当前运行的旧实例不会自动加载新命令。

1. 进入两个既有测试世界，等待连接及代理 spawn。
2. Minecraft 执行 `/mc7dtd_entity_list`、`/mc7dtd_entity_nearest`，复制显示的完整实体 ID。
3. `/mc7dtd_entity_teleport <id>`，确认安全落在对应青色 7DTD 代理旁，观察装备标记。
4. `/mc7dtd_entity_follow <id>`，在 7DTD 移动或切换装备；确认 Minecraft 跟随附近安全地面，观察标记变化。
5. 再执行同一 Follow 命令停止；退出 7DTD 世界确认停止，重进后使用新列表 ID。
6. 设置 debug_navigation=false 并重启 Minecraft，确认四个导航命令不存在；恢复 true 后重启可用。

此 Hotfix 停止于 Phase 3.8.2.2，不包含下一阶段工作。

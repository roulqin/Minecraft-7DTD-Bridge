# Phase 3.8.2.2.1 Entity Navigation Fix

完成范围：Minecraft Debug Command 层命令注册、参数处理、补全与测试。未修改 Entity/Equipment 协议、Presentation、Authority、Bridge 通信、7DTD 采集器或安全传送后端。

## 原因和修复

原实现仅注册 `mc7dtd_entity_teleport`，没有注册 `mc7dtd_entity_goto`。因此 goto 会被判定为未知命令。Brigadier 的 `StringArgumentType.word()` 支持 UUID 的连字符；本次使用实际 Brigadier dispatcher 自动执行完整 UUID 验证了这一点。

现在注册 `mc7dtd_entity_goto <entity_id>`，同时保留旧 teleport 别名。两个命令使用同一命令树工厂、同一查找及安全传送流程。

参数限定完整 8-4-4-4-12 十六进制 UUID，允许大写输入并规范化为小写；拒绝 Java UUID.fromString 接受的某些缩写。非法输入提示 `Invalid UUID: ...`。未知实体提示：

```text
Entity not found: <UUID>
Current synced entities: <数量>
```

数量为导航当前可用的远端同步实体数量，与导航 list 的筛选一致，不包含本地 Minecraft 玩家。跨 world/stream 存在相同 ID 时保持原有歧义拒绝行为。

在实体 ID 参数处提供当前列表的 UUID 补全，支持前缀过滤；断线后列表清空时补全也清空。配置关闭时 goto 和 teleport 均不注册。

## 修改文件列表

项目根目录：`D:\wenjian\minecraft\7-M`。

| 文件 | 变更 |
|---|---|
| `minecraft-mod/src/main/java/io/mc7dtd/MinecraftDebugNavigation.java` | 修改一处命令注册，调用共享 goto/teleport 命令树及 UUID 查找 |
| `minecraft-mod/src/main/java/io/mc7dtd/EntityGotoCommand.java` | 新增命令树、补全、完整 UUID 校验、未知实体提示 |
| `minecraft-mod/src/test/java/io/mc7dtd/EntityGotoHarness.java` | 新增真实 Brigadier 解析测试 |
| `tests/run-phase3_8_2_2_1.ps1` | 新增 Minecraft 构建及 10 个 Java harness 验证脚本 |
| `docs/phase3_8_2_2_1_entity_navigation_fix.md` | 本修复报告 |
| `docs/phase3_8_3_0_renderer_adapter_design.md` | 同条用户请求的独立 Renderer 设计文档；无 Runtime 实现 |

## 编译与测试

Minecraft build PASS，使用真实 Fabric/Minecraft 依赖编译。Bridge 和 7DTD 源码及配置本轮未修改，未重复编译。

```text
Entity Goto UUID PASS
Entity Not Found PASS
Invalid UUID PASS
Safe Teleport Regression PASS
Debug Disable Regression PASS
```

Goto 新增 13 项，既有 Navigation 22 项，既有 Appearance/Equipment/Health/Identity/Presentation/NativeProxy 等 171 项，共 206 项通过。

Goto 测试使用生产代码中的共享命令树注册到真实 Brigadier dispatcher，验证实际执行及 suggestions，未使用手写字符串切割替代框架测试。额外验证旧 teleport 别名、UUID 大写、缩写拒绝、空列表提示、事实只读。

安全回归：原 `teleport`、`landing`、`safe` 后端整段源码与修改前逐字一致，编译通过；既有安全偏移/无安全落点/Follow 测试通过。未执行游戏内传送人工验收，不将这些测试视为地形碰撞实机验收。

## 使用方法

确保 `config/debug.json` 中 `debug_navigation=true`。正常保存退出旧实例并停止旧 Bridge，再执行现有 Hotfix 启动方式加载最新 jar：

```powershell
.\tests\launch-phase3_8_2_2.ps1 -BuildConfiguration Phase3822Navigation
```

进入两个世界，等待同步后：

```text
/mc7dtd_entity_list
/mc7dtd_entity_goto <列表里的完整 entity_id>
```

例如 UUID 格式：

```text
/mc7dtd_entity_goto 3b7424bc-4917-4f19-8f46-dd2882f09825
```

示例 UUID 仅说明格式，实际必须存在于当前列表。输入 goto 后空格，按 Tab 可补全 UUID。

保持原限制：传送只用于单人集成服务器，地面/碰撞/流体/边界检查与 Follow 行为不变，找不到安全落点取消。false 后重启 Minecraft，新导航命令不可用。

复现自动测试：`powershell -File .\tests\run-phase3_8_2_2_1.ps1`。

完成后停止。3.8.3.0 仅输出文档，不进入 3.8.3.1。

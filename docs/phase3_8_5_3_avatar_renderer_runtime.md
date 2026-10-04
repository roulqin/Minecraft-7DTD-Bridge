# Phase 3.8.5.3 AvatarRenderer Runtime Report

状态：开发完成；自动验证 PASS。真实世界人工验收待执行，未宣称人工验收 PASS。

## 1. 实现结果

7DTD 接收 Minecraft 玩家代理时，现有 MarkerController 经 DebugProxyScene → AvatarProxyScene → AvatarRenderer 创建 MinecraftAvatarPrefab。只替换本地视觉对象；Entity ID、接收顺序、映射后的 Transform、Registry 生命周期、Authority、Health、Identity、Equipment 和 Presentation 数据没有新增写入或协议变化。

AvatarRenderer 提供 CreateAvatar / UpdateAvatar / RemoveAvatar。复用 Phase 3.8.5.2 Windows AssetBundle，读取 assets/avatar/avatar_config.json 的 model、variant、skin、可选 skeleton，支持 minecraft_humanoid / alex_slim / minecraft_avatar_v1。本地 PNG 为 64×64，采用点采样、Clamp、无 Mipmap；实例独立 Texture/Material，基础层和第二层共用实例皮肤，保持原资源 UV、透明裁剪和骨架。

身体根节点只应用共享坐标约定对应的负 Unity Yaw，忽略身体 Pitch/Roll。rig_head 在 Animator 之后的 LateUpdate 应用 Pitch，范围 ±90°。Animator 固定 speed=0 / Idle，无位置驱动的动作同步。

资源缺失、配置不支持或 Prefab 加载失败时回退原 UnityPlayerProxyScene 几何代理；PNG 缺失或损坏时保留 Prefab 内置默认皮肤，并报告 fallback。删除、断线、世界退出和游戏关闭清理根对象、本地装备、独立材质、纹理以及共享 Bundle 引用；最后一个 Avatar 移除后卸载 Bundle，下次生成重新加载。Unity API 限制在主线程执行。

## 2. Equipment 与 Inspector 范围

根据用户确认，新增 7DTD LocalAvatarEquipmentAdapter，只以本地测试输入生成 RightHand 标记。支持 woodenClub、torch、ironAxe 的不同颜色标记、替换和移除，禁用碰撞。不属于真实物品模型或跨端装备同步。

现有 Minecraft ItemDisplay Renderer 保持原实现。Minecraft → 7DTD 消息没有 held_item，本阶段没有扩展协议、Bridge、Equipment 或采集器。

7DTD F1 控制台新增：

- `mc7dtd_avatar_inspect [entity_id]`：读取当前 Avatar 派生状态和 7DTD 世界坐标，不修改任何状态。
- `mc7dtd_avatar_test_held <entity_id> <7dtd:woodenClub|7dtd:torch|7dtd:ironAxe|null>`：仅本地标记测试，要求 config/debug.json 的 debug_navigation=true；关闭、缺失、错误类型、重复字段或无效 JSON 均拒绝。

Inspector 包含 entity_id、position、avatar_renderer.status/reason/model/variant/skin/animator，以及明确标为 held_item_local_test 的挂载状态。此信息属于 7DTD 本地运行时，未新增网络 Component，也没有将其伪装为 Minecraft 端远程 Inspector 状态。

## 3. 修改文件列表

项目根目录：D:/wenjian/minecraft/7-M

### 7DTD Runtime

- 新增 7dtd-mod/src/AvatarRenderer.cs：配置、Prefab、PNG、资源引用、生命周期、头部姿态。
- 新增 7dtd-mod/src/AvatarProxyScene.cs：适配现有 IRotatingProxyScene。
- 新增 7dtd-mod/src/LocalAvatarEquipmentAdapter.cs：用户确认的本地挂点适配器。
- 新增 7dtd-mod/src/AvatarInspectorCommand.cs：本地 Inspector 与 Debug 测试命令。
- 修改 7dtd-mod/src/BridgeMod.cs：替换玩家视觉场景、关闭时 Dispose。
- 修改 7dtd-mod/MC7DTD.Mod.csproj：增加本机 Unity Animation、AssetBundle、ImageConversion 和 Newtonsoft 引用。

### Tests / Launch

- 新增 tests/avatar-runtime-smoke/AvatarRuntimeSmoke.csproj
- 新增 tests/avatar-runtime-smoke/RuntimeSmoke.cs
- 新增 tests/avatar-runtime-smoke/ModInfo.xml
- 新增 tests/verify-phase3_8_5_3-game.ps1
- 新增 tests/run-phase3_8_5_3.ps1
- 新增 tests/launch-phase3_8_5_3.ps1
- 新增 tests/launch-avatar-game.ps1

### Docs

- 新增 docs/phase3_8_5_3_avatar_renderer_runtime.md

资源文件、Minecraft 源码、Bridge 源码、Entity / Equipment 协议、Authority、Presentation Component、7DTD 装备采集器未在本阶段修改。仓库原先已存在其他阶段的未提交修改，不能用 git diff 与 HEAD 的全部结果代表本阶段变更。

## 4. 编译结果

- 7DTD build PASS：Phase3853，0 warning / 0 error。
- Bridge build PASS：Phase3853，源码未修改，0 warning / 0 error。
- Minecraft build PASS：Gradle BUILD SUCCESSFUL，源码未修改。
- 隔离 7DTD 引擎测试 Mod build PASS。

产物：7dtd-mod/bin/Phase3853/net48/MC7DTD.Bridge.dll。

## 5. 测试结果

实际安装的 7DTD V3.2.0 b10 / Unity 2022.3.62f2 引擎执行本地样本；无需进入世界，不连接 Bridge。测试 Mod 编译真实 Runtime 源码和现有 MarkerController，按既有接收消息构造 spawn/update/despawn。主 Mod 的游戏世界网络联调仍属于后续人工验收。

| 检查 | 结果 |
|---|---|
| Spawn Avatar / Update Avatar / Remove Avatar | PASS |
| Entity ID 保留 / Presentation 不变 | PASS |
| Load PNG / Layer2 资源绑定 | PASS |
| Position Follow / Yaw Follow | PASS |
| Pitch Head Only / Animator 后 LateUpdate | PASS |
| Right Hand Anchor / 无碰撞 | PASS |
| 本地装备替换 / 空手移除 / 未知物品 | PASS |
| Disconnect Cleanup / World Change Cleanup | PASS |
| 无世界不生成 / 重复删除 / 重生资源重载 | PASS |
| 缺失 Skin / 缺失 Bundle fallback | PASS |
| 多实例共享资源寿命 / 缓存清理 / 延迟销毁 | PASS |
| 工作线程拒绝 / Debug 开关与非法配置拒绝 | PASS |

引擎测试共 31 项 PASS。Minecraft 12 个 Harness 共 261 项 PASS，覆盖资源、Renderer、导航、Appearance、Equipment、Health、Identity、Presentation 和 Native Proxy。现有玩家代理/实际 WebSocket 传输回归 26 项 PASS，包括断线回调清理。三个新增启动/执行脚本已通过 PowerShell 语法检查；尚未运行三端人工启动脚本。

测试证据：work/phase3853-test/7dtd-runtime-results.txt、7dtd-runtime-smoke.log、runtime-harness-build.log、7dtd-build.log、bridge-build.log、player-proxy-regression.log，以及 work/phase3841-test/results.json / minecraft-build.log。

缺失 Bundle 测试会故意触发 Unity 资源加载失败；游戏自动退出期间还出现 Xbox Live 未初始化的 SDK 日志。这些不是 Avatar 测试异常；未发现未预期的 Avatar Runtime 错误。

## 6. 实机验收步骤

1. 正常退出旧 Minecraft、7DTD，停止旧 Bridge。在项目根目录 PowerShell 执行 `./tests/launch-phase3_8_5_3.ps1 all`，或分别传入 bridge、minecraft、7dtd。三端进入已有测试世界，等待连接与 Minecraft 玩家 snapshot。
2. 在 **7DTD** 按 F1，执行 `mc7dtd_avatar_inspect`，记下 Minecraft 玩家 entity_id、position；应看到 model=minecraft_alex、variant=alex_slim、status=active、animator=idle。
3. 在 **7DTD** 观察 Minecraft 来源代理，不是本地 7DTD 玩家，也不是 Minecraft 中的 7DTD 来源代理。必要时用 7DTD 原生命令 `teleport <x+2> -1 <z+2>` 靠近 Inspector 坐标；-1 表示地面。确认 Alex 比例、基础层和外层皮肤可见。
4. Minecraft 玩家移动、左右转头、上下看。7DTD Avatar 位置/Yaw 跟随；上下看只转头，身体不俯仰。基础 Idle 保持，不期待 Walk/Run 跟动作同步。
5. debug_navigation=true 时，在 7DTD 执行 `mc7dtd_avatar_test_held <id> 7dtd:woodenClub`，再切 torch、ironAxe，检查右手标记跟随骨架；执行 `mc7dtd_avatar_test_held <id> null` 后消失。该步骤明确是本地测试，不应通过切换 Minecraft 手持来期待本阶段同步。
6. Minecraft 玩家退出再进入世界，检查旧 Avatar 消失且新 Avatar 按 snapshot 重建。7DTD 退出再进入世界，或断开/重连 Bridge，检查无残留显示对象，重连后重新生成。
7. 正常退出 7DTD，启动辅助进程自动恢复 Steam 全局 MC7DTD.Bridge 的 ModInfo.xml，避免测试路径与全局路径重复加载。辅助进程被强制结束时，先确认 7DTD 已退出，再将全局 Mods/MC7DTD.Bridge/ModInfo.xml.avatar-runtime-disabled 手动恢复为 ModInfo.xml。

## 7. 已知限制与停止点

- 人工游戏世界视觉验收尚未执行；引擎测试不能代替双世界的肉眼验收。
- 本地装备为挂点验证标记，跨端 Minecraft → 7DTD 装备同步留到后续独立阶段。
- 仅支持现有 Alex Slim 64×64 资源；不支持多人 Skin 服务、实时皮肤热更新、自定义模型动态切换。配置/皮肤在新 Avatar 创建时读取。
- Animator 只运行 Idle，无动作/攻击/表情同步；本阶段不实现 EntityAlive、AI、碰撞或代理战斗模型。
- 保持 Phase 3.8.5.3，停止，不进入下一阶段。

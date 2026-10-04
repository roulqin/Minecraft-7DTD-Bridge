# Phase 3.8.7 Avatar Action State Layer v1 Report

**状态：PASS。** 验收日期：2026-10-04。本地动作框架、推导、Animator 和 Inspector 已完成；人工观察确认 Idle／Walk／Run 正确、Jump／Fall 可见、落地恢复正常。

## 实现

新增 `AvatarActionState`，由每个 7DTD AvatarRenderer.Handle 独立持有。读取既有插值位置、水平／竖直速度和本地地面支撑检测，推导 idle、walking、running、jumping、falling。保留 nullable 本地 sneaking 输入接口；当前远端数据无法可靠判断潜行，默认不启用。

新增独立 Jump／Fall 动画资源、Controller 状态和 `speed`、`isGrounded`、`verticalVelocity`、`actionState` 参数。以0.08秒过渡进入空中动作，落地后回到 Idle／Walk／Run。保持 Root Motion 关闭，动作曲线不写 Root／Head，既有插值、Yaw、Head Pitch 和 RightHand 流程保留。

Inspector 增加 `avatar_action`；实际 Controller 状态仍可通过 `avatar_renderer.animator` 对照。实体事实、视觉动作状态和实际动画状态分别展示。完整规则见 [设计与运行说明](phase3_8_7_avatar_action_state.md)。

## 修改文件列表

路径相对 `D:/wenjian/minecraft/7-M`。带 SHA256 的清单见 [modified-files.json](modified-files.json)。清单只针对本阶段，不把工作区中此前已有的未提交修改计入。

| 类别 | 文件 |
|---|---|
| Runtime 新增 | `7dtd-mod/src/AvatarActionState.cs` |
| Runtime 接入／Inspector | `7dtd-mod/src/AvatarRenderer.cs` |
| 资源生成 | `tests/produce-avatar-resources.py`、`assets/avatar/production_unity/Assets/Avatar/Editor/AvatarResourceBuild.cs`、`AvatarProductionSpec.generated.cs` |
| 动画源 | `assets/avatar/animations/Jump.clip.json`、`Fall.clip.json`、`MinecraftAvatarAnimator.source.json` |
| Unity 生成资源 | Generated 目录中的 Jump／Fall Clip、Animator Controller、Prefab 及关联生成资产；`assets/avatar/bundles/windows/minecraft_avatar_v1` 和伴随 manifest；`assets/avatar/resource_manifest.json` |
| 新原生测试 | `tests/avatar-action-smoke/AvatarActionSmoke.csproj`、`ModInfo.xml`、`RuntimeSmoke.cs`、`MotionChecks.cs`、`PolishChecks.cs`、`ActionChecks.cs` |
| 旧测试链接兼容 | `tests/avatar-motion-smoke/AvatarMotionSmoke.csproj`、`tests/avatar-polish-smoke/AvatarPolishSmoke.csproj`、`tests/avatar-runtime-smoke/AvatarRuntimeSmoke.csproj`，只增加 Action 类型链接 |
| 脚本 | `tests/build-phase3_8_7-resources.ps1`、`verify-phase3_8_7-game.ps1`、`run-phase3_8_7.ps1`、`launch-phase3_8_7.ps1` |
| 文档 | `docs/phase3_8_7_avatar_action_state.md`、`docs/phase3_8_7_runtime_acceptance.md` |

本阶段未修改 AvatarAnimationState、Interpolation／Ground Alignment 实现、Entity 协议、Bridge、Authority、Presentation、Equipment 或 Component revision。未增加网络字段。

## 编译与测试

| 验证 | 结果 | 证据 |
|---|---|---|
| 7DTD Phase387 build | PASS，0 warning／0 error | [日志](7dtd-build.log) |
| Bridge Phase387 build | PASS，0 warning／0 error；源码未改 | [日志](bridge-build.log) |
| Minecraft build | PASS；源码未改 | [日志](minecraft-build.log) |
| Unity 资源构建 | 28 PASS | [逐项结果](unity-resource-results.txt) |
| 7DTD 原生引擎 | **116 PASS**，含此前91项及新增25项 | [逐项结果](7dtd-runtime-results.txt) |
| Minecraft 12 Harness 回归 | 261 PASS | [结果](minecraft-regression.log) |
| Proxy／真实 WebSocket 回归 | 26 PASS | [结果](player-proxy-regression.log) |
| 受保护源码哈希 | 72文件、0变化 | [结果](protected-source-results.json) |

要求项目逐项通过：Idle Detect PASS；Walk Detect PASS；Run Detect PASS；Jump Detect PASS；Fall Detect PASS；Idle／Walk／Run／Jump／Fall Action Transition PASS；Spawn PASS；Remove Cleanup PASS。另验证落地转 Walk、顶点保持空中、Teleport 不当成 Jump、无地面不判定 Idle、未知潜行输入安全、非法数字／旧时间忽略，以及实际 Renderer 删除后 Action 数据清零。

Entity、Authority、Presentation、Equipment 与 Interpolation 回归通过。实际 Unity 运行生成的 Inspector JSON 可复核派生状态与已选中的原生 Animator 状态：

- [Idle](action-inspector-idle.json)
- [Walk](action-inspector-walk.json)
- [Run](action-inspector-run.json)
- [Jump](action-inspector-jump.json)
- [Fall](action-inspector-fall.json)

Jump／Fall 的 [资源预览](jump.png) 和 [资源预览](fall.png)来自 Unity Editor 验证，不冒充人工实机动作截图。

## 人工验收结果

三端使用 `tests/launch-phase3_8_7.ps1` 启动，7DTD V3.2.0 b10、Minecraft 1.21.11。用户进入既有世界完成动作观察；受限 computer-use 记录静止截图、查询 Inspector 并正常退出。用户明确回复：**“Idle／Walk／Run 正确，Jump／Fall 可见，落地恢复。”**

| 项目 | 结果 | 依据 |
|---|---|---|
| Idle／Walk／Run | PASS | 用户实机确认 |
| Jump／Fall | PASS | 用户实机确认 |
| 落地恢复 | PASS | 用户实机确认；[动作测试后截图](01-avatar-after-action-test.png) |
| Inspector 本地状态 | PASS（自动验证） | 五份实际原生运行 JSON；人工控制台持续日志把长输出滚走，[截图](02-inspector-render-objects.png)只清楚保留显示对象和地面字段，不宣称截图捕获了完整 Action 字段 |
| 正常退出清理 | PASS | 18:54:08 `world_unloaded`，proxy count=0；[空列表截图](03-cleanup-empty.png)、[日志](manual-lifecycle.log) |
| 世界退出 | PASS | 18:56:35 `World.Cleanup`，正常关闭7DTD；原生自动测试进一步确认 Action 缓存清零 |

## 人工复测步骤

1. 正常关闭旧 Minecraft、7DTD 和 Bridge，确认18771端口空闲。
2. 在项目根目录执行 `.\tests\launch-phase3_8_7.ps1`，进入两个既有测试世界。
3. 在7DTD靠近 Minecraft Avatar，Minecraft依次停止、慢走、快速移动，确认 Idle／Walk／Run。
4. Minecraft跳跃、从安全的小段高度落下，观察 Jump／Fall 和落地恢复；既有约0.55秒插值延迟仍存在。
5. 在 **7DTD F1 原生控制台**执行 `mc7dtd_entity_inspect` 或 `mc7dtd_avatar_inspect`，检查 `avatar_action` 与 `avatar_renderer.animator`。Minecraft端同名 slash Inspector 不会读取7DTD本地 Action；本阶段没有为此改协议。
6. 退出Minecraft世界，确认Avatar消失、Inspector为空。自动测试已验证重建与旧状态清理。

## 限制与完成边界

- grounded 与动作是本地估计，不是源游戏动作事实。台阶、攀爬、飞行等竖直变化可能被判定为 Jump／Fall。
- 约500ms的既有采样可能漏掉短跳或延后落地。单纯上升后静止0.85秒、下降到不同高度后静止0.25秒且有支撑时恢复 grounded，避免状态永久悬挂。
- 没有可用远端潜行数据，sneaking只保留接口。Jump／Fall为基础四肢动作，不包含攻击、武器、战斗或表情。
- 当前完整 Inspector 较长，持续 Debug 日志可能使前面的字段不易保留在截图中；数据验证使用真实原生 Inspector JSON。

测试实例已退出，18771端口已释放，全局 ModInfo 已恢复。未实现攻击／伤害／Inventory／Equipment跨端同步，未提交Git，未进入下一阶段。等待下一步指令。

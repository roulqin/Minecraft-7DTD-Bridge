# Phase 3.8.5.4 / 3.8.6 Avatar Motion Polish Runtime Report

状态：PASS。验收日期：2026-10-04。已完成本地视觉优化及退出／重进验证，可作为 Phase 3.9 的基础；本次未实现 Inventory、跨端 Equipment 或 Combat。

## 实现结果

- Material Pipeline：Base／Layer2 使用 Cutout、深度写入和相同 Alpha Clip 规则，补齐摄像机深度所用 ShadowCaster Pass。皮肤不再被背景透出；保留第二层透明像素。Unity 的摄像机深度纹理由 ShadowCaster Pass 参与生成，见 [Unity 2022.3 文档](https://docs.unity3d.com/2022.3/Documentation/Manual/SL-CameraDepthTexture.html)。
- Transform：32 个本地样本缓冲，位置 Lerp、Yaw Quaternion Slerp；大距离跳变直接修正，缺样本保持最后位置，小距离恢复平滑。
- Head／Body：头部先响应，身体超过角差阈值或移动时跟随；身体只用 Yaw，头部独立应用 Pitch。
- Animation：派生 idle、walking、running、sprinting、jumping、falling；从插值后位置求速度，动态调整既有 Animator 步频，Root Motion 关闭。
- Ground Alignment：7DTD 地面探测仅调整视觉 Y，不回写 Entity 坐标；无地面时回退事实高度。
- Inspector：7DTD F1 执行 `mc7dtd_entity_inspect`，显示本地 Avatar 材质、动画、速度、插值、贴地偏移和实际显示对象状态。Minecraft Inspector 不会跨端读取 7DTD 渲染器，避免新增协议字段。

## 修改文件

以下路径相对项目 `D:/wenjian/minecraft/7-M`。完整 Motion 文件列表及 SHA256 见 [modified-files.json](modified-files.json)。

| 类别 | 文件 |
|---|---|
| 7DTD Motion Runtime 新增 | `7dtd-mod/src/AvatarMotionSettings.cs`、`AvatarMotionController.cs`、`AvatarGroundAlignment.cs` |
| 7DTD Runtime 更新 | `7dtd-mod/src/AvatarRenderer.cs`、`AvatarAnimationState.cs`、`AvatarProxyScene.cs` |
| 本地配置 | `config/avatar_motion.json` |
| 自动测试新增 | `tests/avatar-motion-smoke/AvatarMotionSmoke.csproj`、`RuntimeSmoke.cs`、`MotionChecks.cs`、`PolishChecks.cs`、`ModInfo.xml` |
| 旧测试兼容 | `tests/avatar-runtime-smoke/AvatarRuntimeSmoke.csproj`、`RuntimeSmoke.cs`；`tests/avatar-polish-smoke/AvatarPolishSmoke.csproj`、`RuntimeSmoke.cs`、`PolishChecks.cs` |
| 脚本 | `tests/run-phase3_8_6.ps1`、`verify-phase3_8_6-game.ps1`、`launch-phase3_8_5_4.ps1` |
| 文档 | `docs/phase3_8_6_avatar_motion_polish.md`、`docs/phase3_8_6_runtime_acceptance.md` |

本轮此前 Material Polish 还更新了 `7dtd-mod/src/AvatarInspectorCommand.cs`、`assets/avatar/production_unity/Assets/Avatar/Shaders/AvatarSkin.shader`、资源构建器 `AvatarResourceBuild.cs`、`tests/produce-avatar-resources.py` 及生成的 Material／Prefab／Bundle／manifest。详细说明和资源证据见 [Material Polish 文档](../phase3_8_5_4/phase3_8_5_4_avatar_presentation_polish.md)。Motion 没有再次修改模型、Controller、Clip 或 Bundle。

## 编译及自动测试

| 项目 | 结果 | 证据 |
|---|---|---|
| 7DTD Phase386 build | PASS，0 warning／0 error | [编译日志](7dtd-build.log) |
| Bridge Phase386 build | PASS，0 warning／0 error；源码未改 | [编译日志](bridge-build.log) |
| Minecraft build | PASS；源码未改 | [编译日志](minecraft-build.log) |
| 7DTD 原生引擎测试 | 91 PASS | [逐项结果](7dtd-runtime-results.txt) |
| Minecraft 12 个 Harness 回归 | 261 PASS | [结果](results.json) |
| Proxy／真实 WebSocket 回归 | 26 PASS | [结果](player-proxy-regression.log) |
| Material 资源 Editor 检查 | 25 PASS | [结果](../phase3_8_5_4/unity-resource-results.txt) |
| 受保护源码检查 | 72 个文件，0 个变化 | [结果](protected-source-results.json) |

91 项包含 Position Lerp、最短弧 Slerp、Teleport Bypass、Head Smooth、Body Follow、六种本地动画状态、动态播放速度、Ground Hit／No Ground Fallback，以及实际 Unity Avatar 的 Spawn／Update／Remove、重连新缓冲、初始完整旋转和事实坐标不变。Material 还验证 Alpha Clip、颜色／摄像机深度输出。Entity、Health、Identity、Equipment、Presentation、Authority 回归通过。

## 人工验收

使用 `tests/launch-phase3_8_5_4.ps1` 启动当前 Phase386 三端，7DTD V3.2.0 b10、Minecraft 1.21.11。运动观察由用户完成，退出、重进、Inspector 和最终截图由受限 computer-use 完成。

| 验收项 | 结果 | 证据／依据 |
|---|---|---|
| Skin 不透明、Base／Layer2 | PASS | [材质修复截图](../phase3_8_5_4/03-avatar-depth-fixed.png)、[皮肤近照](../phase3_8_5_4/06-skin-close-up.png) |
| 连续移动 | PASS | 用户明确回复“移动连续” |
| 头身自然、身体保持直立 | PASS | 用户明确回复“头身自然”；[重进截图](05-respawn-avatar-ground.png)显示身体直立、头部独立倾斜 |
| 坡地脚部贴地 | PASS | 用户明确回复“脚部贴地”；[Inspector](02-inspector-motion-ground.png)记录 ground_hit、ground_offset=-2、display Y=61，源 Entity Y=63 |
| 静止归 idle、对象 active | PASS | Inspector：animation=idle、velocity=0、material=cutout、render_objects_active=true；holding 为静止缺少变更样本的正常状态 |
| 退出清理 | PASS | 18:32:14 world_unloaded／proxy count=0；[空列表截图](03-cleanup-empty.png)、[生命周期日志](manual-lifecycle.log) |
| 重进重新生成 | PASS | 18:32:59 新实体 b57fdad4-6a8b-401a-be5c-cd4932c3dc5a，count=1；[本地状态](04-respawn-inspector.png)、[可见模型](05-respawn-avatar-ground.png) |
| 7DTD 世界退出清理 | PASS | 18:34:30 World.Cleanup，随后正常关闭实例 |

本轮用户反馈没有单独逐项确认 walk／run clip，动态步频与状态切换的逐项 PASS 来自自动原生引擎测试；人工结论仅记为连续移动和自然头身表现，不把数据状态当作逐帧动画录像。

## 配置、复测及限制

当前 [avatar_motion.json](avatar_motion.json) 开启插值及贴地，延迟 0.55 秒，传送阈值 12，身体跟随阈值 35°。复测启动原 launcher，进入两个既有世界，观察慢走／快速移动、快速转头、上下视角、坡地和退出／重进；在 7DTD 原生控制台查询本地 Inspector。

- 0.55 秒是新增的视觉缓冲延迟，不改变约 500ms 的网络发送周期。缺样本不外推，可能暂时停住；大距离跳变直接修正。
- 六种状态由现有 Transform 推断，短暂跳跃可能无法捕捉。sprinting 复用 Run；jumping／falling 当前复用 Idle，没有新增动画。
- 地面贴合依赖当前加载的本地碰撞体；无命中回退事实高度，空中高度使用本地启发式保留。不是两个世界的全局地形对应系统。
- 未改 Entity／Equipment 协议、Authority、Presentation、revision、Bridge 和 7DTD 装备同步；未新增任何跨端字段。

测试实例已正常退出，Bridge 测试进程已停止；全局 ModInfo 已恢复，测试监听端口 18771 已释放。未提交 Git 或进入下一阶段实现。

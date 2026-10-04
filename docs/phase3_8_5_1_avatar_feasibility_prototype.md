# Phase 3.8.5.1 Avatar Feasibility Prototype

日期：2026-10-04。环境：7DTD V3.2.0 b10 / Unity 2022.3.62f2。

结论：**自定义 Minecraft Mesh 路线的最小显示及基础 Animator 播放可行。** 独立原型完成；没有实现正式 Avatar Runtime。原生 TPAnimController 的 Humanoid Idle/Walk 直接复用验证失败，不能把这项结果合并成全部 PASS。

## 1. 模型加载结果

| 验证项 | 结果 | 证据与边界 |
|---|---|---|
| 外部测试 Mesh 读取 | PASS | JSON → Unity Mesh，144 顶点、72 三角形、六个身体部分 |
| 独立模型挂载 | PASS | IndependentAvatarPreviewProxy → SkinnedVisual / SkinnedMeshRenderer；真实世界中出现在本地玩家附近 |
| Humanoid Avatar 构建 | PASS | 19 个骨骼节点；Avatar.isValid=True、isHuman=True |
| 接入既有同步代理 | 未执行，按限制排除 | 没有注册 Entity、替换已有代理、调用同步控制器 |

这里的测试代理是独立 Unity GameObject 根节点，与现有代理使用相同的本地视觉对象类型。它证明独立模型可以在当前 7DTD 引擎和世界中挂载、显示；不等于既有 Entity 生命周期的集成验收。

资源为 `assets/avatar/prototype/alex_slim_mesh.json` 与 `player_default.png`。PNG 从现有默认皮肤原样复制，SHA256 均为 `D09B5B5F06B1056229604C363CE539D9BABF9CEA2F49DBE3718E99E6245085EC`。

## 2. Skin 显示结果

- Runtime PNG Texture：PASS。ImageConversion.LoadImage 返回成功，尺寸 64×64；Texture → Material → Renderer 实际渲染成功。
- Alex Slim 基础层：目视 PASS。三像素手臂几何/UV，头、身体、左右手臂与腿部使用对应皮肤区域；T-pose、Idle、Walk 截图已检查。
- 完整皮肤外层：未实现。帽子、外套、袖口、裤腿等第二层不在本原型范围；不宣称完整 Alex 皮肤系统通过。
- 128×128、远程皮肤、缺失皮肤生产 fallback：未测，也未接入资源管理 Runtime。

证据：`evidence/alex-slim-tpose.png`、`alex-slim-idle.png`、`alex-slim-walk.png`。截图由真实游戏进程渲染；为避免同一帧内 GPU 蒙皮缓存造成采样滞后，先用 SkinnedMeshRenderer.BakeMesh 固定当前姿态，再由测试相机拍摄。拍摄对象立即禁用后销毁，不残留前一张姿态。

## 3. 动画结果

| 验证项 | 结果 | 实测 |
|---|---|---|
| 自有 Idle → Animator | PASS | 骨骼角度变化合计 1.09057°；平均顶点位移 0.001854582 |
| 自有 Walk → Animator | PASS | 骨骼角度变化合计 24.91765°；平均顶点位移 0.0251291 |
| 游戏世界中循环切换 | PASS | 每四秒切换 Idle/Walk；实际世界画面与截图观察到摆臂、迈步 |
| 原生 Humanoid Idle/Walk 直接重定向 | FAIL / 无候选 | 当前 SDCSUtils.TPAnimController 返回 679 个 Clip，isHumanMotion=True 的数量为 0 |

工作链路是 **原型程序化动画 → IAnimationJob → AnimationScriptPlayable → AnimationPlayableOutput → Animator → 骨架 → SkinnedMeshRenderer**。不是直接在 Update 中修改场景骨骼后冒充 Animator，也没有做网络动画同步。Unity 官方提供同类动画作业输出到 Animator 的用法：[AnimationScriptPlayable.Create](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Animations.AnimationScriptPlayable.Create.html)。

这些 PASS 只验证自有最小程序化 Idle/Walk 的 Animator 播放能力；没有验证正式 AnimatorController 状态机、导入 .anim/FBX 动画或原生 SDCS Controller 的整体复用。候选 Clip 非 Humanoid 不意味着整个游戏没有其他 Humanoid 资源，也不意味着方案 A 完全不可行。原生动画清单在 `evidence/native-animation-clips.txt`。

## 4. 方案 A / B 建议

**建议下一步以方案 B（自定义 Minecraft Humanoid Mesh）作为主路线，配套自有动画资源或播放适配器。** 本轮已实测 B 的 Mesh、Skin、有效 Humanoid Avatar、Animator 输出；能直接使用 Minecraft Skin UV，保持 Minecraft 外形。

方案 A（原生 SDCS 玩家模型）继续作为备用研究路线。本轮没有实例化原生玩家模型，也没有完成原生 UV 转换，不能给 A 的独立模型复用标 PASS/FAIL。当前控制器的 Clip 检查没有找到可直接用于 Humanoid 重定向的候选，原生骨架路径、控制器与游戏 Entity 的依赖仍需专门实验。不能继续假设“拿到原生 Controller 就能直接使用行走动画”。

## 5. 后续 Runtime 实施计划（仅计划）

1. 固定资源格式、模型单位、左右骨骼方向和根节点高度；补齐皮肤第二层，校准足底位置、绑定姿态与灯光材质。
2. 准备正式 Idle/Walk 动画资源和播放后端，单独验证 Humanoid 导入、循环、姿态混合、根运动禁用；如继续原生复用，先完成独立 Generic 骨架兼容实验。
3. 再实现本地 AvatarState Resolver / AvatarRenderer 的 create、update、remove；建立材质/贴图缓存、失败 fallback、资源释放与退出重进测试。
4. 独立渲染器稳定后，再计划既有 Entity 代理生命周期接入；保持 Entity → AvatarState → Renderer，继续分离 Equipment、Authority 与网络事实数据。
5. 后续另行设计 Anchor Provider 和装备挂载；当前不实现 Equipment 或动画网络同步。

本轮停止在可行性原型，没有执行以上 Runtime 计划。

## 6. 编译、检查与环境恢复

独立 AvatarPreview / net48 编译 PASS：0 警告、0 错误。未修改或重新编译 Minecraft、Bridge、正式 7DTD Bridge Mod；它们不是本原型的编译验证范围。

`tests/verify-phase3_8_5_1.py` 对真实游戏证据完成 10 项检查：Model Load、PNG Texture、Humanoid Avatar、Idle Animator、Walk Animator、Independent World Preview、Bridge Isolation、No Prototype Error、Render Captures、Cleanup Requested，全部 PASS。基础层 UV/皮肤显示另由截图目视检查；自动脚本不证明像素级 UV 全覆盖。

测试使用 `work/phase3851-runtime` 和原测试存档副本。7DTD 同时扫描用户目录与安装目录，因此启动器在测试期间临时改名全局 Bridge ModInfo，游戏日志确认该 Mod 被忽略。游戏已正常退出，原 ModInfo 已恢复，禁用备份不存在。存档原件和正式项目配置未改动。Shutdown 请求销毁模型/贴图/材质/Avatar/PlayableGraph；未进行长期资源泄漏或多实例压力测试。

## 7. 实验中发现的问题及限制

- Unity Editor 批处理构建因无有效许可证而拒绝启动；没有生成或验证 AssetBundle。`unity/` 中的 Editor 实验保留供以后使用，当前结果来自游戏内运行时 Mesh 构建。
- Unity JsonUtility 首轮没有完整读取模型骨骼数组；原型改用游戏已有 Newtonsoft.Json，后续实际加载通过。
- 首版同帧拍摄出现蒙皮采样滞后及临时对象延迟销毁叠影；最终改为当前姿态 BakeMesh，并立即禁用拍摄对象，截图已重测。
- Mesh 使用每个身体块刚性绑定一个骨骼，膝肘没有分段变形；不等于正式完整 Humanoid 角色。
- 当前摆臂、根节点高度、足底接地、肩部绑定、坐标比例与完整皮肤层仍需制作/校准。相机证据用包围盒居中，不能拿居中截图证明世界地面挂载精度。
- 原型静态放置在玩家附近，不跟随玩家移动，没有碰撞、寻路、战斗、装备或动画同步。
- 动画候选读取仍依赖当前游戏版本 SDCS API，正式实现需要版本隔离和失败降级。

## 8. 新增文件与复现

- `prototypes/avatar-preview/AvatarPreview.cs`、`AvatarPreview.csproj`、`ModInfo.xml`、`README.md`、`create_test_mesh.py`
- `prototypes/avatar-preview/unity/Assets/Editor/AvatarPrototypeBuild.cs`、`Packages/manifest.json`、`ProjectSettings/ProjectVersion.txt`（未执行的可选 Editor 实验）
- `assets/avatar/prototype/alex_slim_mesh.json`、`player_default.png`
- `tests/launch-phase3_8_5_1.ps1`、`tests/verify-phase3_8_5_1.py`
- 本文档

在 `D:\wenjian\minecraft\7-M` 执行 `./tests/launch-phase3_8_5_1.ps1`，进入复制的 Navezgane / MC7DTD-Phase3-3 存档。无需装备操作或跨端连接，等待原型出现并查看结果即可。正常退出整个 7DTD 后，启动器恢复全局 ModInfo，再执行 `python tests/verify-phase3_8_5_1.py`。

只编译可用 `./tests/launch-phase3_8_5_1.ps1 -BuildOnly`。不要在游戏运行中中断启动器；若启动器被强制中断，按 README 恢复安装目录的 ModInfo 后再进行原项目验收。

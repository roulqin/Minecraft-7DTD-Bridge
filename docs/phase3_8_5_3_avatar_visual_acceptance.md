# Phase 3.8.5.3 AvatarRenderer Runtime v1 人工视觉验收报告

日期：2026-10-04（Asia/Shanghai）。

结论：**Avatar 存在、皮肤、旋转、挂点及生命周期的核心功能人工验收 PASS。移动跟随 PASS；“完全无可见跳变”的平滑性要求尚不能判为 PASS，因此不是无保留的整体验收结论。**

本次仅进行受限验收，没有修改 Runtime、协议或其他源码，没有进入下一开发阶段。以下截图均来自实际 7DTD 游戏窗口；用户完成短暂 Minecraft 移动／视角输入并协助定位观察者，Codex 通过 computer-use 操作 Inspector、保存退出与重进，记录画面和日志。

## 环境与观察方向

- 实际 7DTD V3.2.0 b10，Minecraft 1.21.11，Bridge localhost:18771。
- 启动前没有旧 Minecraft、7DTD、Bridge 实例，端口未被占用。
- 使用 tests/launch-phase3_8_5_3.ps1 启动三端，证据运行目录 docs/phase3_8_5_3-runtime-evidence/20261004-154200。
- 首次 Minecraft 启动的 HTTP 客户端因 Java UnixDomainSockets/PipeImpl 的 `Invalid argument: connect` 失败，没有连接 Bridge。保存退出后，将该次启动的 TEMP/TMP 指向项目 work 目录重启，连接成功。没有修改启动脚本或 Runtime；此启动环境依赖应作为后续脚本修正项。
- Minecraft 的 entity_list/entity_goto 定位的是 **Minecraft 世界中的 7DTD 来源代理**。本次测试对象是 **7DTD 世界中的 Minecraft 来源 Avatar**，使用 7DTD F1 控制台 `mc7dtd_avatar_inspect`，不把 Minecraft `/mc7dtd_entity_inspect` 当作 7DTD 本地渲染器状态。

## 每项结果

| 项目 | 结果 | 实际证据与范围 |
|---|---|---|
| Test 1 Avatar 生成 | PASS | 7DTD 中出现完整 Minecraft 风格角色，替代旧几何代理；01、05 截图与 spawn 日志。 |
| Test 2 Skin | PASS | 与项目 player_default.png 的粉色头发、黄色眼睛、白色衣服、紫色装饰相符；帽层／外层轮廓、袖口与裤腿纹理可见，未发现明显贴图错位。采用当前自定义皮肤，并非原版 Alex 皮肤；01、02、05。 |
| Test 3 Alex Slim | PASS | 当前 variant=alex_slim，视觉手臂窄于 Steve 宽臂，正面／侧面未见明显 UV 错位；01、02、05。该结论是外观检查，不是重新进行网格像素尺寸测量。 |
| Test 4 移动跟随 | PASS（位置跟随）；平滑性保留 | 用户短暂前后／横向移动，7DTD Avatar 随之改变位置；03 与接收样本。没有发现错误实体、长期消失或来源／接收位置不匹配。更新是离散采样，不能把“完全无跳变”记为 PASS。 |
| Test 5 Yaw | PASS | 角色由正面转为侧面，身体水平转向，右手标记同时转向；01、07 与 yaw 记录。 |
| Test 6 Pitch | PASS | 低头约 +35.1°、抬头约 −38.1°：头部变化，身体保持直立；06、07。 |
| Test 7 RightHand | PASS | 本地 torch 黄色标记位于角色右手；转向后仍与手部连接，没有观察到脱离手部漂浮；04、04-anchor-inspector、07。它是本地挂点标记，不是跨端同步的火把物品模型。 |
| Test 8 删除／重新生成 | PASS | Minecraft 正常退出后 Avatar 和黄色标记消失，日志 count=0；重进后新 snapshot 创建 Avatar，count=1，旧标记不残留；08、09、10 与生命周期日志。 |
| Test 9 Inspector | PASS（7DTD 本地入口） | status=active，reason=null，model=minecraft_alex，variant=alex_slim，skin=player_default.png，animator=idle；05、10。没有新增 Minecraft 端远程 avatar_renderer 字段。 |

## 关键记录

初次被验收的 Minecraft 来源 entity_id：`4d82a420-6616-4772-ae9f-1e5c96fce89c`。

挂点测试命令：

```text
mc7dtd_avatar_test_held 4d82a420-6616-4772-ae9f-1e5c96fce89c 7dtd:torch
```

当时 Inspector 显示 `held_item_local_test=7dtd:torch`、`anchor=RightHand`。

侧身抬头接收记录（16:00 前）：position=(7.544319366998038, 65.094709862517078, 5.37346371558981)，yaw=90.1532211303711，pitch=−38.099895477294922，roll=0，接收应用线程=1。07 截图与此姿态一致，身体没有跟随头部倾斜。

16:01:32：旧 entity despawn，reason=world_unloaded，代理 count=0；08 截图无角色／挂点残留。

16:02:24：新的 entity_id `3e74ba32-dcca-4f02-ad1e-e079fac8dfe4` snapshot，代理 count=1。09 显示 Avatar 重建，10 显示 active / idle / held_item_local_test=null。新会话 ID 来自既有采样器行为，本次未修改 ID 生成规则。

## 发现的问题与限制

1. **启动环境依赖。** 直接使用当前新启动脚本时，这台机器的 Minecraft Java 通信管道初始化失败；使用旧验收流程的项目 work 临时目录后恢复。脚本尚未修正，此问题需单独处理。
2. **移动平滑性未通过完整验证。** 用户第一次移动期间抽取 47 条变化样本，7DTD 主线程接收应用，更新间隔中位数约 0.5275 秒。该数值是采样／应用间隔，不是端到端网络延迟；无法据此宣称网络延迟为 0.53 秒。当前视觉直接使用离散位置，本次截图不足以证明快速移动完全没有跳变。
3. **跨世界地形高度不同。** Minecraft 坐标 Y≈65 对应的 7DTD 地面低于该高度，Avatar 可悬空。7DTD 原生 teleport 会把本地观察者落到地面，定位时需要调整观察高度。Avatar 与事实 Transform 保持一致；本阶段没有贴地适配或地形映射。这不是右手标记脱离骨架。
4. 当前材质呈现较亮的像素色彩；未进行色彩管理或 7DTD 光照一致性验收。
5. Animator 保持 Idle；动作、跨端装备和表情均未同步，符合本次允许的范围。
6. 皮肤检查为可见正面／侧面检查，没有逐面遮挡基础层来单独核对所有 Layer2 像素，也不等同于运行时内存／所有场景压力测试。

没有在本次会话日志中发现 Avatar fallback、Player proxy operation failed 或 Avatar Renderer 异常。日志中的初次 Java 连接错误已经单独保留，不能把整个测试会话称为“从未发生错误”。

## 截图与日志

- 01-avatar-full.png：完整角色正面。
- 02-skin-close.png：侧面皮肤及外层轮廓。
- 03-movement.png：移动后位置变化。
- 04-right-hand.png：右手黄色标记。
- 04-anchor-inspector.png：本地挂点状态。
- 05-inspector.png：初次 active 状态。
- 06-pitch-head-only.png：低头、身体直立。
- 07-pitch-up-side-anchor.png：侧身抬头、挂点跟随。
- 08-avatar-removed.png：退出后的清理。
- 09-avatar-respawn.png：重新生成，无旧挂点。
- 10-respawn-inspector.png：重生 active、本地装备为空。

部分截图同时支持多个测试项，因此为相同画面的副本，不代表重复独立测量。截图没有生成式处理或内容改动。

日志：7dtd-avatar-evidence.log、bridge-lifecycle-evidence.log、minecraft-session.log、minecraft-initial-failure.log；移动采样：movement-receiver-samples.json。

## 收尾与后续

Minecraft 和 7DTD 已正常退出，测试 Bridge 已停止，18771 端口释放。Steam 全局 ModInfo.xml 已恢复，隔离备份文件不存在。Phase 3.8.5.3 的源码／项目／测试文件与开发完成时的 SHA256 一致，见 cleanup-and-source-checks.json。

本阶段“Minecraft Avatar 在 7DTD 中存在”的核心目标已得到实机证据。下一阶段建议先明确是否处理启动脚本临时目录、视觉插值和平移高度策略；不把这些问题默认为已修复，也不在本次验收中实现 Phase 3.8.5.4。

# Phase 3.7.1：Health Component 运行时接入与验收

日期：2026-10-03。工程根目录：`D:\wenjian\minecraft\7-M`。

## 结果与范围

完成 **7DTD 真实玩家 health → Bridge → Minecraft 日志**。支持 initial snapshot、health 组件 partial patch、显式 null remove 与空 snapshot 覆盖清空；真实游戏已验证 100/100 → 92/100 → 93/100 → remove → despawn。

entity_state v2 核心与两个现有 Schema 不变。Minecraft 原有 v1 player_position/player proxy、双向 entity_state、EntityRegistry、CoordinateMapper、生成/显示代码保持兼容。未接入 inventory、combat 同步、动画播放、AI 或 health 写回游戏。

本轮只启用 7dtd:player 的 health 观察通道。Minecraft 来源玩家仍走既有 v1，未升级其生命周期协议或增加反向 health 发布；name、animation_state、custom_metadata 均不接入运行时。

## 架构与权限

1. 七日杀 GameUpdate 主线程读取 `primary.Health`、`primary.GetMaxHealth()`，与既有位置采样共用 500ms 节流。网络在连接工作线程发送，不访问游戏对象。
2. NativePlayerPublisher 先排队 entity_state，再排队引用该事件 sequence 的 entity_components。首次可用 health 发 snapshot；current/max 改变发 patch；采样不可用发 remove；恢复后发新 snapshot。未变化不重复发布，最多每 500ms 一个健康观察。
3. Bridge 在既有生命周期锁内检查握手角色、authority、origin、完整身份、类型、stream、活动 NativeEntityRegistry 当前 sequence，再检查组件权限、revision/base_revision 和 health。
4. HealthComponents 存储观察状态并转发，Minecraft HealthReceiver 在同一连接接收线程关联已接收的 v2 生命周期，记录 initial/update/remove。不触碰 Minecraft 玩家血量，也不改变代理几何对象。

新增 [component_permissions.json](../config/component_permissions.json) 与 [配置 Schema](../config/component_permissions.schema.json)：只有 source=7dtd、target=minecraft、entity_type=7dtd:player，且 publish/store 都明确 true 才授权。还必须匹配 entity_types.json 中已启用、映射到 Minecraft 的源类型；缺失、false、未知组件均拒绝。配置在 Bridge 启动时加载，修改后重启生效。

这是 Phase 3.7.0 定义的独立发布/存储授权的首次运行实现。保留 entity_types.capabilities.health=false：该能力描述目标游戏代理的健康应用能力，本阶段只授权观察与日志，不赋予伤害/治疗能力。没有从消息 metadata 获取权限，不允许另一角色为原生所有者改写或删除健康状态。

未经授权/非法组件消息收到 error，整包不更新且不提高 revision；本组件错误不主动切断原有 WebSocket。原有 v1/v2 的错误处理不变。

## 状态规则与生命周期

- 健康值必须完整包含 current/max，有限数字，current≥0、max>0、current≤max；health=0 仅记录，不发伤害或 despawn。
- initial 为 mode=snapshot、base_revision=0；首次建立需要 snapshot。snapshot 完整覆盖，components={} 清空健康；snapshot 中 health=null 非法。
- partial 为 mode=patch，必须提供完整 health 值；base_revision 精确等于最后已应用 revision，新 revision 必须更大。旧 revision 忽略前仍校验身份、权限与内容。
- remove 为 patch 中 `"health": null`，同样需要所有权、权限和有效版本基线。保留空状态的 revision 以拒绝旧消息；对应实体 despawn 时删除这一基线记录。
- health 原生单位直接传输，不经 CoordinateMapper 换算。坐标仍由既有 v2 逻辑转换。
- 原世界正常退出先排队 remove（引用最后活动 sequence），再排队 despawn（下一个生命周期 sequence）。Bridge 在 despawn 转发前清理对应组件；Minecraft 在生命周期 despawn 时清理本地记录。
- 使用既有连接/接收端重连边界清理组件，新轮次由原生发送器产生 fresh spawn + health snapshot。Bridge 重启无健康持久化。没有新增 ACK、握手类型、端口或队列；组件沿用原生发送 FIFO 的容量 64、超时与重连策略。

## 自动测试与编译

```powershell
Set-Location 'D:\wenjian\minecraft\7-M'
.\tests\run-phase3_7_1.ps1
```

此入口编译 Bridge/.NET 10、七日杀 Mod/.NET Framework 4.8、Fabric/Java 21，执行新增 health 测试和实际 C# 序列化的 Schema/语义验证。使用现有工程内依赖，不安装或修改其他项目。

| 检查 | 结果 |
| --- | --- |
| C# 健康状态/权限/采集器/真实 WebSocket 和 Java 接收器 | 59 项通过 |
| Java 初始、patch、删除、非法状态原子拒绝与生命周期清理 | 20 项通过 |
| 原有 entity/v1/player proxy/marker/v2/Java 投影回归 | 235 项通过 |
| 原类型目录 Schema/adapter | 5 组通过 |
| Phase 3.7.0 组件行为契约 | 41 组通过 |
| C# 发布器实际序列化消息 | 6 份组件消息通过原 Schema 和 Oracle |
| 新组件权限配置 Schema | 通过 |
| Bridge、七日杀、Fabric 编译 | 均成功；C# 0 警告、0 错误 |

回归命令：`.\tests\run-phase3_6_2.ps1`。Phase 3.7.0 的第 42 组“生产文件完全不变”是历史设计阶段边界，本阶段授权修改运行时，故仅运行其 41 组行为测试，不重写或覆盖旧阶段基线/结果。本阶段另以旧 SHA-256 基线核对 v2 Schema、正式网络/坐标/类型配置、CoordinateMapper、原 EntityRegistry、EntityTransportV2 和双方生成代码，全部一致。

测试涵盖 authority 伪造、错误 origin/stream/entity_sequence、权限缺失/false、health 范围和字段、boolean/fraction/超安全整数、重复 JSON 键、未知组件、旧 revision、基线不匹配、原子性、空快照、失效采样删除、恢复快照、正常退出、两端实际 WebSocket 接收以及 Bridge 重启。网络入口沿用 8192 字节、深度 8、文本帧边界。

证据：[work/phase3_7_1-test](../work/phase3_7_1-test/)，既有回归：[work/phase3_6_2-test](../work/phase3_6_2-test/)。自动测试属于受控采样输入，以下实机证据另来自真实游戏主玩家。

## 实机验收

Minecraft 1.21.11 / Fabric Loader 0.18.4 / Fabric API 0.140.2+1.21.11，PID 20552；7DTD V3.2.0 B10，PID 23416；Bridge Phase371 PID 3672。端口继续 18771，使用工程内本轮证据目录的 network.json 和复用上一阶段的相邻坐标验收配置，不覆盖正式配置。

七日杀存档 MC7DTD-Phase3-3；entity_id=`ec5d61f7-843b-47b7-a811-28c15b00651f`；stream_id=`d6937bfa-f41f-4a3c-87a8-c01a0755b820`。

| 验收项 | 真实证据 | 结果 |
| --- | --- | --- |
| 进入世界/初始健康 | 20:16:30，snapshot revision=1，entity_sequence=1，100/100；Minecraft 同值日志 | 通过 |
| 用户正常游戏操作改变生命值 | 20:17:33，patch revision=2/base=1，entity_sequence=124，92/100；Minecraft 同值日志 | 通过 |
| 游戏自然恢复 | 20:18:31，patch revision=3/base=2，93/100；截图 HUD=93/100，Minecraft 同值日志 | 通过 |
| 正常退出移除组件 | 20:19:10，patch revision=4/base=3，health=null；Minecraft removed=true | 通过 |
| 后续生命周期清理 | despawn sequence=318、reason=world_unloaded；Bridge 原生 count=0，Minecraft proxy despawned/count=0 | 通过 |
| 排除依赖断线清理 | /health 退出前后均含 minecraft、7dtd；initial 至 despawn 无 disconnect/reset | 通过 |

[运行验证](phase3_7_1-runtime-evidence/verification.json) 的 **21 项对照全部通过**，逐条校验实际发布组件与源生命周期引用、原 Schema、Minecraft 值、Bridge 接受/删除日志、连接连续性和部署哈希。组件的退休内存行为还由自动测试验证；未增加调试端点或远程内存检查。

证据目录：[phase3_7_1-runtime-evidence](phase3_7_1-runtime-evidence/)。包含两端日志、Bridge 日志、health-events.json、部署/保留文件哈希、退出前后连接快照，以及 7dtd-health-initial.png、7dtd-health-update.png、7dtd-world-exited.png。Minecraft 最新日志另复制保存，不依赖后续运行覆盖。

复核保存证据：

```powershell
$env:PYTHONPATH='D:\wenjian\minecraft\7-M\work\phase3_4-test\python-libs'
python .\tests\verify-phase3_7_1-runtime.py
```

## 启动与人工复验

先正常退出已有两个游戏，并确认旧 Bridge 已结束；每个角色只运行一个实例。需要已有 Minecraft runtime 启动参数、七日杀安装/Steam 登录，以及本工程测试存档。

```powershell
.\tests\launch-phase3_7_1.ps1 bridge
.\tests\launch-phase3_7_1.ps1 minecraft
.\tests\launch-phase3_7_1.ps1 7dtd
```

启动器仅将新 JAR/DLL 部署到工程内 runtime，七日杀窗口可见供人工游戏操作。进入两款游戏的测试世界，查看 Minecraft 最新日志 `7DTD health received:`。使用游戏自身方式让七日杀健康变化，确认 mode=patch、current 与七日杀 HUD 一致；正常退出七日杀世界，确认 removed=true 后收到 despawn，连接仍在。

日志路径：runtime/minecraft/logs/latest.log、docs/phase3_7_1-runtime-evidence/7dtd-game.log、同目录 bridge-stdout.log。重跑前另存已有验收证据，避免启动器覆盖本轮日志；证据验证脚本针对本轮保存值。

## 修改文件列表

新增：

- bridge-server/HealthComponents.cs
- config/component_permissions.json
- config/component_permissions.schema.json
- minecraft-mod/src/main/java/io/mc7dtd/HealthReceiver.java
- minecraft-mod/src/test/java/io/mc7dtd/HealthHarness.java
- minecraft-mod/src/test/java/io/mc7dtd/HealthReceiverHarness.java
- tests/health-runner/HealthRunner.csproj
- tests/health-runner/Program.cs
- tests/health-runner/validate_wire.py
- tests/run-phase3_7_1.ps1
- tests/launch-phase3_7_1.ps1
- tests/verify-phase3_7_1-runtime.py
- docs/phase3_7_1_health_runtime.md
- docs/phase3_7_1-runtime-evidence/（配置副本、进程信息、日志、截图与对照结果）

修改：

- bridge-server/NativeEntityRegistry.cs：只新增活动记录匹配查询。
- bridge-server/Program.cs：组件消息入口、锁内验证/转发和随生命周期清理。
- 7dtd-mod/src/BridgeClient.cs：health 数据契约，复用既有原生队列发送。
- 7dtd-mod/src/NativePlayerPublisher.cs：可选健康采集/发布，保留原构造和调用兼容。
- 7dtd-mod/src/BridgeMod.cs：传入主线程真实健康值。
- minecraft-mod/src/main/java/io/mc7dtd/BridgeClient.java：健康日志接收和连接/生命周期关联。

构建/测试输出在工程 minecraft-mod/build、各 bin/obj、work；runtime 的 JAR/DLL 替换为已验收构建。没有修改原游戏安装、其他项目或协议核心。

## 遇到的问题、限制与停止点

初次新增测试借用了既有 zombie v2 示例但健康授权只允许 player，测试正确拒绝；已改为显式 player 测试夹具，保留原示例。初次七日杀启动器隐藏了窗口，进入世界前重启为可交互窗口；正式验收区间从上述 initial 开始，无断线或重置。

7DTD 启动发现同名旧 Mod 并忽略，实际加载路径是本工程 runtime/7dtd/Mods/MC7DTD-Bridge，新 DLL 的部署哈希一致。Minecraft Demo 的 Realms 登录错误及 7DTD EOS/Twitch 外部服务提示仍存在，没有影响本地桥接；未修改认证或原游戏环境。Windows 控制台启动提示的非 ASCII 符号编码不一致，保留原始日志，对照器容错读取；健康 JSON、ID 和关键验收日志完整。

当前只记录健康观察，不提供血条 UI、治疗/伤害应用、战斗同步、单位换算、ACK 或持久化。健康变化可能被 500ms 采样合并；没有声称每次瞬时变化都已传输。所有权/权限拒绝与重启恢复通过自动测试，本轮实机覆盖 initial、变化及正常退出，未另做实机重启。

本轮结束：七日杀停留主菜单，Minecraft 仍在测试世界，Bridge 保持运行。Phase 3.7.1 完成后停止，不进入下一阶段。

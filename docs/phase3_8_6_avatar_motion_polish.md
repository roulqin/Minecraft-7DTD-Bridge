# Phase 3.8.5.4 / 3.8.6 Avatar Motion Polish + Transform Interpolation v1

## 数据流与边界

既有 Entity Transform → 本地到达时间采样／Interpolation Buffer → Head/Body 旋转 → 本地速度状态 → Ground Alignment 视觉偏移 → Avatar Root／Skeleton。

Entity 的 position/rotation、协议、Authority、Equipment、Presentation、revision、Bridge、7DTD 装备采集及同步均不改。Inspector 的 `position` 是原始事实，`avatar_renderer.display_position` 是最终视觉世界坐标。两个坐标可因插值和地面贴合而不同。只有 Minecraft 来源 Avatar 的 7DTD 本地表现使用此流程。

## 插值

默认延迟 0.55 秒，缓冲最多 32 个本地变更样本；使用本地单调时钟，不增加发送时间字段，也不改变约 500ms 的发送周期。Position 使用 Lerp，Yaw 使用 Quaternion.Slerp 的最短弧。Move/Rotate 在同一帧的调用合并成完整姿态；相同值的浮动原点刷新不重复插入样本。

初始 snapshot 的位置／朝向直接就位。单步距离大于 12 直接修正，不慢慢飞向传送目标；渲染线程停顿超过 1 秒也直接重置显示基线。样本不足时保持最后位置，不外推；超过 2 秒后的小距离新样本从当前显示位置恢复插值。重连和世界切换使用全新缓冲，不带入旧对象。

`buffering` 表示首次样本，`active` 表示当前有可插值区间，`holding` 表示播放时间越过最新变更样本，`snap_teleport`／`resync` 表示直接修正。`holding` 也可以是正常站立，不能据此断言网络断线。现有 Scene 每帧刷新同一事实，因此本层不能分辨“没有新变更”与“相同值的新网络消息”；断线清理仍由已有连接生命周期负责。

## 头／身体

Avatar Root 只应用 Body Yaw，绝不应用 Pitch。rig_head 使用相对 Body 的 Yaw 和 Pitch，Quaternion.Slerp 平滑；头部响应系数 12、身体 6。静止时偏差小于等于 35°不转动身体；超过阈值开始跟随。移动时身体持续跟随视角。头部相对 Yaw 限制 ±75°。Animator 之后的 LateUpdate 再应用头部旋转，保持骨骼动画兼容，RightHand 保持既有骨架挂点。

## 动画

速度从插值后的视觉位置求导并作短时滤波，避免 Animator 在模型开始移动前提前播放，或对单次网络位置跳变产生步频尖峰。速度 ≤0.1 idle、0.1~5 walking、5~8 running、≥8 sprinting；竖直速度 >2.5 jumping、<-2.5 falling。这些是本地推断，不是源游戏动作确认。

保留现有 Idle/Walk/Run Controller 的阈值：walking 的 Controller speed 在 0.5~1；running/sprinting 大于 1。另用 Animator.speed 动态匹配步频：walking 按速度／3.2 限制在 0.5~1.5；running/sprinting 按速度／6 限制在 0.8~1.8。未修改 Controller、Clip 或资源 Bundle。sprinting 复用 Run，jumping/falling 只建立状态并暂用 Idle，不新增跳跃／下落动画。Root Motion 始终关闭。

## 地面

7DTD 原生 Physics.RaycastAll 向下探测，忽略 Trigger 和 Entity Collider，避免把角色当作地面；默认从视觉事实位置上方 16 单位向下探测 128 单位。每个 Avatar 至多约 10 次／秒，传送、初次生成、浮动原点大幅变更立即重新探测。命中时只平滑视觉 Y 偏移，未命中时回到未偏移位置；不载入远处地形，也不改变真实 Entity 坐标。

首次生成／传送直接贴合；后续偏移响应系数 10。对于可推断的 airborne 状态，保留相对最近站立源高度的抬升，最多 8 单位，避免地面贴合完全吞掉跳跃。500ms 采样无法准确区分跳跃、台阶、飞行与落地，这仍是启发式表现，不是两世界地形坐标换算。

## 配置与 Inspector

`config/avatar_motion.json` 仅由 7DTD 本地 Renderer 读取。缺失／非法配置回到本地默认值。`enabled=false` 禁用新运动层；`ground_alignment=false` 可以单独保留原始 Y。参数范围被验证，配置在 Renderer 初始化时读取。

7DTD F1：`mc7dtd_entity_inspect [id]` 或 `mc7dtd_avatar_inspect [id]`。新增 `avatar` 摘要及 avatar_renderer 的 velocity、vertical_velocity、playback_speed、interpolation、ground_offset、ground_status、body_yaw、head_relative_yaw、display_position。Minecraft `/mc7dtd_entity_inspect` 仍观察反方向的本地代理，没有新增跨端 Renderer 字段。

## 测试与使用

`tests/run-phase3_8_6.ps1`：编译、Minecraft 回归、真实 WebSocket 回归、隔离 Unity/7DTD 引擎验收。新增测试覆盖 Position Lerp、Quaternion 短弧、传送、丢失／迟到样本、缓冲上限、头身分离、六种派生状态、动态步频、真实地面 Raycast、无地面回退、坡面平滑、airborne 抬升、原始事实保持及生命周期。

按用户指定入口启动：`tests/launch-phase3_8_5_4.ps1`，当前加载 Phase386 产物，证据目录为 docs/phase3_8_6-runtime-evidence。进入两个既有世界，在 7DTD 观察 Minecraft 来源 Avatar。短暂慢走／快速移动，快速左右转头并抬头低头；在坡地观察脚部；正常退出 Minecraft 世界检查消失，重进检查新 Avatar。

## 限制

插值增加约 0.55 秒视觉延迟。超过缓冲覆盖的延迟可能暂停后恢复；大步长修正仍会立即发生。两世界地形碰撞不同，局部 Ground Alignment 不保证所有障碍物可达；未载入地形只能回退。新状态不是完整动作同步；不包含攻击、战斗、Inventory、跨端 Equipment 或 NPC 动画。正式 Runtime 使用单个 AvatarRenderer 管理所有 Avatar，并共享其 Bundle；测试时不同时创建独立 Renderer 重复加载同名 Bundle。

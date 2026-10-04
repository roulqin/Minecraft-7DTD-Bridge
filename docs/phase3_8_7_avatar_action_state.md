# Phase 3.8.7 Avatar Action State Layer v1

## 架构

Entity Transform → 既有本地 Interpolation／Velocity → Ground Alignment 支撑命中 → AvatarActionState → Animator。

Action State 是 7DTD 端 AvatarRenderer.Handle 持有的本地对象，不属于 Entity Component，不参与网络、Authority 或 revision。每个 Avatar 独立保存状态；Remove／Disconnect／World Change 走既有 Renderer 清理并 Reset。

## 数据与推导

`state`、`speed`、`grounded`、`timestamp`、`verticalVelocity`、`animation`、`sneaking_available`。timestamp 使用本地单调秒时钟，不是源游戏时间。speed 为插值后水平速度。

grounded 且 speed≤0.1：idle；0.1<speed<5：walking；speed≥5：running。airborne 且竖直速度>0.15：jumping；其余 airborne：falling。0.15 是抑制竖直噪声的阈值。远端潜行输入不存在，默认不推导 sneaking；`Observe(..., bool? sneaking)` 保留明确本地输入接口。

7DTD 地面高度不能直接与 Minecraft Y 比较。ObserveTransform 结合竖直运动、上一稳定源高度与本地支撑命中估计离地、顶点和落地。下降后停止且接近起始高度立即落地；停止在其他高度时等待0.25秒支撑稳定。单纯抬升后静止0.85秒也恢复，避免台阶或缺失下降样本造成永久空中状态。没有支撑命中保持非 grounded。Teleport／resync 重置运动基线，避免把坐标修正当成跳跃。

这是视觉估计：台阶、攀爬、飞行可能被当作 Jump／Fall；约500ms网络采样可能漏掉短跳、在顶点短暂停住或延后落地。未加入网络 grounded／jump 字段，也不宣称获取了源玩家动作事实。

## Animator 资源

保留 Idle／Walk／Run，新增独立 Jump／Fall Clip 和 Controller 状态。参数为 float speed、bool isGrounded（默认true）、float verticalVelocity、int actionState。代码：idle=0、walking=1、running=2、jumping=3、falling=4、sneaking=5。airborne 根据 actionState 进入 Jump／Fall，grounded 后按 speed 回到 Idle／Walk／Run；过渡为0.08秒。

Jump／Fall 只修改四肢曲线，不写 Avatar Root Position／Rotation 或 Head。Root Motion 关闭；插值、Yaw、LateUpdate Head Pitch 和 RightHand 保持既有流程。空中 Animator 播放速率为1，地面保留动态步频。旧资源缺少 Action 参数时不写这些参数，保留既有基础 Animator 行为，Inspector 的实际 animator 可用于辨别兼容资源。

## Inspector 与测试

7DTD 原生 F1 控制台执行 `mc7dtd_entity_inspect` 或 `mc7dtd_avatar_inspect`，新增 `avatar_action`。`avatar_renderer.animator` 表示实际 Controller 状态，与 `avatar_action` 派生值分开显示。Minecraft 同名 slash Inspector 不会远程读取 7DTD 本地 Action；本阶段没有为此扩协议。

自动验证：`tests/build-phase3_8_7-resources.ps1` 构建真实 Unity 资源；`tests/verify-phase3_8_7-game.ps1` 隔离原生引擎测试（不进入世界）；`tests/run-phase3_8_7.ps1` 运行资源、编译和回归。

人工验证：`tests/launch-phase3_8_7.ps1` 启动三端，进入既有世界，在7DTD观察 Minecraft Avatar。测试静止→慢走→快速移动、跳跃、从安全小高度落下及落地恢复，再查询本地 Inspector。退出 Minecraft 世界检查列表为空，重进检查新的状态与显示对象正常。

不实现攻击／伤害／战斗／武器动画／Inventory／跨端 Equipment，不修改 Entity 协议、Bridge、Authority、Presentation、Equipment、Component revision。

# Phase 3.8.8 Avatar Animation Refinement v1

## 检查与修复边界

旧 Clip 只给部分 Euler 轴和少量骨骼写曲线，使用 Write Defaults；旧 Fall 的左右手臂前后角度达到±65°。这些会增加混入绑定姿态、空中手臂展开及过渡恢复不一致的风险。单层 Animator 没有 Avatar Mask，运行时只覆写既有 Head Pitch／Yaw；不增加每帧骨骼复位。

现在所有7个 Clip 统一提供18个内部骨骼的 XYZ 旋转通道，排除 rig_head，且不写 AvatarRoot Position／Rotation。所有状态 Write Defaults=false。原地空中 Clip 的手臂和腿部前后角为0，手臂保留下垂基准；移动空中状态按水平速度混合 Walk／Run，避免固定抬臂、弯腿和跑跳与原地跳跃相同的姿态。网格、Skeleton 命名和皮肤 UV 不改。

## 地面 Blend Tree

Ground 状态使用 Simple1D speed：Idle=0、Walk=2、Run=5。speed 为本地水平速度（单位／秒），不再使用上一版归一化 Controller 参数。任意中间速度连续混合，例如3.5为Walk／Run各0.5。speed≥5使用Run，既有动态播放步频保留。

分类 Action State 与实际 Animator Clip 不必一一对应：例如 walking 的较高速度可以混入 Run。Inspector 分别展示 animation_state、animator_state 和实际Run权重 blend。

## 空中流程

Ground → JumpStart → JumpLoop → Fall → Land → Ground。

`AvatarAnimatorDriver` 只写参数。grounded true→false 且竖直速度>0.15时发出一次 jumpStart Trigger；持续上升不重复触发。JumpStart／JumpLoop／Fall／Land各有speed Blend Tree：原地Clip=0、Walk=2、Run=5。原地JumpStart Clip长0.18秒，JumpLoop循环；竖直速度<-0.15进入Fall。空中检测到grounded进入Land，Land播放倍率4、退出时间0.85，以短暂过渡恢复Ground；移动混合时阶段长度由混合Clip决定。Land可被下一次起跳打断，支持连续跳跃。过渡0.12秒，无AnyState自转换。保留本地动作推导的采样局限。

只有一层，无Mask，所有状态使用统一骨骼通道。动画不影响事实坐标、Root Motion（关闭）、Yaw或LateUpdate Head Pitch。删除Avatar时重置Driver的边沿记录和计数。

## 插值 Profile

新增 `config/interpolation_profile.json`，默认 `smooth`、500ms安全缓冲。支持smooth=500ms、normal=300ms、fast=150ms，可选buffer_ms在100~1000ms内明确覆盖mode默认值。切换mode时若希望使用该模式默认值，应同时删除旧的buffer_ms。

配置在创建Renderer时读取，修改后需要重启7DTD；不声称支持热切换。旧配置缺少Profile时保留avatar_motion.json中的延迟；非法Profile保持已验证的motion延迟。Profile不修改发送频率、协议、Bridge或网络revision。normal／fast降低延迟但可能在约500ms采样下更频繁地保持位置，smooth适合当前频率。

## Inspector

7DTD F1执行 `mc7dtd_entity_inspect` 或 `mc7dtd_avatar_inspect`。新增 `avatar_animation_debug`：animation_state、animator_state、speed、blend、pose、interpolation_profile、buffer_ms。

animator_state读取实际Controller／Clip；blend为实际Run权重；pose在过渡时为transition、空中为airborne，地面通过实际手臂方向检查为normal／abnormal。它不是每帧修正器，仅在Inspector查询时诊断。

## 验证与复测

自动验证真实Unity资源的单层／无Mask、Write Defaults、统一旋转通道、Blend Tree和空中流程；原生7DTD验证Idle手臂下垂、实际Walk／Run摆动、中速连续混合、四轮Jump／Fall／Land恢复、无手臂残留、层权重、配置加载与三个Profile、缓存清理及此前Entity／Avatar／插值回归。

构建／测试：`tests/run-phase3_8_8.ps1`。人工：`tests/launch-phase3_8_8.ps1`，两个既有世界，观察站立、普通移动、快速移动、连续跳跃／落地、头部视角、退出／重进。完整实机证据与逐项结果见本阶段验收报告。

本阶段无攻击、武器、Combat、Inventory、跨端Equipment；不修改Entity协议、Authority、Presentation、Equipment或Bridge。

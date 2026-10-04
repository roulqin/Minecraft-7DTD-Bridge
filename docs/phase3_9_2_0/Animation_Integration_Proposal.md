# Animation Integration Proposal — Phase 3.9.2.0

状态：设计。没有新增动画资源或修改 Animator Runtime。

## 现状

Phase 3.9.1 使用每 Avatar 的 AnimatorOverrideController，以 Holding 布尔值切换 HeldWalk／HeldRun；摆臂幅度分别8°／4°。空手恢复原 Walk／Run，Idle 与 Jump／Fall 保持既有流程。当前方案不能区分左／右单手、双持和双手武器。

Avatar Animator 当前使用 Generic Transform 动画；已有 Humanoid Avatar 资源不意味着当前 Controller 使用 Humanoid Retargeting。不得直接套用未验证的 Humanoid AvatarMask。

## Animation Modifier 接口

```csharp
// 文档草案，本地派生，禁止写入 Action／网络。
EquipmentAnimationProfile Resolve(
    ResolvedLayout layout,
    IReadOnlyList<VisualBindingState> actualBindings,
    AvatarActionState action);
void Apply(Animator animator, EquipmentAnimationProfile profile);
void Clear(Animator animator);
```

Profile 含 key、primary_hand、握持类型、Clip 覆盖集合、过渡时间与降级原因。Action State 只读；装备不能制造 Jump、攻击或改变 grounded。有效持物以实际已绑定对象为准，fallback Marker 也可持物；没有对象的 unavailable 条目不能迫使角色摆出持枪姿态。

## Profile 规则

| 有效布局 | Profile | Idle | Walk／Run |
|---|---|---|---|
| 双手空／仅头背身体腰装备 | empty | 原自然站立 | 原动画 |
| 仅右手武器／工具 | one_hand_right | 自然单手握持 | 右臂减摆，左臂自然 |
| 仅左手武器／工具 | one_hand_left | 左手握持 | 左臂减摆，右臂自然 |
| 左右独立单手装备 | dual_hand | 双持自然站立 | 两臂受控减摆 |
| 双手武器 | two_hand | 预制作支撑姿态 | 主手稳定，副手接近支撑位 |
| 左盾＋右单手 | shield_and_weapon | 盾牌自然持握 | 两手各用对应姿态 |

只有盾牌时使用左手 shield Profile。上述名称是资源合同，不表示对应 Clip 已生产。具体弯曲与握持位置需 Alex Slim 实机校准；不得恢复 T-Pose。

双手武器只绑定主手一个对象，副手支撑通过动画资源表现，不同时将对象设两个父节点。本阶段不实现精确 IK、攻击、瞄准或武器挥舞。

## 与 Animator 状态机关系

保留已有 Ground Blend Tree、JumpStart、JumpLoop、Fall、Land，以及速度／落地状态来源。装备 Profile 是动作上的表现修饰，不新增动作同步字段。

优先扩展已有 Override 方案：为各 Profile 制作一致骨骼路径与完整预期曲线的 Clip，批量 ApplyOverrides，保留当前状态与播放进度。不要每帧强制重置骨骼，也不要在装备切换时调用 Animator.Rebind 或重触发 Jump。

第一轮不引入新 Animator Layer。若后续确需上半身 Layer，先验证 Generic Transform Mask 的路径，明确排除 root、腿、head，并测试 Weight 恢复；不能借 Mask 隐藏错误资源。

## 姿态与动画边界

- Idle：自然下垂基础上保持握持，不横向张臂。
- Walk：持物侧减小摆幅，未持物侧保持自然。
- Run：进一步减摆；腿部与移动速度逻辑沿用原实现。
- Jump／Fall／Land：装备稳定跟骨骼，已有起跳／落地流程不重启；确需持物 Clip 时必须完整验证着地恢复。
- Head Pitch：不被装备 Clip 覆盖；现有头部跟随继续生效。
- Position／Yaw／Ground Alignment：不受 Modifier 改写；Root Motion 保持关闭。

建议 Profile 改变时采用约0.12秒可配置混合，最终值由实机确认。镜像左手动画采用验证过的独立曲线／资源，不对 Skeleton 负缩放。

## 清理与回退

Remove 任一手部装备后重新推导 Profile；另一手仍持物时不能直接回 empty。只卸背包或头盔不切换手部 Profile。双手装备完整移除后解除副手约束。

缺 Profile Clip 时回退现有 HeldWalk／HeldRun 或原动画，并显示 animation_profile_fallback；不得让缺资源阻断装备创建。fallback 动画不宣称精确双手支撑成功。

Clear／Despawn／世界切换恢复原 Controller 与 Clip，释放私有 Override。一个 Avatar 的配置不能改变其他 Avatar 的共享 Controller。异步资源结果受本地 session 检查约束。

## Inspector

保留 avatar_action 与实际 Animator 状态，新增 equipment_animation：requested_profile、active_profile、primary_hand、status、reason。这样可以区分动作推导 walking、装备 Profile two_hand 与实际播放 Walk。

## 后续资源验证重点

空手→单手→双手→空手；走跑中替换；JumpLoop 中替换；Land 后恢复；左右手镜像；移除一个双持条目；缺 Clip 降级；重复 Apply 幂等；断线恢复 Controller。所有路径保持头、腿、插值和 Action 回归。

详见 [测试计划](Test_Plan.md)。

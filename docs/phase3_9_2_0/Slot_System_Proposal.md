# Slot System Proposal — Phase 3.9.2.0

状态：设计，不修改当前配置或 Runtime。

## 六槽与骨骼

| 配置／Inspector键 | 对外 Socket 名 | 当前内部骨骼 | 用途 |
|---|---|---|---|
| right_hand | RightHand | rig_hand_right | 主手武器、工具 |
| left_hand | LeftHand | rig_hand_left | 副手、盾牌 |
| head | Head | rig_head | 头盔、帽饰 |
| body | Body | rig_spine | 刚性胸甲、身体装饰 |
| back | Back | rig_chest | 背包、大型武器收纳 |
| waist | Waist | rig_hips | 腰包、挂件 |

不改变 Avatar Skeleton。back 示例中的 Spine 只能作为显式配置选择；当前 rig_chest 绑定不得被迁移时默默替换。held_item 的默认视觉落点保持 right_hand。事实 hands 不等于副手，feet 不纳入本次六槽扩展。

## AvatarSocket Schema

```json
{
  "name":"right_hand",
  "bone":"RightHand",
  "position_offset":{"x":0.08,"y":0,"z":0.12},
  "rotation_offset":{"x":0,"y":0,"z":0},
  "scale":0.8,
  "mirror":false
}
```

bone 是逻辑骨骼标识，经显式表解析到内部 rig 节点。允许已知内部名称作为兼容值；找不到或重复匹配必须报告错误，不能随便选择第一个 Transform。

位置以骨骼局部空间米为单位；模型制作可用 Minecraft 1像素=1/16单位约定，但最终资源需统一尺度。rotation_offset 使用 Unity Quaternion.Euler 的角度约定；scale 必须有限且为正，沿用当前安全上限10。所有分量拒绝 NaN／Infinity。

当前配置字段为 offset／rotation；未来读取层可以在内存中将它们映射为 position_offset／rotation_offset。新旧键同时存在且不一致时拒绝候选配置，保留最近有效配置。本阶段不重写配置。

mirror 表示选择预验证的镜像模型或镜像姿态预设。禁止直接对整个 Avatar 或挂件随意设置负 scale；否则可能反转法线、UV 和握持方向。无镜像资源则回退并给出 mirror_unsupported，不假装左右手完全对称。左右 Socket 仍拥有各自偏移。

组合顺序：bone.world × socket.local × model.gripCorrection。模型需声明握持原点。对象只跟随父骨骼，不改 Entity Transform、Motion Root 或 Head Pitch。

## Slot Occupancy Manager

职责：验证请求、决策占用、输出不可变布局及冲突原因。它不创建 GameObject，不写装备事实，不控制网络。

```csharp
// 接口草案，仅文档定义。
ResolvedLayout Resolve(IReadOnlyList<EquipmentDescriptor> entries);
EquipDecision TryEquip(LocalEquipRequest request, ConflictPolicy policy);
ResolvedLayout RemoveEntry(string entryKey);
```

每槽最多一个 owner。条目必须一次性取得 occupied_slots 全部槽位；不得半成功。每条装备仅在 primary_slot 创建一个视觉对象，其余槽为 reservation。

| 请求 | 占用 | 对象位置 |
|---|---|---|
| 单手武器／工具 | right_hand 或显式 left_hand | 请求手 |
| 盾牌 | left_hand | left_hand |
| 双手武器 | right_hand + left_hand | right_hand，只有一个对象 |
| 双持 | 两条独立单手请求 | 每手各一个对象 |
| 背部收纳武器 | back | back，不占双手 |
| 头盔／胸甲／腰饰 | head／body／waist | 对应单槽 |

## 两种冲突情境

### 只读事实布局

对同一批有效输入进行全量、确定性重算。默认优先级：双手100；盾牌60；单手武器／工具50；护甲／背包40；饰品10。相同优先级按稳定 entry_key 字典序决胜。优先级来自可信本地类型规则，不接受物品任意自报。

因此双手与盾牌冲突时双手胜出，盾牌进入 suppressed_entries；不删除事实输入。双手被明确移除后，仍存在的盾牌重新显现。输入顺序变化不能改变结果。非法重复 entry_key 拒绝该输入批次并报告 duplicate_entry，不作随机选择。

未知映射只允许保守单槽 fallback；无法验证的双手声明不能占用另一只手。非法槽位拒绝该条目，保留其他有效条目。

### 本地调试装备事务

默认 RejectConflicts：冲突时保留全部旧本地输入和对象，返回 owner／槽位信息。只有显式 ReplaceConflicts 才移除冲突的完整装备条目，然后提交新条目。不能借调试命令修改远端事实。

例如盾牌已占左手，装备双手步枪：默认拒绝；显式替换删除盾牌条目并占双手。之后卸步枪不会让已删除的本地盾牌自动复活。

清空 reservation 槽默认返回 reserved_by_owner，并提示主槽；RemoveEntry(owner) 或显式 clear_owner 操作才释放完整双手条目，不能留下半占用。

## 提交与失败

布局与视觉对象由 SlotRenderer 协调提交。资源准备失败且 fallback 成功，可以提交 fallback 对象；缺骨骼不能创建悬浮对象。

本地 TryEquip 失败保留旧状态。新事实快照已明确替换旧装备时，不能保留过期旧视觉冒充新事实：移除旧对象，新条目显示 unavailable。明确 Remove 永远优先清理，不依赖新资源加载是否成功。

占用清理覆盖 Spawn／Remove／Despawn／Disconnect／World Change。重连采用新绑定，不保留旧骨骼引用。双手卸载只 Destroy 一个对象并释放两个 claims。

详见 [Renderer 方案](Renderer_Extension_Proposal.md) 和 [测试计划](Test_Plan.md)。

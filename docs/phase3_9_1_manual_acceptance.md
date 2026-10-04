# Phase 3.9.1 Manual Acceptance Steps

状态：人工视觉验收待执行。本次依用户最新指令完成验证与文档后停止，不自动启动游戏。

## 准备

正常退出旧三端，在项目根执行 `.\tests\launch-phase3_9_1.ps1`；进入两个既有世界，等待连接。在7DTD将观察者移到Minecraft Avatar附近。此阶段装备由7DTD本地测试输入驱动，不读取或同步另一游戏实际背包。

7DTD按F1执行 `mc7dtd_avatar_inspect`，复制 **Minecraft Avatar的entity_id**。下列UUID占位符均替换为该ID。Minecraft `/mc7dtd_entity_list` 显示另一方向的7DTD实体，不能将其ID用于此命令。

确认config/debug.json的debug_navigation=true。可用槽名为right_hand、left_hand、back、head、body、waist。未指定槽时held_item保持绑定right_hand。

## A 空手

新生成Avatar六槽为空。若已装备，对所有占用槽执行 `mc7dtd_avatar_test_held <UUID> null <槽名>`。

观察自然站立、双臂下垂；Inspector avatar_equipment固定六槽均显示null。

## B 木棒

执行 `mc7dtd_avatar_test_held <UUID> voxel:club`。

观察木棒在右手。让Minecraft站立、慢走、跑步、跳跃；确认持物走跑摆臂减小、装备不脱离。Inspector right_hand应为item=club、renderer=voxel、socket=right_hand、active=true、attached=true。

## C 枪与风格

执行 `mc7dtd_avatar_test_held <UUID> realistic:pistol`，确认木棒立即消失、手枪握持位置正确。

同一模型可切换 `minecraft:pistol`、`voxel:pistol`、`realistic:pistol`，观察卡片式／方块组合／Unity Mesh区别，并核对实际renderer字段。这些是本地风格模型，不是原生Minecraft ItemDisplayEntity或已加载的7DTD库存Mesh。

## D 背部与多槽

执行 `mc7dtd_avatar_test_held <UUID> realistic:rifle back`，确认大型武器出现在背部且右手手枪保持。

再执行 `mc7dtd_avatar_test_held <UUID> realistic:backpack back`，确认步枪移除、背包出现。

可补充 `mc7dtd_avatar_test_held <UUID> voxel:helmet head` 和 `mc7dtd_avatar_test_held <UUID> minecraft:torch left_hand`，确认多槽共存。Body／Waist为预留挂点，可使用相同命令指定body／waist测试绑定。

## E 切换与空手回退

反复在右手切换club、axe、pistol、torch（任一已配置风格）。确认无旧对象残影，背部不被覆盖。

移除right_hand和left_hand，确认恢复原始Walk／Run摆臂并自然Idle；只剩背部／头部装备不应启用持物手臂Profile。

## F 退出重进

正常退出Minecraft世界，观察7DTD Avatar与全部装备清理。重新进入Minecraft，获取新的有效Avatar ID；确认空手自然，再重新发送本地装备测试输入，确认各槽正常重新绑定。

本地输入不会跨端同步或自动持久化重放；“重新生成正常”验收包括空手重建与重新提供本地输入后的绑定。

记录A～F PASS／FAIL，保存空手、木棒、手枪、背部、多槽Inspector及重连截图。全部实机观察通过后才能将人工视觉状态标记为PASS。

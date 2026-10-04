# Phase 3.9.1 Runtime Visual Acceptance Report

日期：2026-10-04。测试Run：20261004-204526。

**本轮指定的A～E验收：PASS。** 未修改源码、协议或资源，未进入Phase3.9.2。

## 环境与自动定位

启动 `tests/launch-phase3_9_1.ps1 all`，Bridge、Minecraft、7DTD连接成功；debug_navigation=true。使用现有Bridge `/debug/entities` 列表／Inspector诊断自动识别source=minecraft的Avatar ID，再通过computer-use输入7DTD `mc7dtd_avatar_inspect` 与本地装备命令。用户没有复制任何UUID。

当前世界启动时已有近距离观察位置，Avatar与观察者约4.3米，直接可见，因此未额外执行goto或传送。用户只协助原地转身，让右手不被身体遮挡；走跑跳约15秒。

首轮有效Avatar：`83a45c6c-7127-4e4d-8f5e-cf4a45479212`。重进后Avatar：`3183e929-6f30-40cd-b325-99b4e63272c8`。该ID变化已自动处理，重绑使用新ID。

## 逐项结果

| 项目 | 结果 | 证据与依据 |
|---|---|---|
| A 空手 | PASS | Avatar可见、Idle双臂下垂，无异常张开；截图01 |
| B 木棒 | PASS | voxel:club出现在右手，观察角度修正后可见，无脱离；截图03与命令日志 |
| B 火把替换 | PASS | minecraft:torch替换木棒，旧木棒消失；Inspector尾部记录RightHand与held_item_local_test；截图04、05 |
| C Walk／Run | PASS | 用户实际观察确认装备始终跟手、姿态正常；双游戏移动截图07与40条Transform采样辅助佐证 |
| D Jump／落地 | PASS | 用户确认姿态正常、落地恢复，无漂浮／残影；采样Y从63升至64.252203再回63 |
| E 退出清理 | PASS | 正常保存退出，7DTD记录world_unloaded、despawn、count=0；Bridge列表不再有Minecraft实体，画面无Avatar与装备 |
| E 重进／重绑 | PASS | 新spawn sequence=1，仅一个Minecraft实体；先空手自然生成，再通过新ID重新绑定火把，无旧装备重复 |

用户关于C／D的原话：

> 装备始终跟手、姿态正常、落地恢复、无漂浮/残影

走跑跳瞬间的完整Animator状态没有逐帧截取；这些视觉结论以用户实机观察为主，Transform采样辅助验证真实移动与起跳，不能单独代替Animator视觉证据。

## 截图

- [空手Idle](01-empty-idle.png)
- [右手木棒](03-right-hand-club.png)
- [火把Inspector尾部](04-torch-inspector-tail.png)
- [右手火把，木棒已移除](05-right-hand-torch.png)
- [用户操作时双游戏移动画面](07-motion-dual-view.png)
- [退出后清理，Minecraft回主菜单](10-world-exit-cleanup.png)
- [重进后空手Avatar](12-respawn-empty-idle.png)
- [重绑火把Inspector尾部](13-respawn-inspector-tail.png)
- [最终重绑画面](16-respawn-rebind-torch.png)

所有截图来自本轮实际游戏窗口，没有使用资源预览替代实机证据。

## 日志与数据

- [定位列表](02-entity-location.json)
- [40条移动采样](06-motion-samples.jsonl)、[采样范围](motion-summary.json)
- [退出后列表](09-after-world-exit.json)、[重进后列表](11-after-reentry.json)、[最终列表](14-final-entities.json)
- [装备命令与生命周期日志](15-runtime-lifecycle-and-commands.log)
- [渲染异常扫描](runtime-error-scan.json)：0条匹配

关键时间：20:50:30创建木棒；20:53:58切换火把；20:58:48旧Avatar despawn count=0；20:59:57新Avatar spawn；21:01:41新ID火把重绑。

## 发现的问题与范围

本轮未发现装备漂浮、旧对象残影、重复Avatar／装备或落地姿态残留。没有定量测量网络延迟；用户观察跟随正常。

现有7DTD控制台持续刷日志，完整Inspector容易滚出可视区域；已通过自动ID与命令输入绕过人工复制困难，并保存可读尾部截图。初始右手被身体遮挡，通过用户原地转身排除视角遮挡。

火把为带黄色端部的本地测试物品，没有火焰／灯光效果。装备在静止时偏水平，精细握姿与模型朝向未列为本轮通过标准。新Avatar先以空手重建，本地装备输入不持久化；测试工具再次注入后正常绑定。

本轮仅覆盖空手、木棒、火把、Walk／Run、Jump及清理重绑；不宣称手枪、步枪、背包、头盔、全部六挂点均完成本轮人工视觉验收。相关范围仍有此前自动测试与单独人工步骤。

结束时三端仍运行，7DTD控制台已关闭，当前新Avatar绑定火把。没有继续开发下一阶段。

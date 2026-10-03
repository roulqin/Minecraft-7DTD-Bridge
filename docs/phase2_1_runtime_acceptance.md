# Phase 2.1 非默认坐标映射实机验收

日期：2026-10-03（Asia/Shanghai）
工程目录：D:\wenjian\minecraft\7-M

## 结论

验收通过。真实 Minecraft 玩家移动后，七日杀真实游戏进程的 Mod 日志收到经过比例与三轴偏移转换的坐标。Bridge 重启后两个真实 Mod 自动重连，原双向测试消息仍正常。

没有修改源码或重新编译，没有实体或方块操作。核对 29 个源码、构建文件、脚本及网络配置文件的 SHA-256，全部未变。测试结束已逐字节恢复原 coordinate.json，并重启 Bridge 回到默认映射。

## 本次配置

```json
{
  "scale": 2,
  "offsetX": 100,
  "offsetY": -10,
  "offsetZ": 25
}
```

计算公式：x_out=x_in*2+100，y_out=y_in*2-10，z_out=z_in*2+25。

13:42:11 Bridge 启动日志明确显示 scale=2、offsets=(100,-10,25)，13:42:13 两个游戏连接并双向转发测试消息。正式端口一直为 localhost:18771，未使用模拟角色客户端或测试程序生成位置。

## 实际游戏与操作

Minecraft Java 1.21.11，Fabric Loader 0.18.4，Fabric API 0.140.2+1.21.11，Java 21.0.12；工程 runtime/minecraft 中的真实试玩单人世界。七日杀 V3.2.0 b10，工程 runtime/7dtd 中的标准 Mod，在真实游戏主菜单接收消息。

两个已运行的工程游戏实例继续使用（Minecraft PID 30564、七日杀 PID 29872），没有启动重复进程。非默认配置 Bridge PID 6924，恢复配置后 PID 20156。

恢复 Minecraft 暂停的世界，记录 F3 原始坐标。由于自动按键无法持续按住移动键，用户在实际游戏中移动约 5 秒并回复已移动；随后停在原地，保存 F3 画面和七日杀日志。

## 坐标对照

| 时点 | Minecraft F3 原始显示 | 七日杀实际收到 |
| --- | --- | --- |
| 移动前 | (-34.152, 64.000000, 37.300) | (31.695467439155152, 118, 99.600000023841858) |
| 移动后 | (-29.488, 64.000000, 29.593) | (41.0231406312065, 118, 84.186503041543745) |

移动后的 F3 显示值计算得到 (41.024,118,84.186)。与实际接收值的绝对差分别为 0.000859369、0、0.000503042。F3 的 x/z 仅显示三位小数，按 scale=2 缩放，允许显示舍入带来的误差不超过约 0.001；两轴均在此范围内。游戏内显示和完整精度日志不能要求逐位一致。

七日杀日志样例：

```text
2026-10-03T13:42:40 ... [MC7DTD] Minecraft player: x=31.695467439155152 y=118 z=99.600000023841858
2026-10-03T13:43:08 ... [MC7DTD] Minecraft player: x=41.0231406312065 y=118 z=84.186503041543745
```

Bridge 日志显示相同的转换结果，最后十进制位的不同打印形式属于两个运行时的数字格式化差异。

保存的 13:42 至 13:43 七日杀日志片段有 167 条位置记录、18 个不同位置，包含移动过程及移动后稳定值。对照计算见 mapping-comparison.json，passed=true。结果并非仅有一次连接或固定测试坐标。

## 证据文件

目录：docs/phase2_1-runtime-evidence/。

- coordinate-original.json：原默认配置备份。
- coordinate-tested.json：本次非默认配置。
- minecraft-before.png、minecraft-after.png：移动前后 F3 画面。
- 7dtd-mapped-console.png：真实七日杀游戏内转换后位置日志画面。
- bridge-before-test.log、bridge-nondefault.log：测试前及非默认配置运行日志。
- 7dtd-game.log、minecraft-latest.log：实际游戏日志快照。
- mapping-comparison.json：原始显示值、期望结果、实际接收、误差和样本统计。
- health-nondefault.json：非默认配置时两个真实游戏角色在线。
- bridge-restored.log、health-restored.json：恢复默认配置后的日志和连接状态。
- source-before.json、source-after.json：29 个文件哈希核对。

## 文件修改与复验

新增本报告 docs/phase2_1_runtime_acceptance.md 和上述证据；docs/phase2_1_coordinate_mapping.md 追加本次实机结论。config/coordinate.json 在测试中临时改变，结束已恢复原内容。现有启动脚本更新 docs/runtime-evidence 的 Bridge 运行日志/进程记录，游戏更新 runtime 内日志和测试存档；没有修改其他项目或游戏安装目录。

复验：先备份 coordinate.json，写入本报告的非默认配置，停止工程 Bridge 后使用原 start-bridge.ps1 启动；沿用现有游戏实例或原启动方式。Minecraft 进入世界，记录 F3，移动并停止；对照七日杀 Minecraft player 日志与原坐标乘 2 再加偏移的结果。结束恢复配置并重启 Bridge。

## 结束状态与限制

默认 scale=1、三轴偏移为0 已恢复；13:44:15 两个真实 Mod 再次自动重连并双向交换 test，health 同时列出 minecraft 和7dtd。Bridge 继续运行，七日杀留在主菜单。

本次是位置接收实机验证，没有进入七日杀存档或生成实体。Minecraft 使用真实试玩客户端；没有验证正式账号多人环境、长时间稳定性或坐标与七日杀地形对应。移动期间 y 保持64，已验证对应输出118；没有单独做垂直运动测试。

验收完成，到此停止，不进入实体同步。

# Phase 2.1 — Coordinate Mapping Layer

完成日期：2026-10-03（Asia/Shanghai）。工程根目录：D:\wenjian\minecraft\7-M。

## 结果

Bridge 已加入 CoordinateMapper 模块。每条合法 player_position 在发送给七日杀前，都先经过坐标转换。默认配置保持 Phase 2.0 行为，WebSocket 地址仍为 ws://localhost:18771/ws，连接/重连逻辑、测试消息和位置消息格式均保留。两款游戏 Mod 没有修改。

本阶段只转换坐标并继续输出七日杀日志，不生成实体，不同步方块。

## 架构与配置

数据链路：Minecraft 原始坐标 → Bridge 验证来源与有限数值 → CoordinateMapper → 原 player_position JSON → 七日杀日志。

config/coordinate.json 默认内容：

```json
{
  "scale": 1,
  "offsetX": 0,
  "offsetY": 0,
  "offsetZ": 0
}
```

转换顺序为先缩放，再偏移，三个轴使用同一个比例：

```text
x_out = x_in * scale + offsetX
y_out = y_in * scale + offsetY
z_out = z_in * scale + offsetZ
```

不交换轴、不反转某个特定轴、不舍入。scale=0 会将所有位置映射到偏移点；负比例会反转三个轴。所有四个值必须存在，且为有限 JSON 数字；字符串、null、缺失值、非法 JSON 和非有限数值会阻止服务启动，避免错误配置悄悄退回默认值。配置在启动时读取一次，没有热加载。

Bridge 使用现有命令行 network.json 的所在目录查找 coordinate.json。自定义网络配置目录时，两份配置必须放在同一目录。默认部署已提供这两份文件；旧的独立测试目录也需要提供坐标配置。

转换结果仍为：

```json
{
  "type": "player_position",
  "source": "minecraft",
  "x": 97.5,
  "y": 54,
  "z": 8.25
}
```

该示例输入为 (-2.5, 64, 8)，scale=1，offsetX=100，offsetY=-10，offsetZ=0.25。source 保留 minecraft，表示数据来源，数值已是映射后的目标坐标；未增加协议字段。

即使输入和配置都是有限数字，乘加仍可能溢出。模块检查转换结果，出现无穷值时按现有协议拒绝方式返回 error/code=coordinate_mapping_overflow 并结束该会话，不发送无法序列化的坐标。正常 test 消息完全不经过映射。

## 修改文件

新增：

- config/coordinate.json：默认坐标配置。
- bridge-server/CoordinateMapper.cs：配置读取、转换和有限数值检查。
- tests/coordinate-runner/CoordinateRunner.csproj。
- tests/coordinate-runner/Program.cs：映射及 WebSocket 集成测试。
- tests/run-phase2_1.ps1：测试入口。
- docs/phase2_1_coordinate_mapping.md：本报告。

修改：

- bridge-server/Program.cs：启动加载配置、转发前映射、启动日志。
- tests/phase1-runner/Program.cs：在各自隔离测试目录生成恒等映射配置，使历史测试不依赖用户当前的比例/偏移设置。
- README.md：增加配置、测试及报告入口。

生成文件：Bridge 和测试项目 bin/obj，work/phase2_1-test 中配置、日志与 results.json；历史测试各自更新 work/phase1-test 和 work/phase2-test。现有启动脚本更新 docs/runtime-evidence 的运行记录。全部位于工程根目录内。

## 编译与自动测试

在工程根目录执行（现有 .NET SDK 10）：

```powershell
Set-Location 'D:\wenjian\minecraft\7-M'
.\tests\run-phase2_1.ps1
.\tests\run-phase1.ps1
.\tests\run-phase2.ps1
```

Phase 2.1 入口会构建测试项目及引用的 Bridge 项目，无需重编译 Minecraft/七日杀。完整三组件编译仍可使用原 build.ps1。

实际结果：Bridge Release/net10.0 编译成功，0 警告、0 错误；Phase 2.1 共 34 项通过，Phase 1 回归 14 项通过，Phase 2.0 回归 25 项通过。三份 results.json 均 failure=null。

映射集成测试向真实 Bridge WebSocket 发送输入 (-2.5, 64, 8)，逐一断言七日杀角色接收的数据：

| 场景 | scale | offsetX / offsetY / offsetZ | 期望输出 |
| --- | --- | --- | --- |
| 默认 | 1 | 0 / 0 / 0 | (-2.5, 64, 8) |
| 偏移 | 1 | 100 / -10 / 0.25 | (97.5, 54, 8.25) |
| 比例 | 2 | 0 / 0 / 0 | (-5, 128, 16) |
| 组合 | 0.5 | 10 / -20 / 30 | (8.75, 12, 34) |

每个场景还检查两个角色欢迎消息、位置五字段格式和双向 test 转发。附加检查覆盖无效配置、非有限输入及计算溢出；历史回归继续覆盖约 500ms 发送、客户端源码日志、服务器重启自动重连、非法消息和方块消息拒绝。

测试实例使用工程内隔离配置和空闲本地端口，不修改 config/network.json 的正式端口。结果：work/phase2_1-test/results.json；各场景 Bridge 日志位于同目录 default、offset、scale、combined 子目录。

## 启动与手动验证

```powershell
.\start-bridge.ps1
```

当前 Bridge 已运行，请勿重复启动。修改 coordinate.json 后停止并重新启动 Bridge，启动日志应显示所用比例、偏移和配置路径。Minecraft 与七日杀沿用原启动方式；Minecraft 进入世界并移动后，七日杀 Minecraft player 日志应等于原始坐标乘比例再加偏移。

本次构建前正常测试操作停止工程内旧 Bridge，构建后用原启动脚本启动新版，正式端口仍为 18771。13:37:04 启动日志确认默认 scale=1、offsets=(0,0,0)，13:37:05 两个真实游戏 Mod 自动连接并交换双向测试消息，health 同时列出 minecraft 和 7dtd。运行证据见 phase2_1-evidence/bridge-startup.log 和 health.json。

本次偏移/比例转换由自动 WebSocket 集成测试验证，未重新做非默认配置的游戏内移动实测；Minecraft 保持之前的暂停状态。Phase 2.0 的真实采集、移动、暂停与重连证据仍见 phase2_runtime_acceptance.md，不将本轮模拟角色测试当作新增实机移动证据。

## 限制与停止点

仅支持 Minecraft → 七日杀位置的统一比例与三轴偏移，不提供逆向转换、旋转、维度映射或各轴独立比例。配置重启生效；本层不能保证两个游戏世界地形对应。沿用单角色连接和丢弃历史样本的行为，不增加玩家身份、实体或方块操作。

Phase 2.1 已完成，到此停止，等待下一阶段确认。

## 2026-10-03 非默认映射实机补充

非默认 scale=2、offsets=(100,-10,25) 已通过真实玩家移动验收，七日杀日志与 F3 原始坐标的转换结果在显示舍入误差内一致。完整证据、误差计算和范围见 [实机验收报告](phase2_1_runtime_acceptance.md)。测试结束恢复原默认配置并重启 Bridge，源码未修改。本补充更新上文非默认配置尚未实机移动测试的历史状态。

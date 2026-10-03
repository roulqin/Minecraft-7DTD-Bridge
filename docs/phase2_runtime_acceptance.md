# Phase 2.0 实机验收报告

日期：2026-10-03（Asia/Shanghai）
工程根目录：D:\wenjian\minecraft\7-M

## 结论

本阶段要求的三项剩余验收全部通过：真实 Minecraft 玩家移动后的坐标变化、单人世界暂停/恢复采集、Bridge 重启后的自动重连。七日杀真实游戏进程收到位置并写入日志，原有双向测试消息仍可交换。

没有修改源代码、构建配置、连接逻辑或端口；没有重新编译，也没有进入 Phase 2.1、方块同步或实体同步。对之前保存的 27 个源码、脚本和配置文件逐一核对 SHA-256，全部一致。

## 实际环境

- Minecraft Java 1.21.11，Fabric Loader 0.18.4，Fabric API 0.140.2+1.21.11，Java 21.0.12。
- Minecraft 使用工程 runtime/minecraft 中的独立试玩单人世界 Demo World，真实客户端采集，未用模拟客户端生成坐标。
- 七日杀 V3.2.0 (b10)，Steam 登录成功，真实游戏主菜单运行 Mod；实际加载路径为 runtime/7dtd/Mods/MC7DTD-Bridge/MC7DTD.Bridge.dll。
- Bridge 使用现有 Release/net10.0 构建，地址仍为 ws://localhost:18771/ws。
- Minecraft PID 30564，七日杀 PID 29872；Bridge 重启前 PID 22356，重启后 PID 31028。

## 验收结果

| 项目 | 实际操作与证据 | 结果 |
| --- | --- | --- |
| 玩家移动 | 用户在真实 Minecraft 世界按住 W 移动；F3 起点约 (-45.886, 64, 50.428)，终点约 (-34.152, 64, 37.300)。七日杀日志出现沿途变化，终点为 x=-34.152266280422424 y=64 z=37.300000011920929，与游戏显示一致（显示值有舍入）。 | 通过 |
| 暂停采集 | 打开单人世界暂停菜单；13:26:17.739 至 13:26:44.369，共 26.630 秒，两次位置记录计数均为 256。两次 health 查询均同时包含 minecraft 和 7dtd。 | 通过 |
| 恢复采集 | 点击返回游戏，13:27:04 位置记录计数增加到 284，七日杀继续输出当前位置。 | 通过 |
| Bridge 重启 | 13:27:15 停止已核对路径的工程 Bridge，13:27:28 使用原启动脚本启动同一构建。七日杀 13:27:28、Minecraft 13:27:30 自动重连，无需重启游戏；13:27:30 恢复坐标转发。 | 通过 |
| 原通信回归 | 重连后 Bridge 同时记录 Test forwarded: 7dtd -> minecraft 和 Test forwarded: minecraft -> 7dtd，两个游戏也记录对端测试消息。 | 通过 |
| 约 500ms 节奏 | 从七日杀日志的高精度运行时间提取最后连续 21 条位置的 20 个间隔：最小 500ms，最大 547ms，平均 510.65ms。 | 通过，存在 tick 调度抖动 |

保存的七日杀日志快照包含 360 条位置记录、20 个不同位置。统计范围包含本轮及中止前的实机采集；频率统计仅使用最后连续片段，不包含暂停和服务停机间隔。

## 关键日志

```text
13:27:28 Bridge connected — listening ws://localhost:18771/ws
13:27:28 7DTD connected
13:27:30 Minecraft connected
13:27:30 Test forwarded: 7dtd -> minecraft
13:27:30 Test forwarded: minecraft -> 7dtd
13:27:30 Player position forwarded: minecraft -> 7dtd (-34.152266280422424, 64, 37.30000001192093)
[MC7DTD] Minecraft player: x=-34.152266280422424 y=64 z=37.300000011920929
```

以上为日志内容摘录，完整原始日志保存在下述证据目录。

## 文件与证据

新增报告：docs/phase2_runtime_acceptance.md。
更新说明：docs/phase2_player_sync.md 追加实机验收结论。
新增/更新证据：docs/phase2-runtime-evidence/，包括：

- player-before-move.png、player-after-move.png、player-paused.png、player-after-reconnect.png：Minecraft 画面。
- 7dtd-position-console.png：七日杀游戏内接收日志画面。
- bridge-before-restart.log、bridge-after-restart.log、7dtd-game.log、minecraft-latest.log：原始日志快照。
- pause-start.json、pause-end.json、resume.json、restart-stop.json、health-after-restart.json：时间、计数和连接状态。
- position-analysis.json：坐标样本及接收间隔统计。
- source-hashes-before.json、source-hashes-after.json：27 个文件的源码/脚本/配置核对。

现有启动脚本自然更新 docs/runtime-evidence 中的 Bridge 日志和进程记录；游戏自身更新 runtime 中的日志、测试存档和状态文件。没有修改其他项目或原游戏安装文件。

## 复验方法

如需重新启动现有测试环境，在工程根目录依次运行：

```powershell
python tests/launch-runtime.py bridge
python tests/launch-runtime.py minecraft
python tests/launch-runtime.py 7dtd
```

当前实例尚在运行，不要重复启动。进入 Minecraft 试玩世界，按 F3 查看坐标，移动并检查七日杀日志 Minecraft player 行。打开单人世界暂停菜单后等待至少 10 秒，记录数应保持不变；返回游戏后应恢复增加。停止工程 Bridge 后按原脚本重新启动，两款游戏应自动重连，原双向测试消息和坐标日志应恢复。

## 问题与范围限制

自动按键按下/释放过快，游戏未采样到移动，因此实际移动由用户完成；坐标采集和网络发送仍由真实 Mod 执行。没有以测试数据替代实际移动。

游戏日志出现外部服务相关信息：Minecraft 试玩账号的认证/Realms 报错，七日杀 EOS 通知连接超时。这些信息未阻断本地 WebSocket 链路。七日杀安装目录存在同名旧 Mod，游戏明确忽略该副本并加载工程测试目录的新 DLL；未修改该安装目录。

本次暂停结论限于 Minecraft 单人世界的真正暂停；没有测试多人服务器暂停菜单。七日杀仅验证真实游戏主菜单进程的 Mod 接收和日志输出，没有进入七日杀存档，也没有生成或操作实体。没有验证长时间运行、多玩家或其他版本兼容性。收到的 x/z 已证实随移动变化；本轮未单独判定跳跃 y 变化。

验收结束时 Minecraft 保持暂停界面，七日杀保持主菜单，重启后的 Bridge 仍在运行。Phase 2.0 到此停止，等待下一步指令。

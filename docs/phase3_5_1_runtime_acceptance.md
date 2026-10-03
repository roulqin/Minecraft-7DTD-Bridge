# Phase 3.5.1：首轮实机验收历史记录

后续已加载最终 JAR 并完成玩家退出生命周期验收，见 [最终验收报告](phase3_5_1_final_acceptance.md)。下文保留首轮中断时的原始现场，不代表当前完成状态。

2026-10-03，北京时间。工程根目录：D:\wenjian\minecraft\7-M。

## 已完成的验证

Minecraft 1.21.11、Fabric Loader 0.18.4、Fabric API 0.140.2+1.21.11，进程 31572；七日杀 V3.2.0 B10，工程内隔离用户数据进程 8288。标准 IModApi 从 runtime/7dtd/Mods 加载 DLL，没有修改安装目录。旧七日杀实例已通过主菜单正常退出。

首次 Bridge 启动因项目已有 Debug 服务占用 18771 失败；确认占用者为本工程 BridgeServer 后停止原服务，使用 Release Bridge 正常启动。最终验收服务 PID 32596，端口始终为 localhost:18771/ws。

Minecraft 进入世界后自动发送 minecraft:player，无需手动测试命令；Bridge 创建 Registry 记录，七日杀主线程创建静态人形代理。目标进入世界时的 test 重同步路径已出现于真实日志；Bridge 重启清理和自动重新 spawn 也已观察到。

用户手动移动并转动视角，同一身份 `0822a4cf-94a6-4b2f-aaa8-b1d2989aaa69` 的日志如下：

| 状态 | 源位置 | 七日杀位置 | yaw / pitch | 本地代理数 |
| --- | --- | --- | --- | --- |
| 18:26:12 spawn | (-45.51388970501728,68,71.32531693427028) | (-278,61,449) | 80.54817962646484 / 3.149780750274658 | 1 |
| 18:27:34 update | (-44.30000001192093,68,70.30000001192093) | (-276.78611030690365,61,447.97468307765064) | 18.89806365966797 / 3.2998127937316895 | 1 |

两端 rotation 相符，receiver 日志 thread=1。Unity created 日志确认五部件、colliderEnabled=false。坐标使用验收临时 scale=1、offset=(-232.48611029498272,-7,377.6746830657297)，使代理靠近七日杀玩家；实际 CoordinateMapper 未修改。

## 中断和未完成项

工具在拍摄移动后场景时报告：用户物理 Esc 停止了 Computer Use。此后未继续任何游戏输入。

**本报告不是完整实机通过结论。** 尚未完成可视代理与旋转截图、Minecraft 正常退出后的 despawn、Registry count=0 和七日杀同一对象删除的完整对照。自动测试已覆盖这些逻辑，但不能替代实机验收。

实机 Minecraft 使用本轮核心实现构建；之后追加了正常客户端停止回调和两秒网络排空，最终 JAR 编译与自动测试通过，但尚未重启 Minecraft 加载它。七日杀运行 DLL 为最终构建。两份 JAR 与 DLL 哈希见 artifact-hashes.json，不把不同构建混为同一个验收对象。后续需要部署最终 JAR 并重新启动 Minecraft，再补完进入、移动/旋转和退出三事件。

## 自动验证

玩家采集 16 项、玩家代理接收 26 项、marker 回归 24 项、生命周期回归 87 项通过；类型配置 9 组、原协议示例 29 项通过。Bridge、七日杀、Fabric 与测试工具构建成功。运行 tests/run-phase3_5_1.ps1 可复验。

## 清理和证据

已保存 minecraft-latest.log、7dtd-game.log、bridge-test.log、build.log、java-results.json、receiver-automatic-results.json、coordinate-test.json、原配置和 artifact-hashes.json 到 docs/phase3_5_1-runtime-evidence。runtime-status.json 记录 partial、位置/旋转变化及未完成删除验收。

验收 coordinate.json 按原字节恢复为默认 scale=1、offset=0；停止本轮 Bridge 服务，以触发现有目标断线清理并防止继续产生验收对象。没有通过 UI 验证停止后的场景状态，不把该清理当作玩家退出 despawn 成功。游戏窗口保持当前状态，未继续操作。

实现与修改文件清单见 phase3_5_1_player_proxy.md。停止在 Phase 3.5.1，不进入后续阶段。

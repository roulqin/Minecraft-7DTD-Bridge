# Phase 3.8.2.2 — Proxy Appearance Update v1

日期：2026-10-04，Asia/Shanghai。开发及自动验收完成；真实游戏画面验收待执行。

## 完成结果

Minecraft 代理增加本地手持和头部标记。保持数据路径：

```text
已接受的 Equipment Component
    → EquipmentVisualState
    → ProxyAppearanceAdapter
    → ProxyAppearanceController
    → MinecraftProxyScene 的本地 BlockDisplay 标记
```

场景只取得 Inspector 的 typed EquipmentVisualState，不直接读取 Equipment。Adapter 根据目标本地配置产生不可变 ProxyAppearance；Controller 管理 create/update/delete；所有游戏对象创建、位置旋转更新及删除由既有客户端 tick/代理 Scene 生命周期执行。网络线程只更新本地 Appearance 可用性标志，不操作游戏实体。

本版用 Minecraft 内置方块做简单表现标记，没有真实玩家模型替换、骨骼、动画、第一人称装备或复杂装备模型。基础 body/nose、Health、Identity、Presentation、Authority、碰撞和同步协议保持原实现。

## 第一版标记

| 槽位 | 来源物品 | Appearance | 代理可观察变化 |
|---|---|---|---|
| held_item | 木棒 | held_marker=club | 代理侧面棕色细条（橡木板） |
| held_item | 铁斧/钢斧/石斧 | held_marker=axe | 代理侧面银色块（铁块） |
| held_item | 火把 | held_marker=torch | 代理侧面亮色细条（萤石） |
| head | 简易头盔 | head_marker=helmet | 代理头顶银色薄板（铁块） |
| body/hands/feet | 任意 | 无 | 不创建标记、不改变基础代理 |

标记跟随当前代理位置与 yaw/pitch/roll；不把 Presentation.scale 当作新的装备缩放控制。本版 torch 是视觉标记，不创建真实火把或世界照明效果。所有标记仅存在于 Minecraft 客户端，不生成服务器装备、不发往7DTD。

每实际代理最多2个标记，总数随原代理256上限有界。配置缺失、非法或 enabled=false 均禁用标记，原同步继续；未知物品不会崩溃，也不会猜模型。该配置启动读取，修改后只需重启 Minecraft。

## 生命周期与 Inspector

- null→item：创建对应标记。
- itemA→itemB：先删旧标记，再建新标记；即使两个不同来源 ID 都映射为 axe，也执行替换。
- item→null、Equipment 整体移除/未知：删除对应/全部标记；全空但已知的 Equipment 仍存在。
- 重复相同 EquipmentVisualState：结果一致，不重复创建。位置变化只更新现有对象。
- despawn、原连接 reset、目标世界退出：随基础代理 delete 清理对象和 Appearance 缓存；重新 spawn/目标世界恢复后从当前已接受数据重建。
- Minecraft→Bridge 断线：沿原 finally/reset 清理；源 peer 不可用：消费原 `peer_unavailable` 错误信号，下一客户端 tick 清理 Appearance，事实组件不被改写。
- 标记 create/update 失败：隔离于基础代理；同一失败输入不逐帧重试，输入改变、代理重建或世界重入后可再次尝试。删除失败记录警告；无论单个对象失败，仍尝试其余清理。

Inspector 增加本地顶层字段，保留空值：

```json
"proxy_appearance": {
  "held_marker": "axe",
  "head_marker": "helmet"
}
```

`equipment_visual_state` 保持显示；`components` 内各事实组件不增加 appearance 字段。Inspector 表示当前派生意图，不声明实际标记创建成功、资源加载成功或真实模型状态。无目标世界时两个本地派生视图可为 null；无装备/禁用/断线时 Appearance 的两标记为 null。

Adapter 的来源物品 ID 仅在本地不可变值中用于区分同类标记替换，不出现在网络消息，也不引入 revision。3.8.2.1 的 `resolution=unmapped` 是符号 Modifier 目录尚无规则；本阶段简单方块标记使用独立的 `proxy_appearance.json` 映射，可以为该 ID 提供 marker。它不把 unmapped 改写为 resolved，不修改 Presentation，也不声称有模型绑定能力。

## 1. 修改文件列表

工程根目录：`D:\wenjian\minecraft\7-M`。以下为本阶段范围，不是相对 Git HEAD 的累计历史差异。

### Minecraft

新增：

- `minecraft-mod/src/main/java/io/mc7dtd/ProxyAppearance.java`：本地不可变派生意图。
- `minecraft-mod/src/main/java/io/mc7dtd/ProxyAppearanceConfig.java`：有界严格配置加载与降级。
- `minecraft-mod/src/main/java/io/mc7dtd/ProxyAppearanceAdapter.java`：只消费 EquipmentVisualState。
- `minecraft-mod/src/main/java/io/mc7dtd/ProxyAppearanceController.java`：标记生命周期及缓存。

修改：

- `minecraft-mod/src/main/java/io/mc7dtd/EntityInspector.java`：typed VisualState 读取、派生 Appearance 显示、Appearance 本地可用性。
- `minecraft-mod/src/main/java/io/mc7dtd/MinecraftProxyScene.java`：简单 BlockDisplay 标记后端、tick 更新与删除。
- `minecraft-mod/src/main/java/io/mc7dtd/MinecraftBridgeMod.java`：启动配置、本地 Adapter/Scene 接线、客户端 tick。
- `minecraft-mod/src/main/java/io/mc7dtd/BridgeClient.java`：仅通知本地 Appearance 连接可用性，消费既有 peer_unavailable/welcome/peer_connected/finally 信号；保留原消息集合、解析、Health/Identity/Equipment/Presentation 接受及 revision 逻辑。

### Components

共享 `components/` 无修改；Equipment Component、Presentation Component、Authority、Health、Identity、EquipmentVisualState 及其生成器/映射目录源码均未修改。新增 ProxyAppearance 是 Minecraft 本地值，不是共享网络 Component。

### Config

- 新增 `config/proxy_appearance.json`：enabled 与 item_id→marker 精确映射。
- 新增 `config/proxy_appearance.schema.json`：独立本地配置契约。

真实 ID 已在安装目录 `Data/Config/items.xml` 核对：`meleeWpnClubT0WoodenClub`、`meleeToolTorch`、`meleeToolAxeT1IronFireaxe`、`meleeToolAxeT2SteelAxe`、`meleeToolRepairT0StoneAxe`、`armorPrimitiveHelmet`。配置也保留用户示例 woodenClub/torch/ironAxe/helmet 别名；采集器不会自动发送这些示例名，映射始终区分大小写。

配置最大64KiB、256条映射；仅允许 club/torch/axe/helmet，重复 JSON 键、未知键、非法类型/ID、尾随 JSON 或超限整体降级为禁用。head 只接受 helmet，held_item 只接受 club/torch/axe；跨槽错误映射被忽略。旧配置及协议 schema 未修改。

### Tests

- 新增 `minecraft-mod/src/test/java/io/mc7dtd/ProxyAppearanceHarness.java`。
- 新增 `tests/run-phase3_8_2_2.ps1`：三端构建和八套 headless 回归入口。
- 新增 `tests/launch-phase3_8_2_2.ps1`：人工验收启动器，支持默认 all 或单端；检查旧实例/端口，部署产物，按时间目录保存日志。

### Docs

- 新增本文件 `docs/phase3_8_2_2_proxy_appearance.md`。

新增10文件，修改4个 Minecraft 文件。验收前后 SHA256 检查确认其余既有受保护文件不变；Bridge 和7DTD源码无修改。

## 2. 编译结果

执行 `tests/run-phase3_8_2_2.ps1`：

| 端 | 结果 | 产物 |
|---|---|---|
| Minecraft Fabric | **Minecraft build PASS** | `minecraft-mod/build/libs/mc7dtd-bridge-0.1.0.jar` |
| Bridge net10.0 | **Bridge build PASS**，源码未修改，0警告/0错误 | `bridge-server/bin/Phase3822/net10.0/BridgeServer.dll` |
| 7DTD net48 | **7DTD build PASS**，采集及源码未修改，0警告/0错误 | `7dtd-mod/bin/Phase3822/net48/MC7DTD.Bridge.dll` |

JAR文件版本仍为0.1.0，验收需由新启动器复制最新构建产物。当前运行实例没有部署/重启；构建结果不等同于实机画面验收。

## 3. 测试结果

```text
Proxy Appearance Create PASS
Proxy Appearance Update PASS
Proxy Appearance Remove PASS
Entity Cleanup PASS
Unknown Item PASS
Regression PASS
```

用户 Test1–9 全部 PASS：VisualState→Appearance、null→axe、axe→torch、torch→null、null→helmet、despawn 清理、重复生成一致、未知物品安全降级、四个既有组件不变。

新增 Appearance 测试 **32项通过**。补充覆盖独立 Equipment patch 不依赖位置 update、同类不同来源物品替换、头盔移除保留手持、整体 remove/readd、Disconnect/reset、peer unavailable不改事实、世界退出/恢复、位置旋转跟随、非支持槽位、错误槽位、配置禁用/缺失/非法/重复/超限、create/update故障隔离、无重试风暴、显式清理、精确stream查找、查询/tick不推进revision及Inspector深复制。

既有回归 **139项通过**：EquipmentVisualHarness24、EntityDebugHarness19、EquipmentHarness14、HealthHarness20、IdentityHarness23、PresentationHarness10、NativeProxyHarness29。总计 **171项通过**。

测试使用真实 Java 接收器、Inspector、NativeProxyController 与 ProxyAppearanceController，并以 FakeMarkers 替代游戏世界后端；实际 Minecraft 方块 API 已通过编译。没有用自动 headless 结果冒充游戏截图。

配置 JSON schema 验证 PASS；启动器 PowerShell 语法检查 PASS。日志/示例/范围核对及三端产物哈希在 `work/phase3822-test/`，输出目录保存冻结副本。

## 4. 人工验收步骤

允许使用 computer-use；本轮只提供步骤，尚未执行真实游戏验收。不做自由游戏或长时间操作。

1. 正常保存退出旧两个游戏，关闭旧 Bridge；保持 `debug.json` 原 false/false。确认本阶段三端构建完成，并检查 `proxy_appearance.json` 的 enabled=true。
2. 在工程根目录执行以下任一启动方式：

```powershell
Set-Location 'D:\wenjian\minecraft\7-M'
.\tests\launch-phase3_8_2_2.ps1
# 或逐端启动：
# .\tests\launch-phase3_8_2_2.ps1 bridge
# .\tests\launch-phase3_8_2_2.ps1 minecraft
# .\tests\launch-phase3_8_2_2.ps1 7dtd
```

默认 all 按 Bridge→Minecraft→7DTD 启动。启动器不强制杀进程；旧游戏或占用端口会明确拒绝，避免加载旧产物。日志位于 `docs/phase3_8_2_2-runtime-evidence/<RunId>/`；Minecraft另有 `runtime/minecraft/logs/latest.log`。

3. 等待 Bridge 日志 Minecraft connected / 7DTD connected，进入两个既有测试世界。7DTD 使用现有石斧、铁斧或钢斧及火把；不要把示例 ironAxe 当作真实游戏内部 ID。
4. Minecraft 执行 `/mc7dtd_entity_inspect`，记录代理 `position` 与 ID、EquipmentVisualState、proxy_appearance。到该代理附近观察；现有坐标映射不会自动把两个玩家放在一起，必要时在允许命令的测试世界使用实际 Inspector 坐标移动/传送。不要因距离很远或区块未加载误判标记失败。
5. 7DTD 先切空手：held_marker=null，代理侧面无装备标记。再选择斧头：held_marker=axe，侧面银色块出现，等待约1–2秒。
6. 切换火把：旧银色块消失，亮色细条出现；Inspector held_marker=torch。日志应有 axe 的 Remove 后跟 torch 的 Create。亮色标记不要求真实照明变化。
7. 切回空工具栏槽：held_marker=null，侧面标记消失。可选切换木棒，核对棕色 club 标记。
8. 若已有简易头盔，正常穿戴后核对 head_marker=helmet 与头顶薄板；卸下后薄板消失，手持槽不变。若存档没有头盔，记录该真实画面子项未执行，不用伪造数据替代。
9. 正常退出7DTD世界：Equipment remove后清空标记，随后基础代理删除；日志出现 Proxy Appearance Cleanup 和原 proxy despawn。重新进入同一世界，新实体 snapshot 恢复，标记与当前装备一致，无旧标记残留。
10. 断开 Minecraft↔Bridge 连接时应在下一客户端 tick 清理对象和缓存；重连 fresh spawn 后重新生成。核对原 Health/Identity/Presentation/Authority 值仍正常，Debug false/false不关闭Appearance。

人工验收应分别保存 Inspector、Create/Remove/Cleanup日志和可见代理截图。只有派生意图正确不能证明真实标记成功创建；只凭 Inspector 不给画面子项 PASS。

## 风险与停止点

- 标记是几何提示，不是装备模型；仅 held_item/head，无护甲组合、骨骼或动画。只覆盖已配置来源 ID，未知物品不显示标记。
- Inspector 输出是派生意图；游戏对象创建故障时可能仍显示 axe/helmet，需结合 marker unavailable 日志及真实画面。
- Equipment与Presentation跨消息仍有中间状态，无ACK/重放机制未更改；客户端标记每tick读当前接受状态，未增加迟滞或过渡帧过滤。
- 突然失去7DTD peer时，现有Bridge不主动广播peer断线；Appearance可在收到原peer_unavailable或自身连接结束信号后清理，不能保证无信号时立即识别。正常世界退出/despawn和自身Disconnect已覆盖。没有新增心跳、超时猜测或修改Bridge协议。
- 标记最多每代理2个；没有性能压力或图形帧率基准。创建/更新失败只在输入改变或代理重建后重试，删除异常会记录警告，不宣称任意后端故障下均零残留。
- 本地Inspector扩展不是Bridge HTTP schema扩展，旧严格顶层格式消费者需识别本地proxy_appearance字段。
- 人工游戏验收、远距离可见性、夜间画面仍待执行。本轮未启动或操作真实游戏，也没有进入Phase3.8.3或任何后续阶段。

开发任务在 Phase 3.8.2.2 停止，等待下一步指令。

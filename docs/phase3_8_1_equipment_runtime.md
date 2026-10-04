# Phase 3.8.1 — Equipment Sync v1

日期：2026-10-04（Asia/Shanghai）。工程根目录：`D:\wenjian\minecraft\7-M`。

## 完成状态与范围

7DTD→Bridge→Minecraft 的 Equipment 数据闭环已实现，三端构建和自动验证通过。真实游戏人工验收待用户执行；自动跨进程 WebSocket 结果不作为真实换装画面或原生装备值采集的验收证据。

实现五槽 head/body/hands/feet/held_item、spawn 同步 snapshot、单槽/多槽 patch、槽位 null、整个 equipment null、恢复后完整 add、退出/重连清理和只读 Inspector。没有装备外观、模型加载、背包同步、战斗、目标游戏写入或双向装备控制。Presentation 表现及代理外形保持原实现。

## 运行结构与约束

### 主线程源采集及发布

新增 EquipmentSample.Read(EntityPlayerLocal)，只从 BridgeMod 的 ModEvents.GameUpdate 回调内调用；实际采样发生在 NativePlayerPublisher 的约500ms发布窗口内，不在每帧或网络线程读取游戏对象。

使用公开 Equipment.GetSlotItem(EquipmentSlots.Head/Chest/Hands/Feet)；body 映射 Chest。手持物使用 Inventory.holdingItemStack，按 IsEmpty()/UsingBareHand() 识别空手，避免将拳头内部物品当作装备。非空值读取 ItemValue.ItemClass.GetItemName() 并加7dtd前缀，保留大小写。数量、品质、耐久、装饰及徽章未采集到协议。

源端将游戏对象转成五槽字符串/null字典，不将游戏引用交给网络。完整采样不可用时返回未知，已有 Equipment 用整体 remove 标记未知；初次未知 snapshot 省略 Equipment。异常不会生成虚假的五槽全空快照。此版本不承诺屏蔽换手过渡帧，需在人工验收中关注是否产生短暂空槽/未知观察。

NativePlayerPublisher 在原 desired 集合中组合 Health、Identity 和 Equipment，沿用一个 revision/base_revision 与当前 entity_sequence。装备已有时仅发送改变的槽；未知/退出时发送 equipment:null；恢复时发送完整五槽。旧健康单通道、Identity字段生成和 Authority/Origin DTO保持原逻辑。正常世界退出先发原共享组件 remove，再发 despawn。

### 保持 Health/Identity 核心源码不变

Bridge 的 HealthComponents.cs、IdentityComponent.cs，Minecraft 的 HealthReceiver.java、IdentityComponent.java，以及7DTD的IdentitySample.cs均未改动。

新增 Bridge EquipmentComponents 与 Java EquipmentReceiver 作为外层组件策略：装备内容先验证，再将去掉 equipment 的信封交给原 Health/Identity reducer。只有旧 reducer 接受对应共享 revision 后才提交装备候选状态。装备值单独存储，读取深复制；没有独立装备 revision，旧 State API仍只保存原Health/Identity值。

装备单独 patch 去掉 equipment 后会成为旧实现禁止的空 patch。因此协调层先校验原 patch 的真实 base_revision，再将现有旧组件的完整、相同副本作为**内部投影 snapshot**交给旧 reducer，在同一 revision 接受。该内部投影不转发、不覆盖线上 mode；转发的仍是原始 equipment patch。它可能使旧 Health/Identity 观察日志重复出现或显示内部 snapshot 模式，值及原有替换/删除规则保持一致。自动测试验证旧组件值相同、共享 revision更新及失败整包不提交。

原生命周期 gate、Registry/Authority、坐标映射和端口不变；despawn/连接清理同时清空装备存储。Bridge转发原消息，Inspector将独立装备存储深复制到可选components.equipment，未知时{}。

### 独立装备权限

为遵守不修改Health/Identity权限，新增 `config/equipment_permissions.json` 及 schema，只授权已启用7dtd:player→minecraft映射的装备publish/store。原 `component_permissions.json` 和解析实现保持不变。装备remove和snapshot省略删除同样检查权限，缺失配置默认拒绝装备。该运行接入方式细化了3.8.0的权限提案：不将equipment塞进旧Health权限解析器。

在线entity_components_v1 schema新增equipment可选键，保留原组件契约；Inspector diagnostic schema增加可选装备视图。entity_state v1/v2、Presentation schema和网络消息集合未改。当前需要三端协调升级，不支持新装备发送器与旧接收器混用。

Java网络worker保存装备，EntityInspector的synchronized深复制视图供客户端命令读取；不让游戏线程读取可变接收器HashMap。`/mc7dtd_entity_inspect` 的现有完整JSON格式自动显示equipment，无新GUI或代理生成路径。

## 修改文件列表

新增12个交付文件：

- `7dtd-mod/src/EquipmentSample.cs`
- `bridge-server/EquipmentComponents.cs`
- `minecraft-mod/src/main/java/io/mc7dtd/EquipmentReceiver.java`
- `minecraft-mod/src/test/java/io/mc7dtd/EquipmentHarness.java`
- `config/equipment_permissions.json`
- `config/equipment_permissions.schema.json`
- `tests/equipment-runner/EquipmentRunner.csproj`
- `tests/equipment-runner/Program.cs`
- `tests/equipment-runner/validate.py`
- `tests/run-phase3_8_1.ps1`
- `tests/launch-phase3_8_1.ps1`
- `docs/phase3_8_1_equipment_runtime.md`

修改9个文件：

- `7dtd-mod/src/BridgeMod.cs`：主线程采集接入。
- `7dtd-mod/src/NativePlayerPublisher.cs`：共享desired集合、装备槽位差量、remove和完整恢复。
- `bridge-server/Program.cs`：协调层应用、读取及既有清理挂接。
- `bridge-server/EntityInspector.cs`：可选装备深复制观察。
- `minecraft-mod/src/main/java/io/mc7dtd/BridgeClient.java`：接收协调层与重置/退出挂接。
- `minecraft-mod/src/main/java/io/mc7dtd/EntityInspector.java`：装备观察投影。
- `docs/entity_components_v1.schema.json`：可选Equipment契约。
- `docs/entity_debug_inspector.schema.json`：可选Equipment诊断字段。
- `tests/debug-runner/Program.cs`：允许MC7DTD_TEST_BRIDGE指向新阶段DLL；保留原默认路径。

构建、自动测试和schema更新辅助脚本均位于工程work/bin/build目录。本列表为本阶段文件，git diff相对HEAD还包括大量历史未提交变更，不能把累计行数归于本阶段。没有commit/push、清理未提交文件或操作真实游戏进程。

## 编译与自动测试

完整入口：

```powershell
Set-Location 'D:\wenjian\minecraft\7-M'
.\tests\run-phase3_8_1.ps1
```

| 构建 | 结果 | 产物 |
| --- | --- | --- |
| 7DTD net48 | PASS，0错误/0警告，真实公开API调用编译通过 | 7dtd-mod/bin/Phase381/net48/MC7DTD.Bridge.dll |
| Bridge net10.0 | PASS，0错误/0警告 | bridge-server/bin/Phase381/net10.0/BridgeServer.dll |
| Minecraft Fabric | BUILD SUCCESSFUL | minecraft-mod/build/libs/mc7dtd-bridge-0.1.0.jar |

| 验证组 | 结果 |
| --- | --- |
| 新增C#装备/发布器/真实跨进程WebSocket/Inspector | 45项通过 |
| 新增Java装备接收 | 14项通过 |
| schema、实际序列化消息、权限配置和保护哈希检查 | 30项通过 |
| 旧Java Debug/Presentation/Identity/Health/NativeProxy | 14+10+23+20+29项通过 |
| 旧C# Presentation/Identity-Health/Debug | 42+51+42项通过 |
| 旧组件契约 | 41组通过 |

证据位于work/phase3_8_1-test。C#装备测试包括45个检查，并在随机隔离端口运行真实Bridge、真实7DTD BridgeClient/NativePlayerPublisher与Java BridgeClient/EquipmentReceiver。源采集输入由测试夹具提供，不启动真实游戏。运行验证中Health=95、Identity=EquipmentTest、Presentation=survivor保持，换手持/空手/卸甲抵达Minecraft Inspector，正常Leave后两端观察列表为空。新Java测试14项包括非法混包原子拒绝和清理。

17份核心源码/配置SHA-256保持一致，涵盖Health、Identity、Authority transport、Registry、CoordinateMapper、Presentation和原组件权限。原阶段测试中的“源码冻结”断言只适用于原阶段，不用它判定本阶段允许的组件入口变更；本轮使用17份明确受保护文件及受影响模块回归。最后的小幅Bridge输入类型保护调整后，重新编译装备runner并重跑45项装备及三个C#回归runner、schema/41组契约；Java和7DTD生产源码自成功构建后无新增更改。

## 用户人工验收步骤（待执行）

1. 正常退出两个游戏及旧Bridge。确认18771可用，保持Steam登录。在工程根目录分别运行：

```powershell
.\tests\launch-phase3_8_1.ps1 bridge
.\tests\launch-phase3_8_1.ps1 minecraft
.\tests\launch-phase3_8_1.ps1 7dtd
```

启动器部署到工程runtime，Bridge必须使用Phase381目录；Minecraft JAR文件版本仍为0.1.0。查看Bridge日志确认没有端口绑定失败。旧游戏必须先退出，避免锁定DLL或加载旧JAR。启动器重复运行会覆盖本阶段同名日志，复验前保留所需证据。

2. 进入两个既有世界，在七日杀穿戴可获得的护甲并持有一个物品。Minecraft执行 `/mc7dtd_entity_inspect`，核对equipment.slots完整五槽及真实7dtd物品定义名，同时确认Health/Identity/Presentation存在。全部空槽仍应完整列出且值为null，不是缺失槽位。
3. 七日杀正常切换到另一个手持物；等待约1秒再查询。held_item应变化，未变护甲槽和其它组件保持。
4. 切到空快捷栏槽/空手；held_item应为null，不应出现meleeHandPlayer拳头内部ID。
5. 正常卸下任一护甲；对应槽为null，其它槽保留。需要body对应Chest、hands对应Hands、feet对应Feet；不要求代理外观改变。
6. 可使用Bridge HTTP对照完整源身份及组件：

```powershell
Invoke-RestMethod 'http://localhost:18771/debug/entities' | ConvertTo-Json -Depth 20
```

7. 正常退出七日杀世界，核对equipment remove及随后despawn、Minecraft接收实体移除、Bridge不再列出7DTD玩家。不杀进程替代生命周期验收。Minecraft玩家若仍在世界，Bridge列表可以仍有其v1记录，不要求全局列表必为空。
8. 再次进入七日杀世界应使用新实体生命周期并收到新snapshot，Equipment五槽恢复。当前debug.json为false/false，保留该设置检查新增Debug关闭后装备数据仍能被显式Inspector查询；没有新装备标签。

日志：docs/phase3_8_1-runtime-evidence/bridge-stdout.log、7dtd-game.log及runtime/minecraft/logs/latest.log。七日杀原`7DTD components sent`日志含实际equipment包；Minecraft有`7DTD equipment received`，整组件remove标记removed=true。新增装备观察日志属于组件日志，不受debug_logging控制；`[Entity Debug]`仍按已有开关控制。内部投影导致的Health/Identity重复观察日志不表示新伤害或名字修改。

整个Equipment remove在正常退出及采样不可用时发送，五槽全空本身不删除组件。采样不可用/恢复路径已有自动测试，不提供修改游戏内存或故意破坏采集器的人工步骤。

## 停止点与限制

Phase3.8.1实现与自动验证完成，人工验收待用户。尚无本阶段真实五槽换装采集验收结论。继续存在原系统无ACK/可靠重放、单本地玩家和版本绑定限制；不承诺换手过渡帧无瞬态变化。没有进入Phase3.8.2。

# Phase 3.8.2.1 — Equipment Visual State v1

日期：2026-10-04，Asia/Shanghai。实现与自动化验证完成。

## 结果与数据路径

Minecraft 本地派生层已实现。读取现有接收器合并、接受后的 Equipment 状态，结合只读有效 Presentation，生成不可变 EquipmentVisualState。它不属于网络 Component，不包含 revision，不修改 source item_id、Presentation 或源组件存储。

```text
原 entity_state / entity_components 接收与存储（保持原实现）
    → EntityInspector 的 synchronized 只读快照
    → EquipmentModifierGenerator
    → equipment_visual_state（本地 Inspector 顶层字段）
```

`/mc7dtd_entity_inspect` 显示派生状态；Inspector 的 latest/list/find 均可读取。字段位于顶层，未放入 `components`，不会进入任何发送包或 Bridge Inspector HTTP 输出。

## 实现行为

- EquipmentVisualState 是不可变值，提供 known/unknown、固定 head/body/hands/feet/held_item 五槽；toJson 每次创建独立副本，包含显式 null。
- Generator 的 `generate(equipment)` 支持仅 Equipment 输入，使用通用 Presentation fallback；`generate(equipment,presentation)` 支持只读基础表现的兼容性判断。不推断默认 humanoid/survivor。
- 本地映射解析为 resolved / unmapped / incompatible；只有 resolved 含符号 modifier 描述。没有物品映射时保留 ID，modifier=null。
- JAR 内本地目录包含已在 Phase 3.8.1 观察到的三个来源 ID：粗糙装束、木棒、火把。binding_id 是 `mc7dtd:primitive_outfit`、`mc7dtd:wooden_club`、`mc7dtd:torch`；仅为数据标识，没有对应资源或模型加载代码。
- 目录严格校验槽位、kind、anchor、大小写、字段、重复 JSON 键和重复匹配组合；上限4096规则、2MiB、JSON深度8。非法/缺失目录整体降级为空目录，不影响同步或原组件。
- 空槽为 null；替换和卸下通过原接收器更新后的全量 Equipment 自动体现。整体 remove 或缺失为 unknown/五槽null；全空但组件存在为 known/五槽null。
- Presentation 不兼容时撤销描述，但保留装备 ID 和 known 状态；恢复兼容可重新 resolved。Presentation remove 使用现有通用 fallback 推导，不回写 Presentation 的诊断字段。
- 本次选择**即时派生、不另建 VisualState 缓存**：每次读取在 Inspector 锁内从当前记录计算，生成完整输出后返回；没有跨实体历史、独立序号或重复生成副作用。原记录 despawn/reset 后不再有派生状态。
- 客户端初始化及每个客户端 tick/停止事件只更新本地目标世界是否可用；无目标世界时 `equipment_visual_state:null`，原组件仍可保留。世界重新可用时即时重建。该本地可用性不等于源 Equipment unknown，两者区分。
- 仅对 `source=7dtd,type=7dtd:player` 生成视图；原 Inspector 的完整作用域包含 world/dimension/entity/stream。Debug 开关不影响派生内容或清理。

此即时读取方式细化了 Phase 3.8.2.0 的缓存提案：不引入额外 Map 或网络事件接线。生产 Inspector 命令和 Debug 读取由客户端线程调用；纯数据生成器不依赖 Minecraft API，headless 测试可在其它线程读取 synchronized 视图。

## 文件列表

新增6个文件：

1. `minecraft-mod/src/main/java/io/mc7dtd/EquipmentVisualState.java`
2. `minecraft-mod/src/main/java/io/mc7dtd/EquipmentModifierGenerator.java`
3. `minecraft-mod/src/main/resources/equipment_visual_mappings.json`
4. `minecraft-mod/src/test/java/io/mc7dtd/EquipmentVisualHarness.java`
5. `tests/run-phase3_8_2_1.ps1`
6. `docs/phase3_8_2_1_equipment_visual_state.md`

修改2个文件：

1. `minecraft-mod/src/main/java/io/mc7dtd/EntityInspector.java`：只读输出的即时派生与本地世界可用性。
2. `minecraft-mod/src/main/java/io/mc7dtd/MinecraftBridgeMod.java`：初始化、现有客户端 tick 和停止事件更新本地世界可用性。

Bridge、7DTD、EquipmentReceiver、HealthReceiver、PresentationReceiver、网络 BridgeClient、共享 DTO、采集器及在线 schema 均未改动；Presentation Component 不可变更的限制通过验收前后哈希与测试核对。Git HEAD 仍包含累计历史差异，本列表仅指本阶段。

## 编译与测试

入口：工程根目录执行 `tests/run-phase3_8_2_1.ps1`。脚本只构建 Minecraft 和执行 headless Java 测试，不启动游戏或 Bridge。

Minecraft Fabric：**BUILD SUCCESSFUL**，最终编译8秒。产物 `minecraft-mod/build/libs/mc7dtd-bridge-0.1.0.jar`；已核对 JAR 包含两个新类及本地映射资源。Bridge/7DTD 未改动，本轮未重编译。

新增测试24项：设计 M01–M20 全覆盖，另含用户要求的3项及1项 Inspector 查询覆盖。

| 编号 | 测试 | 结果 |
|---|---|---|
| M01 | 五槽全空、known 与完整 null | PASS |
| M02 | 缺失/整体 remove → unknown | PASS |
| M03 | 护甲符号描述生成 | PASS |
| M04 | 换手持保持护甲槽 | PASS |
| M05 | 空手清空、无拳头物品 | PASS |
| M06 | 卸甲仅对应槽清空 | PASS |
| M07 | 精确 ID/大小写/槽位匹配与未知降级 | PASS |
| M08 | 基础 renderer/model 不兼容及恢复 | PASS |
| M09 | scale/variant 更新不改 Modifier | PASS |
| M10 | Presentation remove/fallback/add | PASS |
| M11 | snapshot 省略与全量重新添加 | PASS |
| M12 | 非法/旧包/错误基线/stream 拒绝 | PASS |
| M13 | 提前 sidecar 拒绝及合法输入顺序收敛 | PASS |
| M14 | Health/Identity 和实际位置更新不改派生装备 | PASS |
| M15 | despawn/reset/同连接新实体清理 | PASS |
| M16 | world/dimension/stream/source 隔离 | PASS |
| M17 | 无目标世界及恢复，保持源组件 | PASS |
| M18 | 深复制、不可变输出及并发读取无半更新状态 | PASS |
| M19 | 非法/重复/超限目录整体降级，原组件继续工作 | PASS |
| M20 | Debug 开关不改变生成和清理 | PASS |
| 新增 | 不修改 Presentation/Equipment 输入，无 revision 字段 | PASS |
| 新增 | 重复生成值、hashCode 和序列化顺序一致 | PASS |
| 新增 | 未知物品保留 ID、无伪造 binding | PASS |
| 新增 | Inspector latest/list/find 显示本地状态 | PASS |

场景测试使用真实 HealthReceiver、EquipmentReceiver、PresentationReceiver 与 EntityInspector，组件数据来自现有消息夹具；并发用例校验锁内完整快照。Debug 场景使用原标签层的测试替身，不创建真实场景或装备模型。

既有回归 **115项通过**：EntityDebugHarness19、EquipmentHarness14、HealthHarness20、IdentityHarness23、PresentationHarness10、NativeProxyHarness29；总计 **139项通过**。

编译和各测试日志位于 `work/phase3821-test/`。用户输出目录包含冻结日志、逐组结果、Inspector 示例、产物哈希及范围核对；本阶段不把 headless 结果当作实机游戏验收。

## 风险与限制

1. **没有模型显示能力**：resolved 只表示映射描述匹配。当前 BlockDisplay 代理不加载装备模型，不假设人体挂点；三项 binding_id 不代表真实资源存在。
2. **映射覆盖有限**：只有粗糙装束、木棒、火把的三个符号规则。其他来源物品按 unmapped 保留，基础不兼容按 incompatible 降级，不能因此误判同步丢失。
3. **两路输入没有跨消息事务**：Equipment 与 Presentation 保持原各自接受规则，查询读取当前组合，可观察中间状态；无 ACK/自动重放问题未在本阶段解决。
4. **本地诊断契约扩展**：Minecraft Inspector 顶层新增字段；原 Bridge HTTP 输出/严格 schema 未修改。针对旧 Inspector 格式写死顶层字段的消费者，需识别这是本地扩展视图，不能用旧严格 schema 无条件校验新输出。
5. **即时派生开销**：每次 Inspector/Debug 查询计算五槽；映射和记录有界，不建额外缓存，但大量高频全实体查询仍有成本。没有性能基准或压力测试结论。
6. **尚未实机复验/部署**：构建产物已生成，运行目录中的旧 JAR 未替换，也未重启当前游戏。本阶段仅完成数据层与自动回归；没有视觉延迟或真实游戏新增 Inspector 验收结论。

停留在 Phase 3.8.2.1，等待下一步指令，不进入 Phase 3.8.2.2。

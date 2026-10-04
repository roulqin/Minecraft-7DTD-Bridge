# Equipment Component v1 — Phase 3.8.0 设计

日期：2026-10-04，Asia/Shanghai。工程根目录：`D:\wenjian\minecraft\7-M`。

## 状态与范围

用户已确认 Phase 3.7.4 通过，并授权开始 Phase 3.8.0 设计。此前 Debug Config Disable 辅助验证证明关闭新增 Debug 输出后实体与组件同步正常；该辅助验证未检查标签画面。本文件不补写不存在的画面证据。

本阶段交付契约、独立草案 schema、完整 JSON 示例和离线语义验证。第一版选定 **7dtd:player → Minecraft 的装备观察**，保存来源物品标识，用于未来日志和只读 Inspector。暂不采集或发送装备，不修改任何运行源码、在线 schema、权限配置、类型配置、Authority、Registry、坐标映射、握手或端口；不部署、不重启当前游戏，不进入 Phase 3.8.1。

Equipment 不创建新实体，不操作目标游戏物品，也不描述装备使用动作。背包、快捷栏全量、正式模型、动画、物品转换、伤害、属性加成、质量、耐久及改装件均留在后续明确授权的设计中。

## 1. 组件结构与槽位

```json
{
  "equipment": {
    "slots": {
      "head": { "item_id": "7dtd:example_head_armor" },
      "body": null,
      "hands": null,
      "feet": null,
      "held_item": { "item_id": "7dtd:example_tool" }
    }
  }
}
```

| 槽位 | 观察含义 | 采集绑定要求 |
| --- | --- | --- |
| head | 头部装备 | 后续通过本地公开 API 核对 |
| body | 身体装备 | 同上，不推定为旧版本胸/腿独立槽位 |
| hands | 手部装备 | 同上 |
| feet | 脚部装备 | 同上 |
| held_item | 当前实际手持物 | 不等同于整个快捷栏；空手行为需核对公开 API |

这是桥接契约的五个逻辑槽位，不是已确认的七日杀 API 枚举或索引。不存在额外 legs/off_hand/饰品槽位。接入阶段若本地 API 无法可靠对应，先报告并调整设计，不猜测索引、API 名称或物品定义。

每个非空槽位为仅含 `item_id` 的完整对象：长度 6..128，格式 `7dtd:[A-Za-z0-9_.-]+`，禁止空白、路径、URL、控制字符及其它命名空间。来源物品内部标识的大小写保留，不能擅自全部转小写造成碰撞；显示名不作为身份。若真实标识不能无损放进此范围，应修订契约，不进行有损替换。示例 `example_*` 只是离线占位值，不声称为游戏有效物品。

不含 count：本版只回答某槽位持有什么，数量属于后续定义。所有对象拒绝未知键和重复键；槽位值不接受字符串、数组、布尔值、空物品对象或任意元数据。

## 2. 未知、卸下和删除

| 输入 | snapshot | patch |
| --- | --- | --- |
| 未出现 equipment | 按原共享快照规则删除旧 Equipment | 保留 Equipment |
| equipment 为 null | 拒绝 | 删除整个 Equipment，保留共享 revision 基线 |
| equipment.slots 中槽位为 null | 该槽位已知为空 | 该槽位卸下，其它槽位保留 |
| 未出现某槽位 | 拒绝：完整装备快照必须包含全部五槽 | 已有 Equipment 时保留该槽 |
| slots={} / equipment={} | 拒绝 | 拒绝，不消耗 revision |

全部五槽为 null 表示“已知没有装备”，仍存在 Equipment；整个组件缺失表示“未知或未提供”，不能把它显示成全空装备。

首次在已有 sidecar 基线上添加 Equipment，或整体 remove 后重新添加时，patch 必须给出全部五槽。这避免把未采集的槽位默认成空。已有 Equipment 的 patch 可以包含 1..5 个槽位；槽位对象整体替换，不能递归更新 item_id 的子字段。

采集失败不得生成槽位 null 来伪造卸下。初次无法可靠读取全部五槽时先不发布 Equipment；已存在的观察状态若变得不可用，可发送 `equipment:null` 将其标为未知。无 ACK 情况下此 remove 也不保证送达；若网络队列失败或需要重新同步，应遵循第 4 节。暂不加入每槽独立 unknown、时间戳或过期机制。

## 3. sidecar 信封、共享版本与覆盖

采用既有 `entity_components version=1 / entity_state_version=2` 的**可选 equipment 扩展草案**。不新增 message type、equipment revision、lifecycle event 或 entity_state 字段。

沿用全部身份字段：source、authority、origin、entity_id、entity_type、world_id、dimension、stream_id，以及 entity_sequence、revision、base_revision、mode、components。Equipment 只附着到活动 v2 原生玩家；Minecraft 当前 v1 路径不新增组件发布能力。

Health、name、custom_metadata、equipment 共用同一 revision/base_revision。snapshot 的 base_revision=0；patch 必须有基线、base_revision 等于已接受 revision，且新 revision 更大。entity_sequence 精确等于 Registry 当前实体序号。不得为了补发组件伪造位置或修改实体序号。

共享 snapshot 完整覆盖四组已支持 sidecar 组件；省略任何旧组件都会按既有规则删除。因此装备新增或换装通常采用 patch；恢复快照由统一发布协调器输出当时全部已知 Health、Identity 和 Equipment，不能仅发送装备快照并期待其它组件保留。空 components={} 仍是合法的全量清空快照。

Health/name 在 patch 中仍是完整对象替换；custom_metadata 仍是整张 map 替换。只有 equipment.slots 引入上述槽位合并规则。Presentation 继续位于 entity_state 中、独立存储，不参与 sidecar snapshot 或 revision。

## 4. 接受顺序、原子性和发送调度

未来接入按以下顺序执行，保留现有 registrationGate：

1. 校验原始 JSON 的类型、重复键、有限数、深度、8192 字节预算和完整信封。
2. 检查握手角色=source=authority=origin.game=7dtd；完整 origin、类型、活动实体、stream 和当前 entity_sequence 精确匹配。
3. 组件白名单与对应源类型的 publish/store 权限检查；snapshot 还检查旧状态中将被省略删除的组件权限。
4. 校验全部内容及 Equipment 初始化完整性；未知槽位/物品字段或错误 Health/Identity 使整包失败。
5. 合法的旧 revision 忽略；身份、权限和内容非法的旧包仍拒绝。snapshot 的省略删除权限先于旧 revision 判断。
6. 新 patch 检查共享基线，生成候选副本，然后一次提交全部组件和 revision；任一失败时全部状态保持。接受后才转发和更新只读观察层。

源端未来采用一个采样/发布协调器：游戏主线程读取 Health、Identity 和装备，得到同一份 desired 状态；网络串行队列先排 entity_state 再排引用其序号的 sidecar。现有 NativePlayerPublisher 会将 desired 集合中缺少的旧键发为 remove，不能另加独立装备发布器后继续让旧发布器省略 equipment，否则装备会被反复删除。

建议沿用约 500ms 采样窗口：只在槽位值改变时发 patch，不随每个位置 update 重发全套装备。采样只读公开 API，不使用 Hook、内存访问或 Harmony。主线程不执行同步网络请求。

revision 表示协调器分配的发送版本，不能宣称接收成功。当前无 ACK/可靠重放/自动能力协商：队列未接受包时不把其 desired 状态当成成功发送基线；应在后续可发送时提供新的完整 snapshot。实体/连接重置后，沿用 fresh spawn + snapshot。其它不可靠送达问题仍是原系统限制，不在本阶段新增重传协议。

## 5. 权限、兼容性与边界

权限提案只对已启用映射的 `7dtd:player / source=7dtd / target=minecraft` 增加 equipment 的 publish/store=true，存于示例 `permissions.draft.json`。该文件不部署；当前生产权限配置及解析器均仍拒绝 equipment。remove 同样需要授权，metadata 无法授予任何权限。代理不能回传装备，owner 不变。

旧接收器会拒绝 equipment，即使与 Health 同包也会导致整包拒绝。本版接入策略选择未来三端**协调升级**，不承诺新发送器与旧接收器混用。升级后的接收器必须继续接受原有合法在线消息，缺少 equipment 时不制造默认装备。未升级的当前服务不得使用本设计示例。

不做逐接收器过滤：在没有能力协商时，过滤组件仍会推进共享 revision，容易掩盖状态丢失。若后来要求混用，需先单独设计能力发现和版本策略，不能默改现有握手。

目前 schema 中的 animation_state 只是历史设计保留、未接入运行时。本草案可用组件集合为 health/name/custom_metadata/equipment；没有顺带启用 animation_state 或 sidecar presentation。原 schema 原样保留。

数据范围仍为原包 8192 UTF-8 字节、深度≤8、正安全整数上限9007199254740991。五槽固定上限，物品 ID 有界；未来状态容量复用活动 Registry 边界，不能缓存未知实体、等待乱序包或无限历史。schema 无法独自证明原始重复键、字节数、权限、实体活动、跨字段等值、基线或原子性，离线语义模型补充这些检查。

## 6. 清理与调试

despawn、世界退出、断线、既有受控重连及 Bridge 重启清理 Equipment。正常退出可在 despawn 前按原顺序排组件 remove；移除 Equipment 不删除实体、不清除 Health/Identity/Presentation，也不恢复/复活退休 ID。

未来只读 Inspector 建议增加可选 `components.equipment`：已知时输出完整 slots，未知时输出 `{}`，区别于五槽全 null。保持现有 identity/health/presentation/authority 字段和只读深复制语义；旧 diagnostic schema 目前仍拒绝新键，必须在运行接入阶段单独扩展并测试，不宣称旧严格 schema 能验证新输出。

遵循已通过的 Debug Config 行为：debug_logging=false 关闭新增自动 Equipment Debug 摘要，debug_name_tag=false 不创建标签；显式只读查询仍可反馈，原有连接和组件日志继续存在。Equipment 存储、权限、revision 和清理均不得依赖 Debug 开关。第一版不新增世界内装备标签或 GUI。

## 7. 交付文件与离线示例

- `docs/equipment_component_v1.schema.json`：组件值草案；通过 `$defs/snapshot` 和 `$defs/patch` 定义模式。
- `docs/entity_components_equipment_v1.draft.schema.json`：完整 sidecar 扩展草案，引用装备 schema 及既有 v2 标识定义。
- `docs/examples/equipment_components/`：initial → slot_update → slot_clear → health_update → remove → add → resnapshot → snapshot_without_equipment 共八条完整消息，另有一份权限提案。
- `tests/equipment-components/equipment_contract.py`：仅离线的语义 Oracle，复用旧验证函数检查身份及 Health/Identity，独立模拟 Equipment 合并和共享 revision；不替代生产 Registry 或 Authority。
- `tests/equipment-components/test_equipment.py`、`tests/run-phase3_8_0.ps1`：离线验证及保护文件校验。

所有消息示例固定 entity_sequence=1，仅代表生命周期尚未推进的可信离线夹具。真实接入必须从当前活动实体生成身份和序号，不能直接复制发到游戏。权限提案单独验证，不能当作消息发送。

## 8. 验证结果与下一阶段入口

在工程根目录执行：

```powershell
.\tests\run-phase3_8_0.ps1
```

本轮 **30 组离线测试通过，67 份受保护文件 SHA-256 保持一致**。测试覆盖完整示例序列、槽位保留/清空、整体删除与重新添加、共享 snapshot 的省略删除、Health/Identity 替换规则、原子拒绝、旧 revision、权限、身份/stream/序号、清理、作用域隔离、深复制、原始 JSON 边界及旧在线组件示例的草案兼容性。输出在 `work/phase3_8_0-test/`，使用已有 jsonschema/referencing 依赖，所有 schema 资源离线注册，不访问网络或运行服务。

这是设计验证，不是 C#/Java Equipment 接入、采集 API 验证、运行编译或装备实机验收。67 份保护文件涵盖运行源码、共享 DTO、配置、相关项目文件及既有在线 schema；不代表运行游戏产生的日志未变化。

进入实现前的待核实项：公开 API 能否可靠对应五槽及空手；真实内部 ID 字符范围；采样失败的可识别性；三个目标产物协调部署；组件协调器扩展能否保留当前 Health/Identity 的 State API。若这些不成立，应先修订本契约。建议下一步先批准最小只读 API 核查，再进行组件扩展接入；本次完成后停止等待指令。

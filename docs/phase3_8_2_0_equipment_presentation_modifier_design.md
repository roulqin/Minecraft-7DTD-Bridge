# Phase 3.8.2.0 — Equipment Presentation Modifier 设计

日期：2026-10-04，Asia/Shanghai。状态：设计提案，未实现。

## 设计结论与边界

Equipment Presentation Modifier 是 **Minecraft 接收端的本地派生表现描述**，不是新的网络 Component。输入为已有、已接受的 Equipment 全量存储状态与有效 Presentation；输出为五槽装备的表现意图。它不回写输入，不发布消息，不创建游戏物品，不修改共享 revision 或采集器。

本阶段仅交付数据结构、合成关系、更新规则和测试方案。没有模型、资源加载、骨骼绑定、贴图、动画、GUI、代理外观变更或 Runtime 接入。Health、Identity、Authority、碰撞、Combat、Inventory、双向控制均保持原边界。

已核对现有实现：Equipment 在 `entity_components v1` 中使用共享 revision；Presentation 在 `entity_state` 中保存 renderer/model/variant/scale，更新由 lifecycle sequence 接受规则控制。二者不共享一个版本号。Minecraft 当前代理为 body/nose 两个 BlockDisplay，尚无人体骨骼或装备挂点；本设计不声称其已经能显示装备。

## 1. 数据结构

### 1.1 输入保持原契约

```json
{
  "equipment": {
    "slots": {
      "head": null,
      "body": { "item_id": "7dtd:armorPrimitiveOutfit" },
      "hands": null,
      "feet": null,
      "held_item": { "item_id": "7dtd:meleeToolTorch" }
    }
  },
  "presentation": {
    "renderer": "humanoid",
    "model": "survivor",
    "variant": "default",
    "scale": 1.0
  }
}
```

这只是把两个已存储输入放在一起的说明，不是新增信封，也不是把 Presentation 移入 sidecar。未知/移除的 Equipment 与完整五槽全 null 仍是两种状态。不得直接把 slot patch 当成完整 Equipment 输入；解析器只能读取已合并状态。

### 1.2 本地输出：EquipmentPresentationModifierView v1

以 `(source, world_id, dimension, entity_id, stream_id)` 隔离，本地会话重置和目标世界切换另行清空缓存。以下 JSON 是本地内存/诊断视图提案，不发往 Bridge，不加入任何现有 Component schema。

```json
{
  "equipment_state": "known",
  "slots": {
    "head": null,
    "body": {
      "item_id": "7dtd:armorPrimitiveOutfit",
      "resolution": "resolved",
      "modifier": {
        "kind": "armor_overlay",
        "binding_id": "example:primitive_outfit",
        "anchor": "body"
      }
    },
    "hands": null,
    "feet": null,
    "held_item": {
      "item_id": "7dtd:meleeToolTorch",
      "resolution": "unmapped",
      "modifier": null
    }
  }
}
```

| 字段 | 约束与语义 |
|---|---|
| equipment_state | `known` 或 `unknown`。五槽全 null 且 known 是“已知未装备”；unknown 表示源组件不存在/已移除。 |
| slots | 永远按 head、body、hands、feet、held_item 顺序输出全部五键；不增加 legs/off_hand。 |
| 槽值 null | known 下表示已知空槽；unknown 下表示该槽不可推导。必须结合 equipment_state 解读。 |
| item_id | 非空输入原样保留 `7dtd:` 标识和大小写；不替换为显示名、Minecraft Item ID 或文件路径。 |
| resolution | `resolved`：匹配本地描述规则；`unmapped`：无物品规则；`incompatible`：有规则但基础 renderer/model 不适用。 |
| modifier | resolved 时为完整描述；其余为 null。无递归 patch 或网络 remove 语义。 |
| kind | 护甲四槽只允许 `armor_overlay`；held_item 只允许 `held_attachment`。名称仅表达意图，不代表具体渲染技术。 |
| binding_id | 本地表现目录的符号标识，1–64 字符，字符集 `[a-z0-9_.:-]`；不能为 URL/文件路径，不触发加载。 |
| anchor | 与所属固定槽一致的逻辑挂点，未绑定骨骼或坐标。held_item 不预设左/右手，也不处理双手武器。 |

整个 Equipment 不可用时输出 `equipment_state=unknown`，五槽全部为 null；同时撤销旧派生描述，不保留可能过时的装备外观。数据本身全空时输出 known，仍保留完整五槽。

本地 View 不包含独立 revision、base_revision、entity_sequence、时间戳、耐久、数量、属性加成或攻击动作。身份和会话由缓存键携带；`v1` 只是设计结构版本，不是升级网络消息版本。

### 1.3 本地映射目录草案

未来可独立存放为 Minecraft 本地配置；本阶段不创建或部署运行配置。示例中的 binding_id 是占位符，未对应真实模型或资源。

```json
{
  "format_version": 1,
  "source_type": "7dtd:player",
  "target_game": "minecraft",
  "rules": [
    {
      "slot": "body",
      "item_id": "7dtd:armorPrimitiveOutfit",
      "compatible_base": { "renderer": "humanoid", "model": "survivor" },
      "modifier": {
        "kind": "armor_overlay",
        "binding_id": "example:primitive_outfit",
        "anchor": "body"
      }
    }
  ]
}
```

规则使用槽位 + 精确 item_id + 精确 renderer/model 匹配；不使用模糊匹配、大小写归一化、正则推断装备类别或按规则顺序选择优先级。variant 和 scale 在本版不参加规则选择，随基础 Presentation 保留。

解析顺序固定为：先按槽位/item_id 查找；没有规则则 unmapped；有规则但没有兼容 renderer/model 则 incompatible；找到唯一兼容规则才 resolved。移除后的 unknown/default fallback 不自动视为 humanoid/survivor。

同一匹配组合重复属于配置错误；每目录上限建议 4096 条规则，每槽最多一个生效描述。未来启动时一次校验未知字段、长度、槽位/kind/anchor 关系及重复键；非法配置使 Modifier 功能降级为无可用目录，基础代理和数据同步继续正常。首次配置缺失使用空目录，非空物品标记 unmapped。配置加载一次，不设计热更新。

## 2. 与 Presentation Component 的关系

合成关系：

```text
基础表现 = 已接受的有效 Presentation（沿用现有默认/移除 fallback）
装备描述 = resolve(已接受的 Equipment 全量状态, 基础表现, 本地映射目录)
目标端表现意图 = { base: 基础表现的副本, equipment_modifier: 装备描述 }
```

这是组合两个视图，不是把装备字段合并进 Presentation。Modifier 不写 renderer/model/variant/scale，不换基础模型，不占用 Presentation 的字段 patch，也不修改 Authority。基础表现和装备描述的存储所有权分开。

| 事项 | Presentation | Equipment Modifier |
|---|---|---|
| 数据来源 | entity_state 中的现有表现 Component | Minecraft 本地推导 |
| 输入更新依据 | 原 lifecycle/sequence | 已接受输入的值变化 |
| 版本 | 保留既有机制 | 不产生网络 revision |
| 删除 | 现有 Presentation remove/fallback | Equipment 移除或槽空时撤销描述 |
| 作用 | 基础 renderer/model/variant/scale 意图 | 五槽附加表现意图 |
| 输出接收者 | 既有接收存储与代理路径 | 未来本地只读诊断/表现适配器 |

Presentation remove 仍由现有接收器提供通用有效 fallback；Modifier 按 fallback 的 renderer/model 重新判断兼容性，不凭空重新套用类型默认值。Equipment 继续 known 的情况下，Presentation 移除不会把装备数据改成 unknown，只会使不适配的非空槽标为 incompatible。

即使 resolution=resolved，当前阶段也不表示装备已可见。未来真实资源目录校验、后端挂点能力、模型加载和显示失败策略需要在另一个明确授权的实现阶段定义。本设计不给 BlockDisplay 增加假骨骼，不把 binding_id 当作 Minecraft 物品。

## 3. 更新规则

### 3.1 解析和发布边界

未来本地消费者读取现有 EntityInspector 的 synchronized 深复制快照，或等价的已接受只读视图；不能跨线程直接读 EquipmentReceiver/PresentationReceiver 的可变 Map，也不能新增网络解析旁路。

纯解析器不访问游戏对象；若未来接入客户端，缓存与表现消费归 Minecraft 客户端主线程，按一次读取的快照计算完整候选 View，再整体替换。无需新增网络锁、消息、采样窗口或修改接收器。没有活动代理或目标世界时不创建表现对象。

同步来源不保证 Equipment 与 Presentation 的跨消息原子性。定义为“当前已接受的两份状态的确定性组合”，不能要求它们具有相同 revision/sequence，也不能推断它们在源端同一瞬间采样。输入先后到达时可短暂经历中间 View，下一次输入变化重新计算；本版不引入延迟合并、过渡帧过滤或 debounce。

### 3.2 触发与撤销

| 已接受的输入事件 | 本地行为 |
|---|---|
| spawn，Equipment 尚未到达 | 新作用域 View 为 unknown，不沿用旧实体描述。 |
| 完整 Equipment snapshot / 整体移除后完整 add | 基于完整五槽重新解析。 |
| 单槽或多槽 patch | 先由原接收器合并；解析器读取全量结果。可重算五槽，最终仅变化槽的描述不同。 |
| 槽位 null | 当次 View 中槽值为 null，撤销该槽旧描述；其余槽按相同输入保持。 |
| equipment:null / snapshot 省略 Equipment | 输出 unknown，全槽撤销；实体及基础表现仍存在。 |
| 五槽全 null | 输出 known，全槽 null，不删除 Equipment 或实体。 |
| Presentation model/renderer 改变 | 重算所有非空槽的兼容性；此前 resolved 可能变 incompatible，反向也可恢复。 |
| Presentation scale/variant 改变 | 组合输出更新 base，modifier 描述保持；不新增装备缩放计算或 variant 覆盖。 |
| Presentation remove/add | 按现有有效 fallback/恢复后的基础值重算，不改 Equipment。 |
| 只有 Health/Identity 或位置/旋转变化 | 输入 Equipment 与 Presentation 相同则 Modifier View 不变；不因共享 revision 增长重建描述。 |
| 重复/旧 revision、非法组件包 | 原接收器不提交，Modifier 无变化；不重新实现拒绝规则。 |
| despawn / 断线 / 重连重置 / Bridge 重启触发的既有重置 | 清空对应缓存或会话缓存；退休实体的旧 View 不可恢复。 |
| Minecraft 目标世界退出/切换 | 清空表现缓存；回到有效代理世界后仅从当时已接受的活动记录重建。 |

相同完整输入和目录生成相同 View；值相等可跳过重复通知。缓存只保留当前活动作用域，每实体五槽，不保存历史、不排队等待乱序包、不持久化。容量不能超过既有活动实体边界。Debug 开关只控制显示/日志，不参与解析结果或清理逻辑；不存在新的“每槽 revision”。

## 4. 测试方案（后续实现时执行）

本阶段没有解析器实现，因此以下是验收用例及预期，不标记自动测试 PASS。优先使用纯输入/输出测试和现有组件夹具，不启动游戏、不装模型。

| 编号 | 场景 | 验收断言 |
|---|---|---|
| M01 | 完整五槽均 null | known，输出五键均 null；组件不被视作缺失。 |
| M02 | Equipment 缺失/整体 remove | unknown，五键 null，无旧 descriptor。 |
| M03 | 一个非空且已映射护甲 | 仅对应槽 resolved；item_id 原样保留，kind/anchor 正确。 |
| M04 | 木棒换火把；两者均有测试映射 | 仅 held_item 描述替换，四护甲槽与 base 不变。 |
| M05 | 非空手持改为 null | 撤销 held_attachment；不生成拳头描述。 |
| M06 | 卸下一件护甲 | 对应槽 null，其余四槽保持。 |
| M07 | 未知物品、相同 ID 不同大小写、同 ID 不同槽 | 精确规则匹配；无规则为 unmapped，不猜资源或跨槽复用。 |
| M08 | renderer/model 不兼容及恢复兼容 | incompatible + modifier null；恢复后 resolved，不改原装备。 |
| M09 | Presentation scale/variant 更新 | base 变化，modifier 值保持，原四字段与输入不被回写。 |
| M10 | Presentation remove 后 add | 按现有 fallback/恢复值解析，Equipment known 状态保持。 |
| M11 | snapshot 省略 Equipment、remove 后全量重新添加 | 撤销旧描述，恢复时从完整五槽重建，无遗留槽。 |
| M12 | 非法包、旧包、错误共享基线/stream | 使用现有接收器驱动输入，拒绝后 View 不变，不推进源 revision。 |
| M13 | spawn 前到达 sidecar、两组件合法更新的不同先后顺序、重复输入 | 提前 sidecar 按原接收规则拒绝，不缓存/重放；只有后续合法 snapshot 到达后才 known。合法两输入均到齐时最终结果一致，重复无额外通知。 |
| M14 | Health/Identity-only patch、连续位置更新 | 原组件正常更新，modifier 不变；Presentation 不被装备覆盖。 |
| M15 | despawn、断线、重连及同连接重进 | 缓存清理，entity/stream/会话隔离，无旧 View 附着新实体。 |
| M16 | 相同 entity_id 跨 world/dimension/source | 独立作用域，无串数据；只有授权目标类型参与推导。 |
| M17 | 本地目标世界退出/切换、无代理 | 清空缓存、不创建对象、不显示旧世界装备。 |
| M18 | 深复制与原子替换 | 修改返回 View 不污染输入/缓存；读取不到半更新五槽。 |
| M19 | 非法/重复/超限映射目录 | 目录校验失败且无部分规则生效；基础表现、Health/Identity、同步继续。 |
| M20 | Debug false/false 与 true/true | 派生结果与清理一致；显式 Inspector 若接入需保留五槽 null。 |

纯解析测试之后，再用既有接收器 snapshot→patch→slot null→Equipment remove→despawn 夹具验证组合行为，并运行 Equipment、Health、Identity、Presentation、Debug、NativeProxy 回归。跨消息顺序用例只验证最终收敛与明确的中间状态，不承诺跨组件事务一致性。

最终收敛的前提是最新合法输入实际被原系统接受；本地 Modifier 不补发 snapshot，也不解决原系统无 ACK/自动重放导致的未送达问题。

未来若获准接入“只读派生视图”，人工验收可复用 Phase 3.8.1 的五项流程，核对 derived View 而非要求代理外观变化。资源存在、挂点实际位置、装备模型、动画和显示延迟的图形验收不属于本次方案的通过条件。

## 5. 设计风险与停止点

主要边界是现有 BlockDisplay 没有装备挂点、映射目录尚无真实资源、跨消息只有最终状态组合，以及 unknown/unmapped/incompatible 容易被显示层混为“空装备”。本提案用显式状态和完整五槽避免这种歧义，并保持基础代理降级可用。

本次只新增本文件，不创建运行配置/在线 schema，不修改 Runtime、协议或 Inspector 实现，不部署、不启动游戏。测试方案尚未执行；完成后停留在 Phase 3.8.2.0，等待用户下一步指令。

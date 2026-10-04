# Phase 3.8.0.5 — Equipment API Research

日期：2026-10-04（Asia/Shanghai）。工程：`D:\wenjian\minecraft\7-M`。

## 调查结论与证据范围

本机七日杀公开成员支持读取四个实际护甲槽、当前手持物、手持数量和物品耐久信息。现有 Equipment v1 五槽、仅 item_id 的观察契约可以保留，未发现需要立即修改 schema 的事实。Minecraft 需要扩展现有共享组件存储的校验/合并入口及只读 Inspector；现有 Block Display 代理没有生物装备渲染能力。

本次只读取本地程序集元数据和指定方法 IL、游戏 XML 及项目源码，没有调用游戏方法、操作游戏、修改源码/schema/config、编译或部署 Mod。调查工具和原始输出仅保存在 `work/phase3_8_0_5-research/`；新增本报告。Windows PowerShell 使用 ReflectionOnlyLoadFrom，游戏程序集不执行，不触发其静态初始化。

版本依据：本轮既有七日杀日志记载 `V 3.2.0 (b10)`；调查程序集为安装目录的 `7DaysToDie_Data/Managed/Assembly-CSharp.dll`，SHA-256：`CB33A4ADBD9BD25C98255B8DE675B3050EA39C782A515EC97CF943EAC4A480FE`。结论针对该实际二进制，不等同于所有七日杀版本的稳定 SDK 保证。

证据：`api-members.txt`（可见性、类型、成员签名）、`api-il.txt`（选定 getter/读取方法的 IL）、`xml-findings.json`（名称和槽位扫描）、`input-hashes.json`（程序集、items.xml、blocks.xml 的哈希）。旧 .NET Framework 反射器对 Inventory 的一个涉及现代接口的无关方法签名标记 METHOD_UNRESOLVED；本报告使用的读取方法签名及其关键 IL 均成功解析。没有将该失败成员当作已确认 API。

## 1. 7DTD 玩家装备读取方案

### API 入口与线程

沿用当前 BridgeMod.OnUpdate 的游戏主线程入口：`GameManager.Instance?.World?.GetPrimaryPlayer()` 获取本地玩家。该入口已用于当前位置、Health 和 Identity 采样。玩家继承 `EntityPlayerLocal → EntityPlayer → EntityAlive`；EntityAlive 中 `equipment: Equipment` 和 `inventory: Inventory` 均为 public 字段。

建议未来主线程采样后立即转换为只含字符串/数值的观察快照，由原异步队列发送。不要在网络线程保留或读取 EntityPlayerLocal、Equipment、Inventory、ItemValue 引用。本次只核实读取路径，没有添加采样器。

### 可读取槽位

Equipment 的公开方法包括：`GetSlotCount(): int`、`GetSlotItem(int): ItemValue`、`GetSlotItemOrNone(int): ItemValue` 和 `GetItems(): ItemValue[]`。

| 契约槽位 | 本地 EquipmentSlots 枚举 | 数值 | 读取路径 |
| --- | --- | ---: | --- |
| head | Head | 0 | player.equipment.GetSlotItem((int)EquipmentSlots.Head) |
| body | Chest | 1 | player.equipment.GetSlotItem((int)EquipmentSlots.Chest) |
| hands | Hands | 2 | player.equipment.GetSlotItem((int)EquipmentSlots.Hands) |
| feet | Feet | 3 | player.equipment.GetSlotItem((int)EquipmentSlots.Feet) |
| held_item | 独立 Inventory 当前手持状态 | 不硬编码 | player.inventory.holdingItemStack / holdingItemItemValue |

这些枚举值来自当前程序集，不应在实现中用裸数字替代枚举。`body` 是桥接名称，对应七日杀 `Chest`，不需要重命名 schema。

实际枚举还包括 BiomeBadge/BiomeBadge2/BiomeBadge3/BiomeBadge4=4..7、ClothingHead/ClothingChest/ClothingHands/ClothingFeet=8..11，Count 和 None 均为12。基础 items.xml 也含这些槽位的直接配置。它们超出首版四护甲槽范围，不应将它们误认为不存在，也不应遍历全部槽后塞进四槽契约。None=12 是哨兵，不能当作有效数组槽位。Equipment 另有 CosmeticSlots/GetCosmeticSlot、临时外观和解锁记录；这些不等同于实际装备 ItemValue，本版不采集。

IL 确认 `GetItems()` 返回内部 m_slots 数组引用，没有深复制；只读采样不修改它。`GetSlotItem()` 对 index≥长度返回 null，但没有负数检查。`GetSlotItemOrNone()` 会将越界或 null 返回 ItemValue.None，仍没有负数检查。未来需先确认 equipment 存在且 GetSlotCount() 足以涵盖0..3，然后使用固定有效枚举读取；不能把容器缺失/结构不完整误报为空槽。

### 空槽与空手判断

- 已确认有效护甲槽的 ItemValue 为 null 或 `IsEmpty()` 时，可观察为槽位 null。IL 中 ItemValue.IsEmpty() 判断 `type==0`。
- Inventory 的 public 属性为 `holdingItemStack: ItemStack`、`holdingItemItemValue: ItemValue`、`holdingCount: int`、`holdingItemIdx: int`，另有 `UsingBareHand(): bool` 和 `GetBareHandItemValue(): ItemValue`。
- 关键差异：选中空槽时 holdingItemItemValue 返回 bareHandItemValue；它可能不是空 ItemValue。holdingItemStack 则返回一个带 bareHandItemValue、但 count=0 的新 ItemStack。
- 因而建议以 `holdingItemStack` 为主：取得可用栈后用 ItemStack.IsEmpty() 区分空手；UsingBareHand() 可作辅助判断。其 IL 比较当前 holdingItem 与 bareHandItem。不要把 `meleeHandPlayer` 等拳头内部定义发布成玩家装备。
- ItemStack.IsEmpty() 判断 count<1 或 itemValue.type==0；若 stack.itemValue 异常为 null，方法本身没有容错。容器未就绪、异常或换手过渡不等同于正常空手，未来要显式区分。

Inventory 还有 public isSwitchingHeldItem 和换手状态字段。只读阶段已确认其存在；具体换手时序未实机测量。集成时建议避开过渡帧、采样稳定状态，不读取/修改 lastDrawnHoldingItemValue 或手持模型来替代逻辑装备。

### Item ID 格式

推荐使用公开 `ItemValue.ItemClass` 属性取得定义，再调用 `ItemClass.GetItemName(): string`，在线标识为 `7dtd:` 加原始定义名。例如基础 XML 中存在的 `armorPrimitiveOutfit` 对应 `7dtd:armorPrimitiveOutfit`。

IL 确认：

- ItemValue.ItemClass 从 ItemClass.list 按 ItemValue.type 查找并做基本边界检查；无法解析时可返回 null。
- ItemClass.GetItemName() 基类返回 XMLData.Item.ItemData.Name，即定义名，而非本地化显示文字。它是虚方法，因此具体物品类型可能有覆盖，接入时仍要校验实际返回值。
- 若使用备选 `ItemClass.GetForId(int)`，应传 ItemValue.type。`GetItemId()` 会减去 Block.ItemsStartHere；不能把其结果当作 GetForId 的同一种索引。GetItemOrBlockId 也有区间转换。
- 不发送数值 type/GetItemId 作为稳定跨游戏 ID；它们依赖定义表。保留字符串大小写，不发送 GetLocalizedItemName/显示名。
- ItemClassOrMissing 会在定义解析失败时返回 MissingItem 占位；本版不使用它伪造原物品身份。无法解析应标记采样不可用，而不是正常卸下或发布占位 ID。

对本机基础 items.xml 与 blocks.xml 的所有直接 item/block 定义名扫描：8,056 个名称，最长加前缀后为71字符，7,993个包含大写字符，全部符合当前 `7dtd:[A-Za-z0-9_.-]+` 且长度≤128。扫描涵盖可能被手持的方块定义；它不是游戏加载后的有效物品注册表，也没有解析 Mod XML patch 后的新增定义、名称覆盖或继承结果。实际 ItemClass.GetItemName() 返回值仍须运行验证。

### 数量、品质与耐久支持

| 数据 | 已确认 public API | 结论 |
| --- | --- | --- |
| 手持数量 | ItemStack.count:int；Inventory.holdingCount:int | 可读取选中栈数量；空手 fallback 栈数量为0 |
| 护甲数量 | Equipment 返回 ItemValue，而非 ItemStack | 没有同一读取路径上的原生 count；不要声称 API 提供了护甲数量 |
| 品质 | ItemValue.Quality:ushort、HasQuality:bool | 支持读取，但首版 schema 暂不同步 |
| 已消耗使用量 | ItemValue.UseTimes:float | 表示已用量，不应直接当剩余耐久 |
| 最大使用量 | ItemValue.MaxUseTimes:int、MaxUseTimesBase:int、MaxUseTimesUI:int | 有读取入口，但语义不同，不能互换 |
| 剩余比例 | ItemValue.PercentUsesLeft:float | IL 为 MaxUseTimes>0 时 1-clamp01(UseTimes/MaxUseTimes)，否则返回1 |

MaxUseTimes 调用 MaxUseTimesBase 后经过 ModMaxUseTimes；MaxUseTimesUI 直接返回 Base。Base 使用 EffectManager.GetValue 求值，并非固定常量。MaxUseTimes≤0 时 PercentUsesLeft=1，不能据此推断“该物品有100%耐久”；这可能代表没有可消耗耐久。若后续授权同步耐久，建议先明确是否支持耐久及原生分母，再发送非负有限的已用量/最大值或明确命名的剩余比例。此次仅确认 API 与 IL，不测量运行数值和修理/改装件的影响。

## 2. Minecraft 接收与存储方案

### 当前入口

现有 BridgeClient 网络 worker 收到 entity_components 后调用 HealthReceiver.receive(message)，随后以 HealthReceiver.state(message) 更新 EntityInspector.components。HealthReceiver 保存：

- entities：活动实体类型及 sequence；
- states：`State(revision, current, max, JsonObject components)`；
- 键：world_id/dimension/entity_id/stream_id；当前只接受 source=7dtd，因此该限定路径内没有跨来源冲突。

当前 components 白名单仅 health/name/custom_metadata，equipment 会被拒绝。现有合并器对所有非 null 值执行整组件替换，不支持槽位合并。直接加白名单会使单槽 patch 丢失其它槽，不能作为完整接入。

### 推荐未来存储位置

保留一个共享 sidecar 状态与 revision 入口，在现有 HealthReceiver.State.components 中保存合并后的完整 equipment.slots。Equipment 校验/合并可放入独立策略或辅助类，由同一入口生成候选状态后一次提交；不另建拥有独立 revision 的 EquipmentReceiver，也不在 Inspector 中实现合并规则。

保留 HealthReceiver 的 State API 含义及原 current/max/name/custom_metadata 处理，改动范围必须有相应回归。snapshot 使用完整组件集合，patch 对 equipment.slots 特殊合并，其它组件继续完整替换；整体 equipment null 删除组件而不删除共享基线。未来如扩展来源，需要将 source 纳入存储键，此次无需改变当前限定路径或 Authority。

HealthReceiver 当前 HashMap 由网络 worker 管理，并非可供游戏线程随意访问的线程安全库；state() 返回现有 State，其中 JsonObject 本身可变。主线程显示/命令应通过 EntityInspector 的 synchronized 深复制视图，或不可变快照/有界队列，不从主线程直接读取/修改 HealthReceiver.states。

### Proxy 显示准备

EntityInspector.copyComponents 当前只投影 Identity 和 Health，需在未来增加 equipment 的完整深复制视图，未知时 `{}`，与五槽均null区别。Bridge diagnostic schema、Java命令格式和观察测试也要相应增加可选键；Inspector 保持只读。

MinecraftProxyScene 当前用青色/金色 BlockDisplayEntity 表示身体与朝向，NativeProxyController.Snapshot 只有身份、生命周期、位置和旋转字段。Block Display 没有 LivingEntity 的装备槽，不能对它调用生物装备 API；当前也没有来源 item_id 到 Minecraft ItemStack/模型的映射。

因此 Phase 3.8.1 第一版可以完成“接收→共享状态→Inspector/日志”，无需改代理类型或外观。若以后要显示装备，应单独设计物品映射及 Item Display/模型适配器，并在 Minecraft 主线程消费已接受的快照。组件更新不一定伴随位置变化，不能只在 Proxy 的位置 update 回调中刷新装备。

## 3. 当前 schema 是否需要调整

结论：**当前设计草案不需要立即改结构；需要把以下已确认的映射与采样语义落实到后续接入方案。** 本次 schema 文件保持原样。

1. 保留五槽。body 对应本地 Chest；其余 BiomeBadge/Clothing/Cosmetic 槽显式不在v1范围。
2. 保留 item_id 大小写和当前长度/字符集。基础 XML 名称扫描通过；不得擅自 lowercase 或改为数值 ID。
3. 保留槽位 null 与组件 null 的区别。空手需根据栈语义识别；容器未初始化、定义解析失败、槽位越界不作为正常卸下。
4. 数量/品质/耐久虽可读，但用户批准的v1是仅物品标识观察，不因 API 存在就扩展字段。加入这些字段需要另行确认契约。
5. 后续运行集成仍需要按已批准设计扩展 online component schema、权限 schema/配置及 Inspector schema，并统一更新三端支持键；独立草案不会让当前运行时自动接受 equipment。

严格 schema 尚不能证明 native API 返回值有效、源装备完整性或跨槽原子采样；这些由采集器和原子组件入口负责。Mod 自定义名字若不符合字符集，先报告并修订契约，禁止有损替换。

## 4. 风险列表与 Phase 3.8.1 准备状态

| 风险 | 影响 | 接入前/接入中处理 |
| --- | --- | --- |
| 游戏版本/API变化 | 枚举、字段或 getter 语义变化 | 固定本轮程序集哈希；变化后重新核对，使用枚举而非裸索引 |
| 空手 fallback | 将拳头伪报为手持装备 | 优先 holdingItemStack.IsEmpty；辅助 UsingBareHand；实机覆盖空手/选空槽 |
| 换装/换手与载入中间态 | 把暂时 unavailable 当成卸下 | 主线程采样、验证容器和索引，覆盖换手/退出/重连，区分unknown |
| 共享 ItemValue/数组引用 | 网络线程读到变化或误写游戏 | 主线程立即复制数据；不保留/修改 GetItems 内部数组 |
| 数值 ID及 MissingItem | 身份随定义表改变或被占位覆盖 | 采用真实 GetItemName 字符串，保留大小写，解析失败不伪造值 |
| 额外槽与装饰系统 | 胸甲与外观/徽章混淆 | 固定四护甲槽，排除其它槽及 CosmeticSlots |
| Mod XML与类型覆盖 | 基础 XML 统计不覆盖实际所有物品 | 对实际 getter 返回值校验；不声称已验证加载后完整注册表 |
| 数量/耐久误解释 | 空手count0、已用量、无耐久物品被误读 | 首版不发送这些字段；后续单独定义和验证 |
| 共享revision及desired集合 | 独立发布器互相删除或基线冲突 | 统一 Health/Identity/Equipment 发布协调器，不改变 Authority |
| 槽位patch与snapshot | 丢其它槽或清掉Health/Identity | 独立槽位策略，原子提交，完整恢复快照及回归测试 |
| 旧客户端不识别equipment | 混包整包被拒绝 | 未来三端协调升级，不默改握手或过滤共享版本 |
| 接收线程和可变JsonObject | 跨线程竞争/状态被修改 | 单worker写共享状态，Inspector深复制供主线程只读 |
| 被动代理无装备渲染 | 存储成功被误称装备可见 | 第一版验收采用日志/Inspector，渲染另立范围 |
| 无ACK/可靠重放 | 发送日志不等于接收成功 | 用实际Bridge及Minecraft状态对照验收，保留已有限制 |

API Research 完成。可进入下一步运行集成设计/实现的依据是已核实的 public 读取路径；尚未完成目标框架最小调用编译、真实五槽值采集或换手时序验收。本次没有运行时实现，也未进入 Phase 3.8.1。

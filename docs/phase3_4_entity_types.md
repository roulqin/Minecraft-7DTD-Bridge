# Phase 3.4：实体类型系统设计

日期：2026-10-03。工程根目录：`D:\wenjian\minecraft\7-M`。

## 本阶段交付和边界

交付通用类型目录的数据契约、marker 的默认类型配置、严格 JSON Schema、离线语义校验器和自动测试。**配置尚未被 Bridge 或游戏 Mod 加载**；调整该文件不会改变当前实机行为。运行时仍使用 Phase 3.3 的 marker 实现。本阶段不增加新的对象生成入口，不实现玩家实体、NPC AI、战斗或方块同步。

`entity_state` 协议、`CoordinateMapper`、`EntityRegistry`、网络端口、两个 Mod 源码均不修改。配置版本与消息协议版本独立。

## 配置契约

文件：`config/entity_types.json`；结构规范：`config/entity_types.schema.json`。
规范使用 [JSON Schema Draft 2020-12](https://json-schema.org/draft/2020-12/json-schema-validation)。离线校验在结构检查后执行跨字段语义检查；Schema 本身不能表达所有映射唯一性和适配器支持关系。

| 字段 | 含义和约束 |
| --- | --- |
| version | 配置版本，当前为整数 1，未知版本拒绝 |
| types | 1–128 条类型定义 |
| entity_type | 源游戏命名空间类型，精确匹配，最长 128 字符 |
| enabled | 是否允许未来运行时启用该映射；布尔值 |
| target_mapping.game | 目标游戏，minecraft 或 7dtd；不得等于源命名空间 |
| target_mapping.entity_type | 本地目标类型别名，命名空间必须对应目标游戏 |
| target_mapping.adapter | 编译注册的接收端适配器标识，禁止文件路径、反射类名或动态 DLL 加载 |
| capabilities | 所有能力均显式声明 true/false；遗漏或未知能力拒绝 |

同一 `(entity_type, target_mapping.game)` 只能出现一次，即使其中一条禁用也不允许重复。一个源类型可以分别声明两个目标，但同游戏映射禁止，当前两游戏模型实际只有一个跨游戏目标。类型标识采用协议已有的命名空间字符规则，不引入通配符、继承或自动回退。

默认定义：`minecraft:marker → 7dtd:marker`，适配器 `marker`。
`7dtd:marker` 是框架内的本地可视化对象别名，**不是七日杀内置 EntityAlive 类型，也不是新增游戏 XML 实体名称**。源 `entity_type` 仍为 `minecraft:marker`，不会被目标别名替换。

## 能力模型

| 能力 | 默认 marker | 含义 |
| --- | --- | --- |
| spawn | true | 创建本地 marker 对象 |
| update_position | true | 更新位置 |
| despawn | true | 删除对象并释放资源 |
| update_rotation | false | 应用姿态；现有 marker 不应用协议 rotation |
| health | false | 本地生命值行为 |
| collision | false | 物理碰撞；现有 marker 移除 Collider |
| ai | false | AI 行为 |
| combat | false | 攻击、受伤等战斗行为 |
| persistence | false | 保存并恢复对象 |

能力描述接收端适配器支持什么，不扩展消息字段，也不表示对应玩法已经实现。配置中的 true 必须属于适配器声明的支持集合，超出集合则整个候选配置校验失败；不会靠修改布尔值启动 AI、生命值或战斗。

启用映射必须具备 spawn 和 despawn，以确保对象可清理。允许关闭 update_position。禁用定义可保留未来适配器名称，仍检查结构、命名空间和唯一性；不视为可创建类型、不进行能力授权。默认配置只有 marker，没有玩家或 NPC 定义。

## 未来接入框架设计（本阶段不实现）

职责分配：

1. `EntityTypeCatalog`：启动时读取并完整校验配置，提供不可变目录，以 `(源类型, 目标游戏)` 精确查询。目录只描述类型，不保存实体实例，不替代 EntityRegistry。
2. `AdapterRegistry`：接收端通过编译代码显式注册适配器及能力清单。配置只能选择已有适配器，不能加载脚本或创建任意反射对象。
3. `EntityAdapter`：建议接口为 `Spawn(identity, mappedState)`、`Update(identity, mappedState)`、`Despawn(identity)`、`Reset()`；实例操作沿用游戏主线程及资源清理约束。这里是接口设计，没有新增生产接口或工厂。

预期数据流：

```text
Minecraft 原始 entity_state
  → Bridge 原有协议验证
  → 原有 CoordinateMapper（仅映射一次）
  → 原有 EntityRegistry 生命周期处理及转发
  → 7DTD 接收队列
  → 未来本地目录解析类型和能力
  → 已注册适配器，在主线程操作对象
```

解析类型不会修改 `source`、`entity_id`、`stream_id`、`entity_type`、生命周期、metadata 或协议格式。实例仍使用既有 `(source, world_id, dimension, entity_id)` 标识，不能把类型别名当实体 ID。目标类型仅是接收端内部解析结果；映射后的坐标直接交给适配器，不再次缩放或加偏移。

该目录的数据结构允许表达反向映射，但现有 Bridge 的 entity_state 方向仍是 Minecraft → 7DTD；本阶段不增加反向实体通道。

未来启动策略：完整校验成功后一次安装不可变目录；失败不安装候选目录并给出本地诊断。暂不设计热重载，避免修改目录时已有实例失去适配器。断线、世界退出、Bridge 重启的清理沿用已有流程，目录不保存实体、不重放旧 spawn。

未知、禁用或无适配器的类型未来在接收端只记录原因并忽略对象生成，不自动变为 marker、玩家或 NPC。此策略不新增协议错误消息，也不改变 Bridge 当前对格式合法未知类型的处理。关闭位置更新只影响未来对象移动，不改变 Bridge 生命周期记录。

## 自动测试与复现

在工程根目录执行：

```powershell
.\tests\run-phase3_4.ps1
```

需要 Python 3.11+、.NET SDK、已有七日杀编译引用。首次运行下载固定版本 `jsonschema==4.25.1` 到工程内 `work/phase3_4-test/python-libs`，缓存和临时文件也在工程内，不修改系统 Python 包。校验器仅存在于 tests，不被 Bridge 或 Mod 引用。

新测试直接读取正式配置和 Schema，覆盖合法 marker、禁用预留类型、关闭位置能力、未知字段、缺失字段、重复映射、命名空间错配、未知适配器、未实现能力、清理能力缺失、版本错误、非法 JSON 和重复 JSON 键，并检查校验不会重写目录。随后运行原有 Phase 3.2 生命周期和 Phase 3.3 marker 回归测试。

预期：类型配置全部校验通过，负例均被拒绝，原有 87 项生命周期测试与 24 项 marker 测试通过。该结果证明配置设计及现有行为兼容，**不证明运行时已经使用类型目录**。

编译验证：

```powershell
$env:JAVA_HOME = 'C:\Program Files\Java\jdk-21.0.12'
$env:PATH = "$env:JAVA_HOME\bin;$env:PATH"
.\build.ps1
```

编译原有 Bridge、7DTD Mod、客户端测试工具和 Fabric Mod，不部署、不重启游戏。构建产物和测试证据保存在工程内。此次执行结果见下方验收记录。

## 修改文件列表

新增：

- `config/entity_types.json`
- `config/entity_types.schema.json`
- `tests/entity-types/requirements.txt`
- `tests/entity-types/validate_catalog.py`
- `tests/entity-types/test_catalog.py`
- `tests/run-phase3_4.ps1`
- `docs/phase3_4_entity_types.md`

修改：`README.md`，增加设计文档入口及当前阶段边界。

生成：`work/phase3_4-test/` 下依赖、构建和测试日志；原有编译输出目录及回归结果文件。

## 验收记录

本轮执行已完成：

| 检查 | 实际结果 | 工程内证据 |
| --- | --- | --- |
| 类型配置和拒绝边界 | 5 个测试组通过，包含参数化正反例；0 失败、0 错误 | work/phase3_4-test/catalog-tests.log、catalog-results.json |
| 生命周期回归 | 87 项通过 | work/phase3_2-test/results.json；phase3_4-test/lifecycle-regression.log |
| marker 回归 | 24 项通过 | work/phase3_3-test/results.json；phase3_4-test/marker-regression.log |
| Bridge / 7DTD Mod / 客户端测试工具 | Release 构建成功，各 0 警告、0 错误 | work/phase3_4-test/build.log |
| Minecraft Fabric Mod | Gradle build 和 writeTestClasspath 成功，使用已有增量产物 | work/phase3_4-test/build.log |

没有部署构建产物、操作真实游戏、改动现有网络和坐标配置。本阶段属于设计和离线验证，不新增实机验收结论。

## 当前限制与停止点

本阶段完成配置与框架设计，不含生产 EntityTypeCatalog、动态类型分发、目录热重载、能力玩法或新游戏对象。未来接入需单独确认范围，并再次验证主线程操作、断线清理和默认 marker 行为。本阶段结束后停止，等待用户确认。

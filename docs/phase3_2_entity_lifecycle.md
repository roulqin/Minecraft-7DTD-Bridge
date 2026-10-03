# Phase 3.2：Entity Lifecycle State Machine

日期：2026-10-03（Asia/Shanghai）。工程根目录：D:\wenjian\minecraft\7-M。

## 完成结果

Bridge 新增 EntityRegistry，管理实体身份、类型、状态、最近更新时间和已映射的完整状态快照。spawn 创建记录并防止重复创建，update 只更新已有记录，despawn 删除记录。整个流程仅操作内存数据，不生成或删除游戏实体。

两个游戏 Mod、config/network.json、config/coordinate.json 未修改。player_position、entity_state 字段与 version=1、三种事件及旧名称别名、双向 test 均保留。七日杀仍只打印日志，Minecraft 仍使用既有采集和可选手动验收入口。

## 注册表与状态转换

逻辑键为 (source, world_id, dimension, entity_id)，不是单独的原生数字编号。每条活动记录包含：

| 属性 | 含义 |
| --- | --- |
| Key | 来源、世界、维度、entity ID |
| Type | entity_type；活动记录期间不可改变 |
| Status | active，表示 Bridge 已登记；不表示源游戏实体必然存活 |
| StreamId | 本次记录所属 stream_id |
| Sequence | 最近接受的状态消息序号，仅记录 |
| LastUpdate | Bridge 接受 spawn/update 时的 UTC DateTimeOffset |
| State | 经 EntityTransport 校验和坐标映射后的完整 JSON 快照 |

despawn 后记录不存在，不保留 inactive 记录或删除墓碑。查阅记录时返回深拷贝，外部不能改动存储状态；重复 spawn 不覆盖位置或更新时间。注册表线程安全，最多4096条活动记录，达到上限返回 entity_registry_full，不增加记录。

| 当前记录 | 事件 | 处理 | 转发 |
| --- | --- | --- | --- |
| 不存在 | spawn | 新建 active 记录 | 是 |
| 存在、类型/stream一致 | spawn | 忽略 duplicate_spawn，保持原记录 | 否 |
| 存在、类型/stream一致 | update | 替换完整状态并更新时间 | 是 |
| 不存在 | update | 返回 entity_not_found，不创建记录 | 否 |
| 存在、类型/stream一致 | despawn | 删除记录 | 是 |
| 不存在 | despawn | 忽略 unknown_despawn，幂等结束 | 否 |
| 存在但类型不同 | 任一事件 | 返回 entity_type_mismatch | 否 |
| 存在但stream不同 | 任一事件 | 返回 entity_stream_mismatch | 否 |

已删除的记录不能被 update 复活，必须显式 spawn。相同 ID 在不同维度属于不同记录；跨维度移动仍应由源端对旧维度发送 despawn，对新维度发送 spawn。

结构校验仍先由 EntityTransport 执行，非法 JSON/字段/数字遵守原有错误关闭策略。合法结构但状态不适用的错误使用原 error/code 格式，保持 WebSocket 连接可用，不影响后续 player_position/test。重复 spawn/despawn 只写 Bridge 日志，不增加 ACK 或更改协议。

处理链路：接收 → 既有字段校验和坐标映射 → 检查七日杀在线 → Registry 状态转换 → 仅转发获准事件 → 既有七日杀日志。对端离线时返回 peer_unavailable，不创建/更新注册表、不积压离线事件。源端日志的 queued/sent 不等于状态转换成功，必须检查 Bridge 和接收日志。

## 重连处理设计与本轮实现

本阶段采用清空后重新登记，避免旧记录跨连接被误用：

1. 任一已注册角色断线时清空全部活动记录，日志记录断线角色和删除数量。
2. 重连仍使用现有握手/welcome/peer_connected/test，不增加协议字段或通知。
3. 重连后的 update 被拒绝为 entity_not_found；源端必须重新发送完整 spawn，再发送 update/despawn。
4. Bridge 进程重启同样从空表开始，不加载旧快照、不补发缓存。
5. 拒绝重复客户端握手时，由于该连接没有注册成功，不执行清理，原客户端及记录继续有效。

实体应用/发送、连接注册和断线清理共用既有注册门锁，防止新连接创建的记录被上一会话延迟清理。注册表内部另有同步锁，保护查询和状态修改；发送仍遵守既有5秒超时。转发失败会结束该来源会话并触发清空，不承诺事务式交付或可靠重放。

断线不是源游戏实体死亡，因此没有合成 despawn/died。也不要求目标游戏删除对象，本阶段目标端没有生成对象。未来若引入游戏对象，必须先设计目标端会话失效清理/新轮次隔离，不能把当前清空内存表当作目标对象已被清理。

已有 Minecraft 验收入口没有自动重连全量登记；重新连接后需手动执行 `/mc7dtd_entity_test spawn`。本阶段不为 Mod 增加自动实体观察或重新登记逻辑。

## 文件变化

新增：

- bridge-server/EntityRegistry.cs。
- tests/run-phase3_2.ps1。
- docs/phase3_2_entity_lifecycle.md。

修改：

- bridge-server/Program.cs：实体状态转换、转发控制与断线清理日志。
- tests/entity-runner/Program.cs：增加可选 phase3_2 模式；原 Phase 3.1 测试继续保留。
- README.md：测试及报告入口。

生成：Bridge/测试项目 bin/obj、work/phase3_2-test、docs/phase3_2-evidence 和已有运行日志。所有文件位于工程根目录内，两个游戏 Mod 和配置哈希核对未变。

## 编译与自动测试

工程根目录执行：

```powershell
Set-Location 'D:\wenjian\minecraft\7-M'
.\tests\run-phase3_2.ps1
.\tests\run-phase3_1.ps1
.\tests\run-phase1.ps1
.\tests\run-phase2.ps1
.\tests\run-phase2_1.ps1
```

Phase 3.2 脚本构建既有共享接收客户端，再构建实体测试项目及 Bridge 引用，不改动 Mod 源码。Bridge Release/net10.0 编译0警告、0错误。

本次实际结果：

| 测试 | 通过数 | failure |
| --- | --- | --- |
| Phase 3.2 生命周期及传输 | 87 | null |
| 原 Phase 3.1 实体传输 | 57 | null |
| Phase 1 通信 | 14 | null |
| Phase 2 玩家同步 | 25 | null |
| Phase 2.1 坐标映射 | 34 | null |

新增检查覆盖注册记录及时间、完整快照更新、深拷贝、重复 spawn、防止 update 创建/复活实体、幂等删除、类型/stream冲突、维度身份、容量上限和清空。真实 WebSocket 测试还验证重复事件不被转发且连接可用、重复客户端不能清理记录、两种角色分别断线重连后要求新 spawn。共享 net48 七日杀接收类的日志、Bridge 重启后的接收恢复、旧位置和test回归继续通过。

结果：work/phase3_2-test/results.json；各进程日志在同目录。摘要和生命周期服务器日志保存在 docs/phase3_2-evidence/results.json、lifecycle-server.log。

## 启动与验证方法

仍使用原 start-bridge.ps1。当前新版 Bridge 已由原启动脚本恢复运行，请不要重复启动正式监听实例。

若执行手动复验，使用既有默认关闭的 Minecraft 验收入口（启动时 MC7DTD_ENTITY_TEST=1）：先 spawn，再重复 spawn，Bridge 应出现 duplicate_spawn、count不增加，七日杀只有一次新增记录日志；执行 update 应继续转发；despawn 后count减少，随后的 update 应返回 entity_not_found，而 player_position 仍正常。

任一游戏重连后，先尝试 update 应得到 entity_not_found，再发送新的 spawn 应成功。日志 Entity registry reset 说明清理发生；服务重启后同样需要新 spawn。实施阶段已自动测试这些行为；2026-10-03 后续完成真实 Minecraft 手动状态机及 Bridge 重启验收，结果见 [Phase 3.2 实机验收报告](phase3_2_runtime_acceptance.md)。重复 spawn 未转发，重启后同身份 update 被拒绝，新 spawn 正常建立，最终 despawn 清至 0。

## 当前限制与停止点

注册表为内存表，没有持久化、状态查询HTTP接口、自动过期、离线重放或目标实体对应关系。LastUpdate 是接收时刻，不是源采样时刻；status=active 是注册状态，不是网络在线/游戏存活判定。

本阶段只实现基础生命周期条件和重复 spawn 防护。sequence 记录在表中，未实现严格单调序号过滤；旧传输测试允许相同序号的不同事件，继续兼容。没有墓碑和已退役stream历史，因此删除/清空后收到显式旧 spawn 仍可能被重新登记。Phase 3.0 的完整乱序、旧轮次和全量快照设计仍需后续实现；本报告不宣称已完成全部设计规则。

仍只处理 Minecraft → 七日杀实体事件。没有游戏实体生成、删除、伤害、插值或方块同步。Phase 3.2 基础生命周期管理完成，到此停止，等待下一步确认。

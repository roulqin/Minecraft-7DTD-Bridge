# Phase 3.1：Entity Transport Layer

日期：2026-10-03（Asia/Shanghai）。工程目录：D:\wenjian\minecraft\7-M。

## 结果与架构

Bridge 支持 Minecraft 角色发送 entity_state，校验后映射坐标并转发到七日杀；七日杀共享接收类只打印日志，不调用游戏对象、实体生成、删除、伤害或AI API。Minecraft Mod 未修改，继续使用现有 player_position。

数据链路：自动测试的 Minecraft 角色发送 JSON → Bridge EntityTransport 校验 → CoordinateMapper → 七日杀共享 BridgeClient 反序列化 → 日志。将来的真实 Minecraft 实体采集不在本轮范围内；自动测试的发送器不会改变游戏。

正式地址仍为 ws://localhost:18771/ws，config/network.json、coordinate.json 和现有连接/重连逻辑未修改。player_position 和双向 test 保留。

## 事件与消息

生命周期使用 lifecycle.event=spawn、update、despawn。为兼容 Phase 3.0 设计，接受 announce→spawn、remove→despawn 输入别名，转发统一使用新名称。version=1，除此之外结构与 Phase 3.0 一致。

spawn/update 必须携带完整 position、rotation、metadata；despawn 必须 position=null、rotation=null、metadata={}，并给出 lifecycle.reason。spawn 仅是事件名，不产生实体。

完整源端示例：

```json
{
  "type": "entity_state",
  "version": 1,
  "source": "minecraft",
  "stream_id": "55c44b7e-82c8-461d-8c2b-d4e4e4e77fd1",
  "entity_id": "6b6d9c36-909c-4fbb-a537-d309f48f8231",
  "entity_type": "minecraft:player",
  "world_id": "demo-world-01",
  "dimension": "minecraft:overworld",
  "sequence": 1,
  "lifecycle": { "event": "spawn" },
  "position": { "x": 10, "y": 64, "z": 20, "space": "minecraft" },
  "rotation": { "yaw": 90, "pitch": -10, "roll": 0 },
  "metadata": { "health": 20, "max_health": 20, "is_alive": true }
}
```

使用 scale=2、offsets=(100,-10,25)，出站 position=(120,118,65)，space=7dtd。source 与身份字段不变，rotation 不做缩放或偏移；metadata 原样转发，但七日杀目前不应用或打印 metadata。

接收日志示例（实际自动测试使用七日杀接收源码的 net48 客户端）：

```text
Entity state: event=spawn id=6b6d9c36-909c-4fbb-a537-d309f48f8231 type=minecraft:player source=minecraft world=demo-world-01 dimension=minecraft:overworld stream=55c44b7e-82c8-461d-8c2b-d4e4e4e77fd1 sequence=2 x=120 y=118 z=65 yaw=90 pitch=-10 roll=0
Entity state: event=update ... x=120 y=118 z=65 yaw=90 pitch=-10 roll=0
Entity state: event=despawn ... reason=world_unloaded
```

sequence=2 是传输测试样本，不表示本层已经应用了生命周期顺序。完整日志见 work/phase3_1-test/process-1.log；其余日志含 Bridge 初次启动和重启记录。

## 校验和错误

Bridge 校验必填/额外/重复字段、version、与握手角色相符的 source、UUID、命名空间类型、世界/维度标识、sequence 范围、事件及移除原因、有限数字和旋转范围。metadata 必须是≤32键的扁平对象，各值/长度及预留 health/max_health/is_alive 等类型按协议检查。

spawn/update 使用正比例映射，拒绝 scale≤0、入站错误 space 或计算溢出；despawn 没有坐标，允许完成移除通知而无需有效比例。整个入站消息继续受8192字节/深度8的原限制，转换后再检查出站 JSON 的8192字节限制，避免数值或编码展开后超出接收上限。旧 player_position 的零/负比例行为不变。

新增错误代码包括 invalid_entity_source、invalid_entity_version、invalid_entity_id、invalid_entity_type、invalid_entity_world、invalid_entity_dimension、invalid_entity_sequence、invalid_entity_lifecycle、invalid_entity_reason、invalid_entity_space、invalid_entity_scale、invalid_entity_rotation、invalid_entity_metadata、invalid_entity_fields、invalid_entity_state、invalid_entity_number、duplicate_entity_field、entity_message_too_large；计算溢出沿用 coordinate_mapping_overflow。返回现有 error/code 并结束违规会话。对端离线仍返回 peer_unavailable，不缓存。

## 文件列表

新增：

- bridge-server/EntityTransport.cs。
- tests/entity-runner/EntityRunner.csproj。
- tests/entity-runner/Program.cs。
- tests/run-phase3_1.ps1。
- docs/phase3_1_entity_transport.md。

修改：

- bridge-server/Program.cs：新增 entity_state 分支；其余消息路径保留。
- 7dtd-mod/src/BridgeClient.cs：实体传输 DTO 和纯日志接收。
- docs/entity_sync_protocol.md：澄清设计基线、新名称及已实现子集。
- README.md：阶段、测试和报告入口。

生成：Bridge、七日杀 Mod、客户端和实体测试项目的 bin/obj；七日杀 dist/MC7DTD-Bridge；work/phase3_1-test；历史回归各自的 work 测试结果。证据摘要另存 docs/phase3_1-evidence。所有文件位于工程根目录。

## 编译、启动与测试

在工程根目录运行：

```powershell
Set-Location 'D:\wenjian\minecraft\7-M'
.\tests\run-phase3_1.ps1
.\tests\run-phase1.ps1
.\tests\run-phase2.ps1
.\tests\run-phase2_1.ps1
```

Phase 3.1 脚本先构建七日杀 net48 Mod 和共享源码客户端，再构建实体测试项目及其引用的 Bridge/net10.0。需要现有 .NET SDK、Framework 4.8 引用程序集和七日杀游戏程序集；不要求重编译 Minecraft。Bridge、七日杀和接收客户端均编译成功，0 警告、0 错误。

实际通过：Phase 3.1 57项、Phase 1 14项、Phase 2.0 25项、Phase 2.1 34项；所有 results.json 均 failure=null。

实体测试覆盖三个规范事件和两个别名、默认/非默认映射、完整输出及metadata保留、非法字段/值、移除格式、重复键、出站大小、对端离线、实际net48接收类日志、旧位置日志与双向test、Bridge重启后接收类自动重连及新轮次消息记录。测试使用工程内隔离配置和空闲本地端口，不修改正式18771端口。

正式 Bridge 使用原 start-bridge.ps1；本轮编译后已由原启动脚本重新运行新版服务。不要启动重复监听实例。新版七日杀产物为 7dtd-mod/dist/MC7DTD-Bridge/MC7DTD.Bridge.dll；实机接收前应通过已有准备流程部署到工程 runtime/7dtd/Mods/MC7DTD-Bridge，并正常重启七日杀。仅替换磁盘 DLL 不会更新已运行的进程。

本轮未重启七日杀、未部署新版 DLL 到运行实例、未发送真实游戏实体状态。当前游戏里的旧版 Mod 只能证明旧通信链路，不能充当本轮新版实体日志实机验收。Minecraft 尚无实体发送功能；可运行自动测试验证传输链路，不能要求它自动产生spawn/update/despawn。

## 当前限制与停止点

本轮仅支持 Minecraft → 七日杀实体传输；七日杀来源的 entity_state 返回 invalid_entity_source，不做逆向转换。Minecraft Mod 源码和现有player_position保持不变。

本层无实体状态表，不执行序号去重、旧stream排除、生命周期顺序或墓碑清理。序号和轮次只校验格式；完整状态机仍是 Phase 3.0 的后续应用层设计。七日杀仅记录合法收到的事件，不将spawn当作生成命令或despawn当作删除命令。没有实体创建、游戏对象引用、战斗、插值、身份绑定或方块同步。

Phase 3.1 到此完成并停止。下一步可在明确授权后进行新版日志接收实机验收；实体生成需要另行确认。

## 2026-10-03 实机验收补充

用户授权在 Minecraft 增加默认关闭的手动验收命令，真实客户端已发送 spawn/update/despawn，七日杀新版 Mod 已实机加载并打印三个事件。完整数据核对通过，详见 [实机验收报告](phase3_1_runtime_acceptance.md)。本补充更新上文“未部署、未实机验收”和 Minecraft 尚无发送入口的历史状态；协议不改动，不生成实体，不实现生命周期状态机。

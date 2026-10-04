# Phase 3.7.3 — Entity Presentation System v1

## 范围与现有系统

Presentation 描述同步实体在目标游戏中的表现意图。本版本只实现协议、配置、状态存储和同步日志；renderer/model/variant 都是标识，不是资源路径，不加载模型、皮肤或动画，也不改变代理外观或缩放。

保留 entity_state v2 的 entity_id、entity_type、source、authority、origin、stream_id、sequence、lifecycle、position、rotation 和 metadata。既有 Minecraft v1 发送路径也支持这一可选扩展，不升级它的 Authority 或序号规则。Health/Identity 继续使用原 entity_components v1、name/custom_metadata 字段、权限及 revision/base_revision；没有改成示意例中的 health.value 或 identity.name。

## 数据结构

文件：components/presentation/PresentationComponent.cs（7DTD 共享 DTO）；bridge-server/PresentationComponent.cs（校验、默认值、状态合并）；components/presentation/presentation.schema.json（字段契约）。

```json
{
  "renderer": "humanoid",
  "model": "default",
  "variant": "survivor",
  "scale": 1.0
}
```

| 字段 | 语义 | 校验 |
| --- | --- | --- |
| renderer | 渲染器标识 | 1–64 个小写字母、数字或 `_.:-` |
| model | 模型标识 | 同上；不解析为文件或 URL |
| variant | 可选变体 | 同上；缺省不注入 variant |
| scale | 意图显示比例 | 有限数，`0 < scale <= 16`；拒绝字符串、布尔值及 null |

字段 patch 可以只包含任意子集。对象中的字段 null 不表示删除字段；**整个 presentation 为 null 才表示删除该组件**。未知字段和重复字段被拒绝。空对象是合法空 patch。

## 配置与默认值

config/entity_types.json 为每种已配置实体增加可选 presentation_default，config/entity_types.schema.json 已扩展校验。当前默认值：

| entity_type | renderer | model | variant | scale |
| --- | --- | --- | --- | --- |
| minecraft:marker | marker | default | default | 1 |
| minecraft:player | humanoid | player | default | 1 |
| 7dtd:player | humanoid | survivor | default | 1 |

Bridge 启动读取配置；修改后需要重启 Bridge 和重新 spawn。缺少 presentation_default 的旧类型使用：

```json
{"renderer":"unknown","model":"default","scale":1}
```

spawn 时先读取类型默认值，再逐字段合并消息的显式 presentation。Bridge 发出完整有效值，两端无需重复加载表现配置。没有 presentation 的旧消息仍被接受；已有类型获得其配置默认值，未配置类型获得通用默认值。不改变现有类型的 target_mapping、capabilities 或 enabled 语义。

## 生命周期、更新和权限

1. 源端发送原 entity_state，加可选 `components.presentation`。
2. Bridge 先验证 Presentation 数据，再调用原 v1/v2 transport 检查身份、来源、Authority/Origin 和坐标映射。没有新的 Authority 规则或序号系统。
3. 在既有生命周期锁内解析默认值/合并 patch，再让原 EntityRegistry 或 NativeEntityRegistry 应用事件。只有接受的事件才提交 Presentation 状态并转发。
4. 目标端保存元数据并日志记录。7DTD 在代理生成的游戏主线程读取并记录 renderer/model/variant；网络线程只入队并复制快照。
5. 重复 spawn、非法身份、未知 update、错误 stream 和 v2 旧序号不改变 Presentation。v1 沿用原生命周期规则。
6. despawn 删除该实体的 Presentation 状态。Bridge 重启及原重连清理边界清空状态，按既有机制重新 spawn。

Presentation 状态以 `(source, world_id, dimension, entity_id)` 隔离。不能借 Presentation 修改 entity_id、source、authority 或 origin；这些字段仍由现有 Authority/生命周期系统验证。组件不是单独的转发通道，不会把镜像实体发回源游戏。

| 输入 | 处理 |
| --- | --- |
| spawn + 对象 | 类型默认值合并显式字段 |
| spawn 无组件 | 类型默认值，或通用默认值 |
| update + 对象 | 指定字段合并已有状态；其余字段保留 |
| update 无组件 | 保留已有状态 |
| update + presentation:null | 删除 Presentation；实体、Health、Identity 保留 |
| 删除后 add/update + 对象 | 在通用默认值上重新建立组件 |
| despawn | 清理状态；不得附带 Presentation |

remove 后目标端可读取通用默认表现；不会再次隐式套用类型默认值。add 是已有实体的 update 中重新附带对象，**不新增 lifecycle.event=add/remove**。不改变 Health/Identity 的 partial update 或权限含义。

## 示例

完整合法 v2 示例见 docs/examples/presentation：spawn.json、update.json、remove.json、add.json、despawn.json。使用既有 entity_id/entity_type，避免把消息类型 type 与实体类型混淆。

初始 spawn 组件：

```json
{"components":{"presentation":{"renderer":"humanoid","model":"survivor","variant":"default","scale":1}}}
```

随后 update：

```json
{"components":{"presentation":{"scale":1.5}}}
```

Bridge 中 model=survivor、renderer=humanoid、variant=default 保留，仅 scale 改为 1.5。

删除：

```json
{"components":{"presentation":null}}
```

这些片段必须放进完整有效的 entity_state；update 要有源坐标和 rotation，并使用原实体/stream/生命周期序号。

## 编译与自动测试

在工程根目录执行：

```powershell
.\tests\run-phase3_7_3.ps1
```

脚本分别编译 Minecraft、Bridge/测试和 7DTD，测试组件生命周期、field patch、权限拒绝、旧 v1/v2、默认配置、主线程快照及真实双向 WebSocket；随后运行 Health/Identity 回归与 schema 验证。测试使用工程 work 内的独立随机端口，不改 config/network.json 的 localhost:18771；不启动游戏。

构建产物：minecraft-mod/build/libs/mc7dtd-bridge-0.1.0.jar；bridge-server/bin/Phase373/net10.0/BridgeServer.dll；7dtd-mod/bin/Phase373/net48/MC7DTD.Bridge.dll。日志与结果在 work/phase3_7_3-test。已有组件回归脚本沿用其既有 work/phase3_7_2-test 输出目录。

### 本轮结果（2026-10-04）

```text
Minecraft build PASS
Bridge build PASS
7DTD build PASS

Presentation Spawn      PASS
Presentation Update     PASS
Presentation Remove     PASS
Backward Compatibility  PASS
```

Bridge 与 7DTD 构建均为 0 错误、0 警告，Fabric build 成功。新增验证 81 项通过：C# 生命周期/存储/权限/真实双向 WebSocket 42 项，Java PresentationReceiver 10 项，schema/示例/受保护文件哈希 29 项。既有回归 135 项通过：Java Identity 23、Health 20，C# Identity/Health/真实 WebSocket 51，原组件契约 41；另外校验 7 条真实序列化组件消息。

证据文件：work/phase3_7_3-test 的 presentation.log、results.json、PresentationHarness.log、schema.log、identity-regression.log、identity-schema-regression.log、identity-schema-regression-stderr.log 和三份 build.log，以及 bridge.log、minecraft.log、7dtd-client.log。后面三份日志来自自动测试进程，并非本轮真实游戏截图或实机证据。

测试过程中修正了测试夹具的跨线程 Tick 调用；原游戏主线程保护未改变。Windows PowerShell 把成功 unittest 的 stderr 进度当作错误，已在运行脚本按进程重定向并检查实际退出码；schema 引用也使用工程内资源注册，不下载外部 schema。最终完整脚本退出码为 0。

## 用户手动验收

本阶段不使用 computer-use、不自动操作或启动游戏。以下步骤由用户执行；关闭旧游戏及旧 Bridge，避免重复实例。Steam 保持登录，沿用工程内已有 runtime、Fabric 和 Mod 安装目录。

1. 启动 Bridge：在工程根目录运行 `.\tests\launch-phase3_7_3.ps1 bridge`。查看 docs/phase3_7_3-runtime-evidence/bridge-stdout.log 的 listening/Bridge connected；端口保持 18771。
2. 启动 Minecraft：运行 `.\tests\launch-phase3_7_3.ps1 minecraft`，进入测试世界。该启动器部署工程内最新 JAR，并设置 `MC7DTD_ENTITY_TEST=1` 启用验收命令。
3. 启动 7DTD：运行 `.\tests\launch-phase3_7_3.ps1 7dtd`，进入既有测试世界。启动器只更新工程 runtime 下的 Mod DLL；游戏日志位于 docs/phase3_7_3-runtime-evidence/7dtd-game.log。
4. Minecraft 中依次执行 `/mc7dtd_presentation_test spawn` → `update` → `remove` → `add` → `despawn`（每条都带完整命令前缀，间隔至少 1 秒）。测试对象为独立的 minecraft:marker，不修改自动玩家代理。spawn=model survivor/scale 1，update 只发 scale 1.5，remove 发 null，add 恢复 scale 1，despawn 清理。
5. 核对同一 entity_id：Bridge 出现 Presentation 状态；7DTD `Spawn proxy: ... renderer=humanoid model=survivor`，update 日志保留 model/renderer 且 scale=1.5，remove `removed=True`，despawn 代理 count 下降。**外观及大小不要求变化。**
6. 验证反向默认流程：七日杀正常进入世界，Minecraft 的 runtime/minecraft/logs/latest.log 应出现 `[Presentation] entity=... renderer=humanoid model=survivor ... scale=1.0`。继续观察原 Health/Identity 日志；正常退出七日杀世界仍收到原组件清理及 despawn。反向 field patch/remove 已用实际 C# 发送 DTO → Bridge → Java BridgeClient 自动测试，本版本不新增七日杀游戏操作指令。

启动器会覆盖本阶段同名运行日志，复验前按需在工程目录内保存上一轮。人工验收结果待用户执行后填写；自动 WebSocket 测试不等同于本阶段实机验收。

## 当前限制

没有资源加载、渲染替换、动画、Skin、装备、战斗或输入控制。Presentation 不持久化，没有额外 ACK 或重传协议；沿用现有连接和生命周期规则。配置启动加载，不热更新；Minecraft/7DTD 读取的是 Bridge 发送的有效表现状态，继续使用已有代理。Phase 3.7.3 完成后停止，不进入 Phase 3.8。

## 本阶段文件变更

新增：

- bridge-server/PresentationComponent.cs
- components/presentation/PresentationComponent.cs、presentation.schema.json
- minecraft-mod/src/main/java/io/mc7dtd/PresentationReceiver.java、PresentationTestCommands.java
- minecraft-mod/src/test/java/io/mc7dtd/PresentationHarness.java
- tests/presentation-runner/PresentationRunner.csproj、Program.cs、validate.py
- tests/run-phase3_7_3.ps1、tests/launch-phase3_7_3.ps1
- docs/Entity_Presentation_System.md、docs/examples/presentation/ 下五份 JSON

修改：

- bridge-server/Program.cs：可选表现组件入口、接受后提交状态、清理。
- config/entity_types.json、entity_types.schema.json：表现默认配置。
- docs/entity_state_v2.schema.json：可选 components.presentation。
- minecraft-mod/src/main/java/io/mc7dtd/BridgeClient.java、MinecraftBridgeMod.java：独立接收状态和验收命令注册。
- 7dtd-mod/src/BridgeClient.cs、MarkerController.cs、7dtd-mod/MC7DTD.Mod.csproj：共享 DTO、代理元数据保存与日志。
- tests/client-harness/ClientHarness.csproj、marker-runner/MarkerRunner.csproj、player-proxy-runner/PlayerProxyRunner.csproj、native-transport-runner/NativeTransportRunner.csproj、health-runner/HealthRunner.csproj、identity-runner/IdentityRunner.csproj：链接共享 DTO，保持原测试编译。
- tests/identity-runner/Program.cs：允许回归测试指定本阶段 Bridge DLL，默认运行路径仍保留。

构建、测试及辅助修改工具均保存在工程 bin/obj/build/work。原 Health/Identity 实现、组件权限配置、CoordinateMapper、两个 Registry、原 transport/Authority 校验及网络配置均通过本阶段前后 SHA-256 对比验证未改动。

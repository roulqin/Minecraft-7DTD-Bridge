# Phase 3.1 Entity Transport 实机验收报告

日期：2026-10-03（Asia/Shanghai）。工程目录：D:\wenjian\minecraft\7-M。

## 验收结论

通过。真实 Minecraft 1.21.11/Fabric 客户端在游戏内执行手动命令，发送 spawn、update、despawn；现有 Bridge 转发，真实七日杀 V3.2.0 b10 进程的 Mod 逐条打印接收日志。移动后的 update 与 spawn 坐标不同，接收值与实际源消息一致。

没有生成跨游戏实体，没有修改 entity_state 协议，没有实现生命周期状态机。七日杀保持日志接收功能，不访问实体创建、删除或伤害 API。

## 前置缺口与授权

Phase 3.1 初始交付保留 Minecraft Mod 不变，只发送 player_position，因此原构建不能从真实 Minecraft 进程发送实体消息。用户本轮明确回答“允许加入验收入口”，授权增加仅验收用途的手动入口。

新增入口默认关闭，只有 Minecraft 进程环境 MC7DTD_ENTITY_TEST=1 时注册以下 Fabric 客户端命令：

```text
/mc7dtd_entity_test spawn
/mc7dtd_entity_test update
/mc7dtd_entity_test despawn
```

命令在游戏线程读取当前真实玩家 UUID、位置、朝向、血量、维度；发送使用有界后台队列并复用原 WebSocket，不阻塞游戏线程。验收 world_id 固定为 runtime-acceptance，stream_id 在本次验收入口实例中生成，sequence 只是手动消息计数器。没有活动实体表、自动观察、状态转换或顺序约束，也没有重连后的实体重建逻辑。

despawn 是手动发送的协议测试事件，reason=out_of_scope，不代表玩家真的消失、死亡或退出世界。它不会停止原 player_position 采集。

## 环境与实际操作

- Minecraft Java 1.21.11，Fabric Loader 0.18.4、Fabric API 0.140.2+1.21.11、Java 21.0.12，工程 runtime/minecraft 真实试玩客户端及既有 Demo World。
- 七日杀 V3.2.0 b10，工程 runtime/7dtd 的标准 Mod 目录。游戏日志确认加载工程内新版 DLL、初始化 ModAPI，Steam Login ok。
- Bridge 使用已交付的 Phase 3.1 构建，localhost:18771；本轮没有修改 Bridge、协议或映射配置。
- 映射为默认 scale=1，offsetX/Y/Z=0。本次只做默认映射实体实机验收；非默认实体转换仍由已有自动测试验证。
- 新 Minecraft PID 32092、七日杀 PID 32180，Bridge PID 8364。两款旧测试游戏正常保存/退出后，使用原准备和启动脚本部署/启动新版；没有改动原游戏安装目录。

Minecraft 启动日志 14:10:41 显示 Manual entity acceptance commands enabled，14:10:42 连接成功。七日杀 14:10:44 连接并收到 Minecraft 测试消息，14:10:50 Steam 登录成功。

用户在真实 Minecraft 世界依次执行三个命令，在 spawn 与 update 之间移动，并回复“已完成”。本轮未启动独立模拟 Minecraft 角色发送实体消息；三个实体 JSON 都来自 Minecraft 游戏进程。

## 三事件与数据对照

entity_id：00000000-0000-0000-0000-000000000001（本测试试玩客户端实际玩家 UUID）。
stream_id：fd814250-684a-43bd-b1a5-1f4c25779574。

| 时间 | 事件/序号 | Minecraft 实际发送 | 七日杀实际日志 |
| --- | --- | --- | --- |
| 14:12:53 | spawn / 1 | (-29.48842968439675,64,29.593251520771876) | 同一位置；yaw=199.05028，pitch=18.750008，roll=0 |
| 14:13:12 | update / 2 | (-28.270701005049695,64,26.403712499538088) | 同一位置；yaw=150.9001，pitch=24.599995，roll=0 |
| 14:13:26 | despawn / 3 | position=null、rotation=null、metadata={}，reason=out_of_scope | despawn、序号3及相同原因，不携带坐标 |

移动差值为 Δx=1.2177286793470543、Δy=0、Δz=-3.189539021233788，证明 update 使用移动后的实际玩家状态。

完整 Minecraft 发送 JSON 与七日杀日志逐条对照，身份、类型、source、world、dimension、stream 和 sequence 均一致。按保存的 coordinate.json 计算目标坐标，位置误差≤1e-12，旋转打印值误差≤1e-6。不同运行时的最后一位十进制打印差异不表示坐标被修改。Bridge 同时记录同一 ID 的三个 Entity state forwarded 事件。

解析结果 entity-comparison.json：passed=true、gameRuntimeTested=true。七日杀记录三条实体状态，Minecraft 发送三条；不是仅验证连接成功。

## 文件变化

新增源码：

- minecraft-mod/src/main/java/io/mc7dtd/EntityTestCommands.java：默认关闭的手动验收入口。

修改源码：

- minecraft-mod/src/main/java/io/mc7dtd/MinecraftBridgeMod.java：读取验收开关并注册命令。
- minecraft-mod/src/main/java/io/mc7dtd/BridgeClient.java：验收专用有界后台发送入口。

文档：新增本报告，README.md 和 phase3_1_entity_transport.md 更新验收说明。entity_sync_protocol.md 不改动。

生成/部署：Minecraft build 中新版 JAR、runtime/minecraft/mods 中对应 JAR；runtime/7dtd/Mods 中已交付的 Phase 3.1 DLL/XML；工程内测试日志、缓存、存档和 docs/runtime-evidence 运行记录。七日杀源码、Bridge 源码、网络/坐标配置和构建配置未修改。source-check.json 记录原文件核对，仅上述两个已有 Java 源文件改变；新增命令文件另列。

## 证据位置

目录：docs/phase3_1-runtime-evidence/。

- minecraft-latest.log：真实 Minecraft 加载、连接和完整 Entity test sent JSON。
- 7dtd-game.log：真实游戏 Mod 加载、Steam 登录和三条 Entity state 日志。
- bridge.log：对应三个转发事件及原通信记录。
- minecraft-commands.png：游戏内三条手动命令的反馈画面。
- entity-comparison.json：逐条数据核对及真实移动差值。
- coordinate.json、health.json：本次映射配置与两个在线角色。
- artifacts-before.json、deployed-hashes.json：旧/新产物和工程内部署哈希。
- source-before.json、source-check.json：已有源码/配置的变更边界。
- before-relaunch/：正常重启前的运行日志备份。

七日杀日志含系统本地编码文本，核对程序保留原始日志并只分析 ASCII 的实体行，未改变原始文件编码。

## 编译与复验方法

Minecraft 构建使用现有缓存完成 compileJava、remapJar、build、writeTestClasspath，BUILD SUCCESSFUL；现有 Phase 2 玩家/通信回归25项全通过。七日杀直接部署已经通过57项传输测试的既有 Phase 3.1 构建，不需要再次修改或编译它。

从工程根目录准备既有产物并运行（先正常退出旧测试游戏，避免重复连接）：

```powershell
.\tests\prepare-runtime.ps1
# Bridge 未运行时，先运行 .\start-bridge.ps1。
$env:MC7DTD_ENTITY_TEST = '1'
python tests/launch-runtime.py minecraft
Remove-Item Env:MC7DTD_ENTITY_TEST
python tests/launch-runtime.py 7dtd
```

进入 Minecraft 世界，在游戏内执行 spawn，移动后执行 update，最后执行 despawn。检查三个组件的日志，预期同一身份、递增序号和变化后坐标均一致。聊天中的 queued 仅代表入队，完整通过必须看七日杀接收日志，不能只凭发送日志。

普通启动不设置 MC7DTD_ENTITY_TEST，三个验收命令不会注册。此环境变量已从启动脚本进程移除，但当前已启动的 Minecraft 子进程仍启用入口，正常重启且不设置开关即可关闭。新增入口默认保留为可复验工具，未自动删除用户授权的源文件。

## 限制与停止点

本次验证的是手动源状态传输，不是自动实体发现、死亡/重生检测或完整生命周期同步。world_id/stream_id/序号只服务本次验收，不承诺跨存档/重连身份连续性。没有检查玩家相机与原生实体本体角度之间的完整适配；本轮只核对发送值与接收日志一致。

七日杀保持真实游戏主菜单，不进入七日杀存档、不创建对象；因此不能把日志通过解释为已经出现跨游戏玩家实体。试玩账号的 Realms/认证错误不阻断本地链路，游戏安装目录同名旧 Mod 仍被忽略，本次工程内新 DLL 已加载。

两款游戏和 Bridge 仍在运行。本阶段实机验收完成，到此停止；未进入生命周期状态机或实体生成。

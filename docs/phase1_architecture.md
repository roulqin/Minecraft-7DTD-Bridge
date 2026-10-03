# Phase 1 架构与协议

## 组件与版本

Minecraft 1.21.11 / Fabric Loader 0.18.4 → Java 21 HttpClient WebSocket → ASP.NET Core .NET 10 Bridge → System.Net.WebSockets.ClientWebSocket → 七日杀 IModApi Mod。
WebSocket 为全双工，两端均可发送和接收测试消息。

Bridge 使用 SDK 10.0.400 / net10.0。七日杀使用 net48：本地 Assembly-CSharp 的间接依赖 BhvrAnalyticsMS 要求 .NET Framework 4.8，不能用 net10.0 替代。编译引用实际游戏 Assembly-CSharp.dll 与 LogLibrary.dll，Private=false，不复制游戏程序集。退出事件 ModEvents.GameShutdown / SGameShutdownData 已经本机程序集编译验证。

Fabric 端只使用 Loader 客户端入口、JDK 网络接口、游戏已带的 Gson/SLF4J，不引用混淆的 Minecraft 类型。因此本阶段使用 Gradle Java 插件生成 Fabric-loadable JAR，无需 Loom 重映射或 Fabric API。未来使用游戏类时应引入匹配的 Loom/mappings。本阶段没有引入游戏数据访问。
Gradle Wrapper 固定 8.14.3，并校验官方 SHA-256。

## 配置

三端共享 config/network.json：host 只允许 localhost，port 允许 1024..65535，默认 18771，WebSocket 路径 /ws。
Bridge 绑定 IPv4/IPv6 回环，不监听局域网。GET /health 返回状态和已注册角色。
Mod 默认从 D:\wenjian\minecraft\7-M 读取配置；可用进程环境变量 MC7DTD_ROOT 指向同一工程目录。无效配置记录错误，不回退其他地址。修改配置后重启三端。

## 消息流程

Mod 初始化启动后台线程，不阻塞游戏主线程，不要求进入世界。首条消息必须为：

```json
{"type":"minecraft_connect","client":"minecraft"}
{"type":"7dtd_connect","client":"7dtd"}
```

Bridge 验证角色和类型，每种角色最多一个连接，回复：

```json
{"type":"welcome","client":"minecraft","message":"Bridge connected"}
```

第二端注册后向两端发送 peer_connected，两端各自动发一次测试。注册锁保证先 welcome 后通知。

```json
{"type":"peer_connected","client":"7dtd"}
{"type":"test","text":"Hello from Minecraft"}
```

Bridge 将测试转发给另一角色，强制根据握手身份填写 from：

```json
{"type":"test","from":"minecraft","text":"Hello from Minecraft"}
```

反方向发送 Hello from 7DTD。接收方只写日志，不重复转发，避免循环。重连后重新握手、通知和交换消息。

## 约束与处理

- 文本 JSON 对象，上限 8192 字节，JSON 深度 8，test.text 长度 1..256；支持消息分片。
- 握手总超时 10 秒，发送超时 5 秒；客户端连接超时 5 秒、welcome 超时 10 秒、失败后间隔 3 秒重试。
- 每条连接串行发送；断开释放角色。游戏退出取消后台任务，无世界/玩家 API 调用。
- 非法消息返回 error/code 后关闭；对端未连接返回 peer_unavailable，不缓存。
- 握手后仅允许 test；player_position、player、block、entity 等全部拒绝。
- 拒绝带 Origin 的浏览器连接。无认证/TLS，仅用于本机开发；本机其他程序仍可冒充角色。
- Bridge 输出就绪、注册、转发、拒绝与断开日志。start-bridge.ps1 保存 work/logs/bridge.log；两端分别使用 SLF4J 和 Log.Out。

Bridge connected 在服务端表示开始监听，在客户端表示收到 welcome。双端互通必须检查双方 Test received 日志，不能只凭这一行。

## 参考依据

- [Fabric 客户端入口源码](https://raw.githubusercontent.com/FabricMC/fabric-loader/0.18.4/src/main/java/net/fabricmc/api/ClientModInitializer.java)
- [Fabric 项目结构](https://docs.fabricmc.net/develop/getting-started/project-structure)
- [ASP.NET Core WebSocket](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/websockets?view=aspnetcore-10.0)
- [Gradle 官方校验和](https://gradle.org/release-checksums/)
- [七日杀官方论坛 Mod 目录公告](https://community.thefunpimps.com/threads/20-4-new-mod-folder-location.28389/page-5)

七日杀 API 的最终依据为本机实际程序集；目录公告用于指导手动验收。

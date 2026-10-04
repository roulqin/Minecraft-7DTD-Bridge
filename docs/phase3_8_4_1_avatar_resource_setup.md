# Phase 3.8.4.1 Avatar Resource Setup — 设计更新与完成报告

本阶段仅完成本地 Avatar 资源管理基础。没有模型显示、皮肤同步传输、7DTD 模型加载或游戏初始化接入。Entity/Equipment 协议、Authority、Presentation、Bridge 通信及现有 Renderer Runtime 未修改。

## 资源目录

```text
assets/avatar/
├── skins/
│   ├── player_default.png
│   └── djsgj.png                 # 用户已有原图，保留
├── models/
│   └── .gitkeep                  # 占位，无模型资源或模型加载
├── avatar_config.json
└── avatar_config.schema.json
```

当前目录已有 `djsgj.png`（64×64 PNG）及空配置文件。本轮保留原图不变，原样复制为默认资源及内置回退资源；没有生成或编辑皮肤图像。配置填写为：

```json
{
  "avatar_id":"default_player",
  "model":"minecraft_humanoid",
  "variant":"alex_slim",
  "skin":"skins/player_default.png"
}
```

`minecraft_humanoid` 是模型契约标识，不代表已加载模型。`alex_slim` 解析为 ALEX_SLIM，对应上一阶段设计的 slim 语义；兼容 `steve_classic`→STEVE_CLASSIC。皮肤图像不会自动推断 classic/slim，变体来自明确配置。本阶段不验证将来实际 Mesh 的 UV/手臂宽度。

## 独立资源管理器

新增 Minecraft 模块中的 `AvatarResources`，依赖 Java 文件 IO、Gson 与标准 ImageIO，不调用 Minecraft/Unity 模型或纹理显示 API。既有 MinecraftBridgeMod 初始化代码未接入该类，因此当前游戏行为不改变。

接口与结果：

```text
AvatarResources.load(avatarRoot) → Result

Result {
  config: Config,
  skin: Skin | null,
  configFallback: boolean,
  warnings: immutable list,
  ready(): boolean
}

Skin {
  path: resolved real local path | null,
  source: LOCAL | DEFAULT_FILE | BUNDLED,
  png: validated immutable-by-copy byte buffer
}
```

ready 只表示资源已解析/解码，绝不代表模型已创建或画面可见。配置的 skin 是请求路径；发生回退后，调用者必须使用结果 Skin.path/png，不能重新使用请求路径加载。

### 路径和 PNG 约束

skin 使用相对 assets/avatar 根目录的本地 PNG 路径。本阶段不允许任意绝对路径或网络 URL；其他本地皮肤可放入该目录并设置相对路径。解析做 normalize 和 toRealPath，拒绝 `..` 越界及符号链接/目录链接逃逸，不以工作目录为资源基准。

PNG 不超过 256 KiB，必须有效 PNG 签名、IHDR、64×64，实际 ImageIO 解码成功。先检查尺寸再解码，避免按不受支持的尺寸分配图像。64×32、高清皮肤、远端下载和纹理上传尚不支持。

配置最多 4 KiB，必须包含四个字符串字段，拒绝缺字段、未知字段、重复键、尾随 JSON、不支持的 model/variant。错误或缺失配置使用 Config.defaults() 并记录 configFallback。

### 回退顺序

1. 配置指定 PNG 成功：Source.LOCAL。
2. 指定资源缺失/损坏/越界：尝试 `skins/player_default.png`，Source.DEFAULT_FILE。
3. 默认文件也不可用：读取 jar 内 `/io/mc7dtd/avatar/player_default.png`，Source.BUNDLED、path=null。
4. 连内置资源也缺失/损坏：ready=false、skin=null，返回明确 warnings，不创建模型，不修改同步状态。

内置默认 PNG 位于 `minecraft-mod/src/main/resources/io/mc7dtd/avatar/player_default.png`，打包到 jar，确保项目资源目录缺失时仍有有效默认资源。配置和 PNG 字节均不会发送到 Bridge，也不新增网络 Component 或 revision。

## 修改文件

项目根目录：`D:\wenjian\minecraft\7-M`。

| 文件 | 变更 |
|---|---|
| `assets/avatar/avatar_config.json` | 填写原有空文件为默认配置 |
| `assets/avatar/avatar_config.schema.json` | 新增本地资源配置 Schema |
| `assets/avatar/skins/player_default.png` | 从已有 djsgj.png 原样复制 |
| `assets/avatar/models/.gitkeep` | 新增目录占位，不提供模型 |
| `minecraft-mod/src/main/resources/io/mc7dtd/avatar/player_default.png` | 新增内置回退 PNG，与原图字节相同 |
| `minecraft-mod/src/main/java/io/mc7dtd/AvatarResources.java` | 新增独立资源解析、校验、回退 |
| `minecraft-mod/src/test/java/io/mc7dtd/AvatarResourcesHarness.java` | 新增资源自动测试 |
| `tests/run-phase3_8_4_1.ps1` | 新增构建与回归脚本 |
| `docs/phase3_8_4_1_avatar_resource_setup.md` | 新增本设计更新文档 |

已有 djsgj.png 未修改；已有源码/Renderer 实现不修改，仅增加独立类及资源。

## 验证结果

```text
Config Load PASS
Skin Path Resolve PASS
Missing Skin Fallback PASS
Alex Variant Parse PASS
```

新增资源测试 14 项，既有 Renderer/导航/Goto/Equipment/EVS/Health/Identity/Presentation/NativeProxy 回归 244 项，总计 258 项通过。额外覆盖内置回退、错误/缺失配置、非法变体、classic 标识、重复键、路径越界、URL 拒绝、PNG 字节防外部修改、损坏图片回退。

Minecraft build PASS；本地 Avatar 配置 Schema 验证 PASS。Bridge/7DTD 本轮没有修改，无需重新构建。原有源码和配置哈希复核通过，默认 PNG、内置 PNG 与原图哈希一致，jar 中回退资源存在。

复现：`powershell -File .\tests\run-phase3_8_4_1.ps1`。

## 对 Phase 3.8.4 设计的补充

资源状态与 Avatar 显示状态分离；model/variant 仅作为后续 Avatar Resolver 的输入。该资源管理器提供经过验证的本地 PNG 字节与默认配置，尚不提供 GameProfile 获取、PNG 自动导出、skin hash manifest、动态纹理注册、模型挂点或任何 Unity 加载。

后续实现可以在明确授权后消费本结果，但必须保留 scope/generation 生命周期、主线程显示资源创建以及既有 Renderer 分层约束。本阶段不修复 held_item pitch、不实现模型显示、不进入下一阶段。

完成后停止，等待下一步指令。

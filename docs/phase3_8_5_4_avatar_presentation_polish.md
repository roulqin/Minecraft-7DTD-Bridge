# Phase 3.8.5.4 Avatar Presentation Polish v1

## 范围与分层

仅修改 7DTD 本地 Avatar 表现及资源。Entity Protocol、Bridge、Authority、Equipment Component、Network Revision、7DTD 装备采集／同步均保持不变。

数据流：既有 Entity Transform → 本地 AvatarAnimationState → Animator speed → 已有 Idle/Walk/Run。位置和 Yaw 仍由 AvatarRenderer 更新；Root Motion 关闭，Head Pitch 在 LateUpdate 单独覆盖。没有新增网络状态、动作消息或动画 revision。

## 材质

MC7DTD/AvatarSkin 显式使用 Blend Off、ZWrite On、ZTest LEqual、Cull Back；片元 clip(alpha - 0.5)，保留片元的输出 alpha 恒为 1。基础层队列 2450，外层 2451。两个层均使用 Alpha Cutout；未使用普通 Transparent。保持 Point、Clamp、无 mipmap 与现有外层网格膨胀尺寸。

旧 Shader 本来就没有透明混合，基础层 cutoff=0、外层 cutoff=0.01。仅调整阈值后，实机仍出现头部被天空覆盖，因此没有将初次视觉测试记为通过。缺少 ShadowCaster 通道意味着颜色 ZWrite 正确也不能保证写入相机深度纹理；本次增加独立的 AVATAR_DEPTH（LightMode=ShadowCaster）通道，使用完全相同的 Alpha Cutout，并将颜色通道标记为 ForwardBase。原皮肤的基础头部像素 alpha=255，资源没有被改为透明。

真实引擎像素测试检查半透明输入完全不透明、透明孔洞裁切、后绘制背景的深度遮挡，并读取实际 _CameraDepthTexture 验证保留像素与透明孔洞产生不同深度。技术依据：[Unity 2022.3 Cameras and depth textures](https://docs.unity3d.com/2022.3/Documentation/Manual/SL-CameraDepthTexture.html)。实机画面的修复前后证据另附；没有修改 7DTD 全局后处理或图形设置。

资源构建与 Runtime 使用相同阈值；Runtime 克隆自身材质，不改共享 Prefab。Shader 不支持或不符合预期时回退几何代理，避免紫色材质继续呈现。缺失 PNG 保留 Bundle 内默认皮肤；实体删除时释放自身材质、纹理和本地装备对象，最后一个 Avatar 删除后卸载 Bundle。

## 动画规则

只测量水平 X/Z 位移，单位为既有 Transform 的距离单位／秒。首次采样 idle；速度 ≤0.1 为 idle，0.1<速度<5 为 walking，速度 ≥5 为 running；转换到 Controller speed 分别为 0、0.5、2。

相同位置刷新不重置速度估计；0.85 秒无位移回到 idle。小于 0.05 秒的突发位置变化、单步位移超过 12 或速度超过 40 的跳变作为传送，不触发行走。超过 1 秒的采样间隔使用 1 秒窗口，避免久站后的第一次动作被过度稀释。未增加插值、攻击、跳跃、战斗或动作同步。

## Inspector

**7DTD F1 本地控制台**：`mc7dtd_avatar_inspect [entity_id]` 或别名 `mc7dtd_entity_inspect [entity_id]`，无前导斜线。

输出 model、variant、skin、material、animation（本地目标状态）、animator（Unity 当前状态）、speed、status、reason、render_objects_active。只有根对象、Animator 和 12 个 SkinnedMeshRenderer 及其材质／纹理可用时，render_objects_active 才为 true；此标志确认对象存活，不承诺摄像机当前能看见它。过渡期间 animation 与 animator 可以短暂不同。

Minecraft 的 `/mc7dtd_entity_inspect` 仍检查 Minecraft 本地的反方向代理。没有跨网络搬运 7DTD Renderer 状态，也不将它伪装为 Minecraft 已知信息。

## 可重复验证

自动：`tests/run-phase3_8_5_4.ps1`。Unity Editor 构建资源，Minecraft／Bridge／7DTD 编译，12 个 Minecraft Harness、真实 WebSocket 代理回归、隔离 7DTD 引擎测试。隔离测试不进入世界，自动退出并恢复全局 ModInfo。

人工：`tests/launch-phase3_8_5_4.ps1`，进入两个既有世界。7DTD 观察 Minecraft 来源 Avatar；先站立，再在 Minecraft 普通移动／快速移动，观察 idle→walking→running→idle。抬头低头确认只有头部 Pitch 改变；退出 Minecraft 世界检查消失，重进检查新 Avatar 和默认皮肤。启动器继承项目 work 作为 TEMP/TMP，避免当前机器 Java 临时目录短路径问题。

## 结果与限制

Minecraft 与 7DTD、Bridge 编译通过，7DTD 0 警告／0 错误。Unity 资源验收 25 项、隔离真实引擎测试、Minecraft 回归 261 项、代理／通信回归 26 项的详细结果见最终报告。受保护源码哈希比对无变化。

人工截图和最终结论见对应验收报告。当前 Transform 约 500ms 一次，不实现移动插值；基于采样估算速度无法精确区分游戏原生步态。皮肤仍为当前自定义 player_default.png，非原版 Alex 皮肤；模型比例是 Alex Slim。本地 RightHand 测试适配器保持原状，跨端装备不在本阶段。

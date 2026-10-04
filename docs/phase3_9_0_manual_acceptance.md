# Phase 3.9.0 人工验收步骤

本轮只整理自动验证与验收步骤，未执行新的人工视觉验收。

1. 正常关闭旧三端，确认端口无旧Bridge占用；在项目根执行 `.\tests\launch-phase3_9_0.ps1`。进入两个既有世界，等待连接，7DTD观察者移到Minecraft Avatar附近。
2. 7DTD F1执行 `mc7dtd_avatar_inspect` 获取 **Minecraft Avatar的entity_id**。Minecraft `/mc7dtd_entity_list` 返回的是另一方向7DTD代理ID，不要把它用于此本地装备命令。
3. 确认config/debug.json的debug_navigation=true。在7DTD F1执行 `mc7dtd_avatar_test_held <Minecraft Avatar UUID> 7dtd:iron_axe`。观察木柄灰头斧绑定右手；Inspector应为style=realistic、renderer=7dtd_renderer、socket=right_hand、attached=true。
4. Minecraft保持站立、普通走动、跑动及跳跃／下落；观察装备持续跟随右手，无漂浮。它是本地测试模型，不读取7DTD玩家实际背包中的斧头。
5. 7DTD执行 `mc7dtd_avatar_test_held <UUID> minecraft:diamond_sword`。确认旧斧移除，切换青色体素剑，Inspector为blocky／minecraft_renderer／attached=true。重复走跑跳。
6. 执行同一命令将item参数改为 `other:unknown`，确认黄色Legacy Marker回退；再改为 `null`，确认装备消失、attached=false。
7. 正常退出Minecraft世界，确认7DTD Avatar和装备一并清理。重新进入Minecraft，确认Avatar生成；重新获取UUID、再次执行本地测试命令，确认装备重新绑定。**本地输入不会在重连后自动重放。**

记录斧／剑截图、动画跟随、移除与重绑、Inspector状态。通过标准：两风格可区分、父子挂接正确、走跑跳无脱离、替换与清理正常。无攻击、Inventory或跨端装备同步验收。

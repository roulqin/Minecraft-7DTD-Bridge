# Phase 3.8.0 — Equipment Component v1 设计报告

日期：2026-10-04（Asia/Shanghai）。用户确认 Phase 3.7.4 已通过后，本轮完成 Equipment 设计阶段。

## 结果

Equipment Component v1 Design PASS。

完整契约见 `Equipment_Component_v1.md`。选择 7DTD→Minecraft 的本地玩家装备观察，五个逻辑槽位 head/body/hands/feet/held_item；每槽为来源 item_id 对象或 null。共享现有 sidecar revision，完整装备快照必须含全部槽，局部 patch 保留其它槽；组件 null 与槽位 null 的含义分开。装备 patch/remove 不影响 Health、Identity 和独立 Presentation；共享 snapshot 的全量覆盖语义保持。

旧接收器不支持 equipment，因此选定未来三端协调升级，不进行未知组件过滤或隐式握手修改。当前运行时仍未接入 Equipment，示例不可发往当前服务。权限配置提案未部署，线上 schema 原样保留。

## 文件清单

新增两个文档、两个草案 schema、八个消息示例和一个权限提案、一个离线 Oracle、一个测试文件、一个 PowerShell 验证入口，共 16 个工程交付文件。设计生成辅助脚本和测试证据保存在忽略的 `work/phase3_8_0-test/`。

## 验证

`tests/run-phase3_8_0.ps1` 最终退出码0：30组测试通过；67份受保护文件哈希不变。包括组件生命周期、slot partial update、旧组件保留、共享 revision 冲突、权限拒绝、错误 stream/entity_sequence、清理、旧消息兼容及原始 JSON 限制。

初次离线执行发现既有 v2 schema 无 `$id`，测试资源注册改为显式使用其既有 canonical URI，生产 schema 未修改。修正后完整脚本通过。

没有修改运行源码、在线协议/schema 或配置，没有编译/部署新产物，没有操作或重启游戏；没有进入 Phase 3.8.1。公开装备 API、槽位实际映射及真实物品 ID 仍待后续核查。本轮仅确认设计契约可离线验证，不声称运行装备同步已实现。

# Test Plan — Phase 3.9.2.0

状态：未来实施测试计划，以下用例均为 **PLANNED／未执行**，不得解读为 Runtime PASS。

Phase 3.9.1 已有自动验证基线：7DTD 310项（含139项 Expansion）、Minecraft 261项、Transport 26项，Unity资源26项。这里引用历史报告，不在设计阶段重新编译或运行。后续实施应先复跑对应基线，再添加下列用例。

## 自动测试用例

纯布局／分类使用可重复单元测试；GameObject、骨骼、资源、Animator 使用 Unity 引擎测试。模拟 Provider 不能替代真实对象与材质验证。

### Slot / Occupancy

| ID | 用例及通过条件 | 状态 |
|---|---|---|
| EQX-001 | 六槽空布局：固定六键均为空 | PLANNED |
| EQX-002 | 六槽独立 Create：每条装备只生成一个主槽 | PLANNED |
| EQX-003 | 单槽 Replace：其余五槽布局不变 | PLANNED |
| EQX-004 | 单槽 Remove：释放对应 owner | PLANNED |
| EQX-005 | held_item 默认 right_hand；事实 hands 不当副手 | PLANNED |
| EQX-006 | 双手占用两槽且仅一个视觉条目 | PLANNED |
| EQX-007 | 盾牌默认左手 | PLANNED |
| EQX-008 | 左右双持互不覆盖 | PLANNED |
| EQX-009 | 背部收纳武器不占手部 | PLANNED |
| EQX-010 | 双手与盾冲突按固定优先级处理 | PLANNED |
| EQX-011 | 同优先级按 entry_key 决胜 | PLANNED |
| EQX-012 | 输入所有排列得到相同布局 | PLANNED |
| EQX-013 | 重复输入布局幂等 | PLANNED |
| EQX-014 | 双手取得失败不残留单槽 claim | PLANNED |
| EQX-015 | TryEquip 默认冲突拒绝并保留旧输入 | PLANNED |
| EQX-016 | 显式 ReplaceConflicts 删除完整旧条目 | PLANNED |
| EQX-017 | 清空 reservation 返回 owner 提示 | PLANNED |
| EQX-018 | Remove owner 同时释放双手 | PLANNED |
| EQX-019 | 只读 winner 移除后 suppressed 请求恢复 | PLANNED |
| EQX-020 | 已被本地替换删除的装备不复活 | PLANNED |
| EQX-021 | 重复 entry_key 拒绝批次 | PLANNED |
| EQX-022 | 未知物品保守单槽 fallback | PLANNED |
| EQX-023 | 非法槽不影响其他有效条目 | PLANNED |
| EQX-024 | Remove／Clear 重复执行安全 | PLANNED |

### Socket

| ID | 用例及通过条件 | 状态 |
|---|---|---|
| EQX-025 | 六 Socket 精确解析预期内部骨骼 | PLANNED |
| EQX-026 | 局部 offset 与预期世界位置一致 | PLANNED |
| EQX-027 | rotation_offset 顺序与握持方向一致 | PLANNED |
| EQX-028 | 正 scale 正确应用 | PLANNED |
| EQX-029 | NaN／Infinity／越界 scale 被拒绝 | PLANNED |
| EQX-030 | 缺骨骼不可浮空创建 | PLANNED |
| EQX-031 | 重名骨骼不选择首个匹配 | PLANNED |
| EQX-032 | 左手 mirror 使用验证资源且法线正常 | PLANNED |
| EQX-033 | 缺镜像资源安全降级 | PLANNED |
| EQX-034 | Socket 不修改 Entity／Motion Root | PLANNED |
| EQX-035 | head 装备跟随 Head Pitch | PLANNED |
| EQX-036 | 新 Avatar 不复用旧骨骼引用 | PLANNED |

### Renderer

| ID | 用例及通过条件 | 状态 |
|---|---|---|
| EQX-037 | Minecraft Style 创建 Unity 风格对象 | PLANNED |
| EQX-038 | Voxel Style 创建方块模型 | PLANNED |
| EQX-039 | Realistic Style 创建独立 Mesh | PLANNED |
| EQX-040 | 同 item 显式 style 切换而非前缀推断 | PLANNED |
| EQX-041 | 未知模型 Legacy Marker fallback | PLANNED |
| EQX-042 | Renderer 创建异常 fallback | PLANNED |
| EQX-043 | fallback 失败为 unavailable 且同步继续 | PLANNED |
| EQX-044 | 替换隐藏旧对象无残影 | PLANNED |
| EQX-045 | 资源 Prepare 失败清理候选 | PLANNED |
| EQX-046 | 本地事务失败保留旧对象 | PLANNED |
| EQX-047 | 事实替换失败不显示过期旧装备 | PLANNED |
| EQX-048 | Update 不无故重建对象 | PLANNED |
| EQX-049 | 六槽并存对象数量正确 | PLANNED |
| EQX-050 | 双手 reservation 不生成第二对象 | PLANNED |
| EQX-051 | 两个 Avatar 共享资源时清理互不影响 | PLANNED |
| EQX-052 | 私有 Mesh／Material 释放且共享资源存活 | PLANNED |

### Animation

| ID | 用例及通过条件 | 状态 |
|---|---|---|
| EQX-053 | empty 恢复自然 Idle／原 Clips | PLANNED |
| EQX-054 | 右手单持只约束对应手臂 | PLANNED |
| EQX-055 | 左手单持 Profile 正确 | PLANNED |
| EQX-056 | 双持 Profile 正确 | PLANNED |
| EQX-057 | 双手 Profile 与占用一致 | PLANNED |
| EQX-058 | 盾牌＋主手武器 Profile 正确 | PLANNED |
| EQX-059 | Walk 持物兼容且腿部不变 | PLANNED |
| EQX-060 | Run 减摆且运动速度推导不变 | PLANNED |
| EQX-061 | Jump 中换装不重启 Jump | PLANNED |
| EQX-062 | Fall→Land 恢复无旧 Pose | PLANNED |
| EQX-063 | Head Pitch 不被覆盖 | PLANNED |
| EQX-064 | 只移除背部不改变持物 Profile | PLANNED |
| EQX-065 | 缺 Clip 回退并报告原因 | PLANNED |
| EQX-066 | Controller 清理恢复且不影响另一个 Avatar | PLANNED |

### Lifecycle / Regression

| ID | 用例及通过条件 | 状态 |
|---|---|---|
| EQX-067 | Spawn 骨骼就绪后绑定一次 | PLANNED |
| EQX-068 | Remove 清理 owner／reservation／对象 | PLANNED |
| EQX-069 | Despawn 全槽清理 | PLANNED |
| EQX-070 | Disconnect 清理缓存与 Override | PLANNED |
| EQX-071 | World Change 旧回调不能挂到新 Avatar | PLANNED |
| EQX-072 | Reconnect 新对象正确重绑无重复 | PLANNED |
| EQX-073 | 未知输入与明确 null 区分 | PLANNED |
| EQX-074 | 无装备输入保持六槽空 | PLANNED |
| EQX-075 | Inspector 空槽／reserved／fallback／unavailable 区分 | PLANNED |
| EQX-076 | Entity／Authority／Presentation／Equipment 原测试保持通过 | PLANNED |
| EQX-077 | Motion Interpolation／Head Pitch／Action 原测试保持通过 | PLANNED |
| EQX-078 | 网络消息与 revision 未改变，Bridge／采集器受保护文件未修改 | PLANNED |

## 人工验收矩阵

| 场景 | 可观察通过条件 |
|---|---|
| 空手 | Idle 自然，无张臂，六槽 Inspector null |
| 右手木棒／火把／枪 | 正确握持；换装无残影；实际 Renderer 与 Inspector 一致 |
| 左手盾牌／双持 | 左右分离；左右动画正确；镜像没有反面或 UV 错误 |
| 双手步枪＋盾牌冲突 | 只显示胜出的装备；副手 reservation；本地默认拒绝与显式替换分别验证 |
| 头／背／身体／腰同时存在 | 绑定正确，无独立漂浮；刚性身体装备按已声明边界观察穿模 |
| Walk／Run／连续 Jump／Land | 装备跟随，落地恢复，无旧 Pose；Head Pitch 独立 |
| 三风格切换 | Minecraft／Voxel／Realistic 实际不同，未知物品安全 Marker |
| 退出／重进／换世界 | 旧对象全部消失，新骨骼重绑无重复；测试装备重新输入后正常生成 |

不要求人工复制 UUID；后续验收沿用既有 debug entity list／goto／inspector 辅助定位。7DTD Avatar 与 Minecraft 端的 7DTD代理是相反方向，必须观察 7DTD 内的 Minecraft Avatar。不得用 Minecraft ItemDisplay 成功作为 Unity Avatar 装备成功证据。

## 证据与完成标准

未来新增自动计划共 78 项。通过需记录测试运行器结果及失败原因；PLANNED 只有执行后才能改 PASS／FAIL。

截图至少包括空手、左右手／双手、六槽并存、三风格、冲突 Inspector、落地、重连。日志记录 entity_id、本地 session、slot、owner、requested／actual Renderer、骨骼、Profile、fallback reason，避免每帧刷屏。

人工必须确认实际对象和动画；单凭 Inspector attached=true 不算视觉通过。多次换装与 Despawn 后核对对象数、私有资源数回到基线；避免只检查消息存在。

禁止通过改变协议、Authority、Component revision、Bridge 或采集器来让测试通过。此设计不包含 Combat、Inventory、攻击／伤害或跨端装备字段。

## 当前设计交付验证

仅验证五份文档、JSON示例、相互链接与只读保护哈希。不会执行上表 Runtime 用例，不启动下一阶段。

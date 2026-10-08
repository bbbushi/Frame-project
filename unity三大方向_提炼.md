# Unity 学习路线图（内容提炼）

> 原文档：111.md —— 面向"小团队技术负责人"视角的 Unity 三大方向学习规划

---

## 方向一：UGUI 组件化布局 + 动态 Prefab 生成器

**核心目标**：让 UI 布局像网页一样灵活，并能像"发消息"一样生成 Prefab。

- **UI 布局网页化**
  - RectTransform + Layout Group（Horizontal / Vertical / Grid）+ Content Size Fitter + Aspect Ratio Fitter 模拟 Flexbox
  - CanvasScaler 的 Scale With Screen Size + Match Width/Height 实现多分辨率适配
  - 自定义 LayoutElement 脚本实现类似 CSS 的 margin / padding
  - 典型示例：ScrollRect + VerticalLayoutGroup 构成消息列表，每条消息为动态生成的 Prefab
- **Prefab 生成编辑器（Editor Window）**
  - EditorWindow + `HandleUtility.GUIPointToWorldRay` 射线检测，在 Scene 视图点击位置生成 Prefab
  - 代码放 Editor 文件夹下，`[MenuItem("Tools/Spawn Tool")]` 添加菜单入口
- **运行时动态生成**
  - 全局事件系统（EventManager）+ SpawnCommand，或统一的 `GameObjectSpawner.Spawn(string prefabId, Vector3 position)`
  - 价值：快速搭建关卡动态障碍物、奖励、敌人

---

## 方向二：动态生物组件系统（建议最先做）

**核心目标**：一套可动态附加到任意 GameObject 的"自动行动组件"。

- **周期性创建/销毁组件**
  - `gameObject.AddComponent<T>()` + `Destroy(component, delay)`
  - 示例：生物每隔 5 秒获得限时护盾组件，10 秒后消失
- **轻量状态机（SimpleStateMachine）**
  - 不依赖 Animator Controller，用 `Animation.Play()` 或 `Animator.Play(stateName, layer, normalizedTime)`
  - stateTimer 倒计时归零后按随机权重切换状态（如 40% idle / 30% walk / 30% attack）
  - stateTimer 设为 AnimationClip.length 或随机时长
- **随机行为**
  - 随机音效：`AudioSource.PlayOneShot(audioClips[Random.Range(0, length)])`
  - 点击 NPC 状态机：点击时根据当前状态产生不同反应（生气 / 说话 / 给道具）
  - 简化版行为树：随机种子 + 环境变量（如是否被玩家注视）决定下一步动作
- **价值**：快速制作"会呼吸的生物群系"，适合模拟、沙盒、回合制怪物

---

## 方向三：领域类库（5 个子方向，按兴趣选一）

| 子方向 | 核心内容 | 适合 |
|---|---|---|
| 3.1 桌游卡牌 | Card（费用/伤害/科技解锁）/ Piece（位置/范围/血量）数据结构；ScriptableObject 存科技节点 + Tooltip 显示 Tips；协程倒计时限时回合；监听"生命≤0 / 卡组抽空"事件触发胜负结算 | 策略游戏 |
| 3.2 存档与联网 | EasySave3 本地存档；UnityWebRequest + Newtonsoft.Json / JsonUtility 服务器请求；Photon PUN 2 / Mirror 实时对战（连房间 → 实例化角色 → 同步 Transform/动画）；抽象 `IDataService` 接口切换本地/网络存储 | 联网手游 |
| 3.3 背包系统 | 多层嵌套背包（BagSlot 内可持 Bag 子对象）；ScriptableObject 条件列表 + 运行时 Evaluate（如"等级≥5 AND 生命<50%"）；斩杀线 UI（法力足够高亮、不足置灰）；分支面板淡出/滑入动画 | RPG / 生存 |
| 3.4 特殊机制 | 火车多节：HingeJoint 或自定义 Trailer 保持距离跟随；链条伤害：搜索带 ChainTag 的单位传递伤害，最多 X 次；重复 Dot：List\<DotEffect\> 每 0.5 秒 Tick 结算并移除过期；进化阶段：EvolutionComponent 达条件后 EvolveToNextStage 替换模型/技能/属性 | 独特机制 |
| 3.5 镜头系统 | Post-Processing Stack v2 的 DOF/Bloom 模糊；墙体靠近时改 renderQueue 或 Shader Clip 平滑透明；正交相机 45° 俯视 + 鼠标边缘移动（RTS 模式）；Lerp 平滑跟随（第三人称模式）；CameraManager 统一管理双模式切换 | 电影化体验 |

---

## 学习顺序建议

1. **先做方向二**（周期组件 + 简单状态机）→ 快速获得"游戏感"，成就感强
2. **再做方向一**（UI 工具 + 动态生成 Prefab）→ 有了动态生物后立刻提升开发效率
3. **最后选一个 3.x 子方向**深入，每个子课题约需 2-4 周精通

**核心方法论**：为项目建立"自定义类库"文件夹，将 `StateMachine`、`Spawner`、`CameraSwitcher` 等写成可复用模块，后续开发效率翻倍。

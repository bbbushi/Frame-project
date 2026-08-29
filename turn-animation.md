# 转身动画优化方案（turn animation）

> 生成: 2026-08-29 · 状态: **方案已获批，代码已实现，资产侧待手工操作**
> 配套文档: [CLAUDE.md](CLAUDE.md)（核心架构）· [state-machine.md](state-machine.md)（双层 FSM）· [bug-list.md](bug-list.md)

## 1. 问题与目标

当前玩家反向移动的表现是"瞬间翻转 localScale + 锁移动 0.5s"，生硬且动画接线完全断开：

- `SetTrigger("flip")` 在 `shina.controller` 中**无对应参数**（trigger 无人消费，静默无效）
- FX 层进入 `Turnflip` 状态的转换是**无条件转换**（非 trigger 驱动）——接线 bug，会导致 FX 层在 Empty↔Turnflip 间无条件震荡
- `LocomotionComponent.ApplyHorizontal` 的刹车分支（反向输入时速度按 `deceleration` 渐变过零）因移动被 `AddIgnore(Move, 0.5s)` 锁住而**成死代码**，与注释宣称的"速度过零才翻转"自相矛盾

**已确认的设计决策**（2026-08-29 与作者确认）：

1. **每次反向输入都播转身动画**（地面）——重量感取向（武士零风格）
2. **视觉+手感一起修**——删 0.5s 硬锁，转身时序由物理驱动（速度过零时翻转），激活刹车死代码
3. **空中反向瞬时转身**——只翻 localScale、不播动画、不锁移动

**核心设计**：转身不再是"动作锁"，而是**移动层内的物理过渡**——反向输入由 `ApplyHorizontal` 刹车分支自然减速，速度过零的那一帧执行翻转并播 Turnflip 涟漪；急停过程的表现（run→walk→idle）由 GroundMove 混合树按 xvelocity 天然呈现，零新增逻辑、零新增状态。

## 2. 参考来源说明

| 参考游戏 | 具体机制 | 借鉴的设计 | 来源 | 性质 |
|---|---|---|---|---|
| 蔚蓝 Celeste | `Player.cs` 的 `RunTurnaround` 状态：反向急停时短暂减速并播 skid/turnaround 动画，之后回 Run | 转身是移动层内的**短暂物理过渡**（减速+动画叠加），不是动作锁；操作不被阻塞 | [Celeste 源码 Player.cs](https://github.com/NoelFB/Celeste/blob/master/Source/Player/Player.cs)（源码公开+含 RunTurnaround 已经搜索确认；行级细节受网络限制未能直读） | 借鉴思路 |
| 蔚蓝 Celeste | 源码发布报道与物理常量设计（RunDecceleration/SkidDecel 等） | 刹车率独立配置、转身时长由物理常量决定 | [Game Developer 报道](https://www.gamedeveloper.com/programming/-i-celeste-i-devs-release-player-movement-code-for-the-tough-as-pitons-platformer)、[Celeste and TowerFall Physics](https://maddythorson.medium.com/celeste-and-towerfall-physics-d24bd2ae0fc5)（搜索摘要级确认） | 借鉴思路 |
| Mario / Pizza Tower | skid 速度门控与 skid 动画 | 刹车期间的可视化（姿态渐变+专用涟漪）是重量感来源——本方案取"每次都播"变体 | [TV Tropes: Instant 180 Degree Turn](https://tvtropes.org/pmwiki/pmwiki.php/Main/Instant180DegreeTurn)、[GameMaker 论坛 skid 讨论](https://forum.gamemaker.io/index.php?threads/pizza-tower-mario-skid-when-turning.109562/) | 借鉴思路 |
| （社区实践） | facing 与 velocity 解耦 | 本项目 `FacingDirection` 与位移已解耦，扩展为"翻转时机跟随速度符号" | [Unity Discussions: direction change for 2.5D](https://discussions.unity.com/t/best-way-to-handle-character-animation-direction-change-for-a-2-5d-game/750098) | 借鉴思路 |
| 蔚蓝 Celeste | 手感哲学：宽容/响应优先 | 支持空中转身瞬时、地面转身不锁操作的决策 | [Celeste & Forgiveness](https://maddythorson.medium.com/celeste-forgiveness-31e4a40399f1)（已直读全文：10 项输入宽容机制） | 借鉴思路 |

**基于设计常识的推测**（未经搜索确认）："武士零/空洞骑士空中转向接近瞬时"为平台动作游戏普遍惯例；Turnflip 播放速率建议 1.5× 为调校常识。

**本项目既有设计意图的兑现**（非外部参考）：`LocomationComponent.cs` ApplyHorizontal 注释"翻转时机由上层按速度归零触发"与 `PlayerAnimatorComponent.cs` 类头注释"flip trigger 归 FX 事件层"——本方案就是把这两条已写下但未接通的设计接通。

## 3. 架构契合分析

| 设计点 | 落点 | 理由 |
|---|---|---|
| 刹车物理（速度渐变过零） | `LocomotionComponent.ApplyHorizontal` 既有刹车分支 | 激活死代码，通用层能力不变 |
| 转身判定（空中瞬时/地面过零+动画） | `PlayerLocomotion.SetMoveInput` | 玩家手感设计归 PlayerModule；该函数在正常/子弹时间两种输入模式下均每帧被调（PlayerInputController.cs），天然承载每帧判定 |
| Turnflip 动画 | FX 事件层 trigger（`flip`） | 符合动画三分法"瞬时涟漪归 FX 层"，与 Hit/Landing 同模式 |
| 翻转执行 | `LocomotionComponent.Flip()`（既有） | 不新增视觉路径 |
| 受击互斥 | 复用 `IsIgnore(ActionIgnoreTag.Move)` 守卫 | 受击 `AddIgnore(All)` 含 Move 位，受击硬直期间转身判定静默跳过，避免与 hit 涟漪打架 |
| 配置 | **不新增字段** | 刹车时长由既有 `EntityControllerConfig.deceleration` 决定（≈ moveSpeed÷deceleration） |

**不新增状态的理由**：移动层哲学是"只按物理事实分 Ground/Air"（Idle/Move 均非独立状态）。转身是速度符号变化过程，由物理与混合树自然承载，设 TurnState 会破坏该哲学并引入让位/恢复复杂度。

**删除 0.5s 硬锁的影响**：`ActionIgnoreTag.Move` 的唯一写入点消失，`MotionState.Update` 的 `IsIgnore(Move)` 判断保留（受击走 All 仍需要它）。转身期间**不锁跳跃/攻击/突刺**——转身只是物理过渡。

**与其他机制的关系**：
- **BulletTime**：`SetMoveInput` 在子弹时间模式照常每帧调用；timeScale 0.2 下速度过零变慢、Turnflip 同步变慢，表现自洽，无需特判
- **处决链**：`PlayerBulletTime` ExecuteChain 中的直接 `Flip()`（瞬移翻转）保持现状——瞬移场景瞬时翻转合理
- **帧序**：输入驱动（GameManager IUpdatable）与移动驱动（Player.Update）先后未定脚本顺序，判定与物理最多错开一帧；刹车全程约 0.125s（≈7-8 帧），一帧偏差不可感知

## 4. 实现内容

### 4.1 代码（已完成）

`Assets/Script/Modules/PlayerLocomotion.cs` — `SetMoveInput` 重写：

```csharp
public void SetMoveInput(float input)
{
    HorizontalInput = input;
    if (input == 0f) return;
    LocomotionComponent loco = Players.locomotionComponent;
    if (input * loco.FacingDirection >= 0f) return;      // 同向/静止朝向，无需处理
    if (Players.actionIgnoreComponent.IsIgnore(ActionIgnoreTag.Move)) return;  // 受击硬直让位

    // 空中：瞬时转身，不播动画（空中操控响应优先）
    if (!loco.IsGrounded)
    {
        loco.Flip();
        return;
    }

    // 地面：速度过零（或本就静止）才翻转——转身时序由物理驱动；
    // -0.01f 容差防浮点抖动；静止反向时条件立即成立，原地转身即时发生
    if (input * Players.rb.velocity.x >= -0.01f)
    {
        loco.Flip();
        Players.anim?.SetTrigger("flip");   // Turnflip 涟漪归 FX 事件层（同 Hit/Landing 模式）
    }
}
```

### 4.2 地面转身全流程（时序）

```
反向输入 ──► 刹车期：vx 渐减，GroundMove 树 run→walk→idle 连续渐变（树白拿）
        │    （MotionState.Update 无条件 ApplyHorizontal，刹车分支生效）
        ▼
速度过零帧（input·vx ≥ -0.01f 成立）
        ├──► loco.Flip()（localScale 翻转）
        └──► SetTrigger("flip") → FX: Empty→Turnflip（播 0.43~0.65s，exit 0.9 回 Empty，权重自治）
        ▼
反向加速：树 idle→walk→run 连续渐变；期间跳跃/攻击/突刺随时可插入（不锁）
```

分支行为：
- **静止反向**：过零条件立即成立 → 即时翻转+播 Turnflip
- **空中反向**：立即 `Flip()`，无动画无等待
- **刹车中途回原方向/松手**：不翻转无动画（速度自然回原方向）
- **受击硬直中反向**：`IsIgnore(Move)` 守卫拦截；硬直结束后下一帧正常判定
- **搓招（快速左右交替）**：每次完整过零才翻转，flip trigger 重触发 FX 层重进 Turnflip

### 4.3 资产侧手工操作（Unity 编辑器，待完成）

> 修复顺序：先资产后测试（资产不修，代码修完动画仍乱）。

1. **添加参数**：`Assets/anim/shia/shina.controller` → Parameters 面板新增 **Trigger** 类型参数 `flip`
2. **修 FX 层 Empty→Turnflip 转换**（当前接线 bug 根因）：该转换现为无条件+无 exit time；给它加上条件 `flip` trigger（与 Landing/Hit 的转换同型）
3. **保留** Turnflip→Empty 的 exit time 0.9 转换（播完 90% 自治回位，与 landing/hit 同模式）
4. **可选微调**：Turnflip 状态 Inspector 的 Speed 设 1.5（0.65s→约 0.43s，更利落）；`shaia_Turnflip.anim` 的 Loop Time 可取消（有 exit time 兜底，不改亦可）
5. **无需**给剪辑加动画事件（FSM 正确性不依赖动画事件——架构原则）

### 4.4 参数表

| 参数 | 类型 | 默认值 | 所在 | 说明 |
|---|---|---|---|---|
| `deceleration` | float | 40 | `EntityControllerConfig` → `LocomotionComponent` | 刹车率，决定转身等待时长 ≈ moveSpeed÷deceleration（想更厚重调低，如 15 → 0.33s） |
| 过零容差 | float | -0.01f | `PlayerLocomotion`（内联常量） | 速度符号判定容差 |
| Turnflip Speed | float | 建议 1.5 | shina.controller Turnflip 状态 | 动画播放速率（资产侧） |

## 5. 风险与权衡

1. **Turnflip 时长（0.65s）与刹车物理时长（~0.125s）不匹配**：动画比过程长约 5 倍。FX 叠加不阻塞操作，不卡手，但视觉上"人已反跑、转身动画还在播"。缓解：状态 Speed 1.5~2×；彻底解法是美术补一版短转身剪辑（后续美术任务）。
2. **手感变化**：转身总时长从固定 0.5s 硬锁变为 ~0.125s 速度过零（由 `deceleration` 决定，可调）。
3. **受击击退与移动驱动的既有打架**（ForceMove 在 FixedUpdate 设 velocity，ApplyHorizontal 在 Update 覆盖 vx）：现状遗留、非本方案引入；受击期间有 `AddIgnore(All)` 锁住 ApplyHorizontal，转身判定亦有守卫，不加剧。
4. **否决的替代方案**：
   - 转身设为动作层 TurnState（可锁攻击/跳跃）——违反"移动层只认物理事实"哲学，且与"物理驱动、不锁"决策相悖
   - 保留 0.5s 硬锁只修视觉——刹车分支永远是死代码，注释矛盾无法消除
   - 程序化补间（scale 动画）替代剪辑——引入另一套动画路径，与 FX 层既有体系重复

## 6. 验证清单（Unity 编辑器）

1. 完成 4.3 资产操作后运行场景，逐项测试：
   - 静止按反向 → 立即翻转 + Turnflip 播一遍自治回 Empty
   - 全速跑动按反向 → 刹车渐变（run→walk→idle）→ 过零瞬间翻转 + Turnflip → 反向加速
   - 空中反向 → 立即翻转，无 Turnflip、无迟滞
   - 刹车中途松手/回原方向 → 不翻转、无动画、速度自然恢复
   - 转身期间按跳/攻击/突刺 → 立即响应（无锁）
   - 受击硬直期间按反向 → 不翻转不播动画（守卫生效）
   - 子弹时间中反向 → 流程正常、整体随 timeScale 变慢
   - **回归检查**：无操作静立时 FX 层不再出现 Turnflip 无故循环（无条件转换 bug 消失）；Landing/Hit 涟漪不受影响
2. 可选：打开 `enableTransitionLog` 观察两台状态机无异常转换

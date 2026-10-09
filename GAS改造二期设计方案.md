# GAS 改造二期设计方案（GameplayEffect：效果系统 + 防御力）

> 分工延续：本文档给出全部设计与成员级骨架，代码由你手写、资产由你手建、验证由你自跑。
> 所有 `文件:行号` 引用已对当前代码核实（2026-10-08，一期收尾 3 处修正之后）。
>
> 承接一期（Attribute 化）路标 §十.1：**GameplayEffect SO（Modifier 列表 + Duration/Period）→ ApplyEffect；第一个消费者 = 防御力**。一期预埋的语义（"Instant 改 Current；Duration 改 Current、到期回 Base"）在本期兑现。

---

## 一、目标与范围

**一句话**：把「一段时间内 +50 防御」「每秒掉 5 血」这类效果做成配置资产（SO），全部效果最终走一期那条属性修改管线——不新增第四条修改路径。

| 本期做 | 本期不做（留后续） |
|---|---|
| `GameplayEffect` SO：Instant / Duration（+ Period 周期触发） | Modifier 乘法/覆盖/优先级通道（Additive 先跑通） |
| `ActiveEffect` 运行时实例：应用 → 计时 → 到期按记录 delta 回退 | 效果堆叠规则（同效果叠加刷新时长/层数） |
| `AttributeType.Defense` 防御属性 + 减伤公式接入结算 | GameplayTag（单机 enum 够用，一期决策 §九.7 延续） |
| `ApplyEffect` 入口（AttributeSet 持有，宿主转发） | 敌人主动给玩家上 debuff（本期用调试菜单验证链路） |
| `EntityCharacterConfig` 加 `defense` 字段（现有 .asset 零手术） | buff 图标/剩余时间 UI |
| `Effect` 冒烟测试菜单（纯 C#，非 Play 可跑） | 效果视觉表现（闪红/粒子——Cue 三期） |
| 铁壁/中毒两个示例资产 + 调试菜单 | |

**验收标准**：Debug 菜单给玩家挂「铁壁」（Defense+50 持续 5 秒）→ 再点「玩家受伤10点」→ 飘字显示 **7**（10×100/150，自动显示减免后的实际值，一期飘字就用 delta）；5 秒后防御回 0，再受伤恢复飘 10。挂「中毒」→ 每秒飘绿字/掉血、3 秒停、能把人毒死、死人后毒停止。

---

## 二、现状事实（设计依据，已逐条核实）

### 2.1 一期成果（本期的地基）

- `AttributeSet`（Assets/Attribute/Core/AttributeSet.cs）：三条修改路径 `Modify/SetCurrent/SetBase` 全走 `Apply` 管线（clamp → 判变 → 发事件 → 死亡标记）；`Changed` 实例事件 + `AnyChanged` 静态总线
- 宿主 `HealthManageComponent`：`Set` 属性、`ApplyDamage/ApplyHeal`、`AttackPower` 读路径、Init 里 Define 三属性
- 飘字订阅 `AnyChanged`，只认 `reason ∈ {Damage, Heal}`、显示 `|delta|`、伤害白/治疗绿
- 冒烟测试 9 条全绿（`Attributes/冒烟测试（纯逻辑）`），本期往旁边加一个新菜单不动它

### 2.2 防御力的空缺

- `AttributeType` 只有 `Health / MaxHealth / AttackPower`（Attribute.cs:5-11）
- `DamageComponent.Hit` 把 `damage.damage` **原值**直传 `ApplyDamage`（DamageComponent.cs:44）——没有减免环节
- `EntityCharacterConfig` 只有两个字段：`maxHealth` / `attackDamage`（EntityCharacterConfig.cs:9-10）

### 2.3 ⚠️ 关键事实：Entity 没有 RefreshUpdate 驱动（本期计时挂哪）

- `EntityComponent.RefreshUpdate()` 虚方法存在（EntityComponent.cs:38）但 **Entity 从不调用它**——全项目唯一驱动链是 `Entity.FixedUpdate`（Entity.cs:120-141）逐个调 `detection/locomotion/damageComponent.RefreshFixedUpdate()`
- **结论**：效果计时挂 `HealthManageComponent.RefreshFixedUpdate` override，时间源用 `FrameInterval`（EntityComponent.cs:35 → `Owner.FrameInterval`，带实体自己的 TimeScale）——子弹时间里 buff 同步变慢，语义正确；Owner 为 null 时自动退化全局时间（EntityComponent.cs:31-35 的降级已写好）
- 附带推论：池化敌人回池后 GameObject inactive → FixedUpdate 停 → 效果计时自然暂停；复活走 `Revive()` 清效果（§八.7），暂停问题不复存在

### 2.4 死亡免疫已就位（毒打死人的边界是现成的）

`Modify` 对 Health 负增量在 `_diedField` 时直接拒绝（AttributeSet.cs:41）——中毒 tick 可以把活人毒死（跨 0 线置位），死人后毒自动失效（免疫拒绝后续 tick），**零新代码**。

---

## 三、总体架构

### 3.1 数据流（效果生命周期）

```
GameplayEffect.asset（SO 配置：modifiers + duration/period）
        │ ApplyEffect(effect, ctx)   ← 调试菜单/（将来）技能/敌人
        ▼
AttributeSet._effects（ActiveEffect 运行时列表）
        │ 立即：逐 modifier 走 Modify()（记录实际生效 delta，含 clamp 后的值）
        ▼
RefreshFixedUpdate → TickEffects(FrameInterval)   ← 宿主转发，Entity.FixedUpdate 驱动
        │ Duration：remaining 倒计时 → 到 0：按记录 delta 逆运算回退 → 移除
        │ Period：periodTimer 累计 → 每到点：periodModifiers 即时生效（不回退）
        ▼
所有数值变化 ≡ Modify() ⇒ 一期管线自动接管：clamp/判变/事件/总线/飘字/死亡标记
```

### 3.2 文件总览

```
新增（3 文件）：
├── Assets/Attribute/Effects/GameplayEffect.cs   SO：DurationType/duration/period/modifiers/periodModifiers
├── Assets/Attribute/Effects/ActiveEffect.cs     运行时实例：应用/计时/回退（纯 C#）
└── Assets/Attribute/Editor/EffectSmokeTest.cs   冒烟菜单（非 Play 可跑）

修改（6 处，每处 1~6 行）：
├── Attribute/Core/Attribute.cs:9        AttributeType + Defense（1 行）
├── Attribute/Core/AttributeChange.cs:6  ChangeReason + Effect（1 行）
├── Attribute/Core/AttributeSet.cs       +_effects 列表 / ApplyEffect / TickEffects / ClearEffects；Revive 里清效果
├── Component/HealthManageComponent.cs   Init Define Defense、RefreshFixedUpdate 转发、ApplyEffect/Defense 读路径
├── Config/EntityCharacterConfig.cs:10   + defense 字段（1 行，现有 .asset 自动补 0）
├── Component/DamageComponent.cs:43      ApplyDamage 前减伤公式（2 行）
└── UISystem/Editor/UIDebugMenu.cs:73 后  铁壁/中毒调试菜单（~15 行）

零改动：Entity.cs、Enemy.cs、prefab 全部、HudPanel/FloatingTextManager（事件语义自动兼容）
```

---

## 四、逐文件规格（成员级骨架）

### 4.1 `Attribute.cs`——枚举加一个值

```csharp
public enum AttributeType
{
    Health,
    MaxHealth,
    AttackPower,
    Defense,        // 二期：减伤，见 DamageComponent 公式；Duration 效果可临时抬升
}
```

### 4.2 `AttributeChange.cs:6`——原因枚举加一个值

```csharp
public enum ChangeReason { Init, Damage, Heal, Revive, Debug, Set, Effect }
```
枚举**追加**不改序——现有白名单过滤（飘字只认 Damage/Heal）零影响；`Effect` 不在飘字白名单里 = buff 起效/回退不飘字，中毒每跳走 `Effect` 也不飘（想飘毒字将来加过滤即可，见 §九.8）。

### 4.3 `Effects/GameplayEffect.cs`（SO 配置）

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;
namespace Attributes
{
    /// <summary>持续语义：Instant=modifiers 一次性生效完事；Duration=modifiers 持续，到期回退，
    /// 期间每 period 秒 periodModifiers 即时生效一次（period<=0 则不周期触发）。</summary>
    public enum EffectDurationType { Instant, Duration }

    [Serializable]
    public struct Modifier
    {
        public AttributeType type;
        public float magnitude;     // 正=增益 负=减损；Additive 语义，二期不做乘/覆盖
    }

    /// <summary>效果配置资产。所有数值修改最终都走 AttributeSet.Modify —— 不存在第四条修改路径。</summary>
    [CreateAssetMenu(menuName = "MOMS/GameplayEffect")]
    public class GameplayEffect : ScriptableObject
    {
        public EffectDurationType durationType = EffectDurationType.Instant;

        [Tooltip("Duration 总时长（秒）。Instant 忽略")]
        public float duration = 5f;

        [Tooltip("周期触发间隔（秒）。<=0 = 不周期触发。Instant 忽略")]
        public float period = 0f;

        [Tooltip("持续型修饰：挂上即生效，Duration 到期按记录 delta 回退。⚠️ 别放 Health（到期回退可能把人'回退死'），持续回血/掉血走 periodModifiers")]
        public List<Modifier> modifiers = new();

        [Tooltip("周期型修饰：每 period 秒即时生效一次，逐跳独立、不回退（中毒/持续回血放这）")]
        public List<Modifier> periodModifiers = new();
    }
}
```

### 4.4 `Effects/ActiveEffect.cs`（运行时实例，纯 C#）

```csharp
using System.Collections.Generic;
using UnityEngine;
namespace Attributes
{
    /// <summary>一个正在生效的效果实例。SO 是配置（共享只读），本类持运行时状态——
    /// 同一 SO 可以同时挂在多个实体上互不干扰。</summary>
    public class ActiveEffect
    {
        public readonly GameplayEffect source;
        public float remaining;                      // Duration 倒计时
        float _periodTimer;                          // 周期累计器
        /// <summary>持续型修饰**实际生效**的 delta（clamp 后的落地值），到期按它逆运算回退——
        /// 不回退"配置值"而回退"落地值"，防御下限 clamp 等管线行为不会造成漂移</summary>
        public readonly List<(AttributeType type, float delta)> applied = new();

        public ActiveEffect(GameplayEffect src) { source = src; remaining = src.duration; }

        /// <summary>是否到周期点（到点即重置累计器）。返回 false = 本跳无周期触发</summary>
        public bool ConsumePeriod(float dt, out float interval)
        {
            interval = source.period;
            if (source.durationType != EffectDurationType.Duration || interval <= 0f) return false;
            _periodTimer += dt;
            if (_periodTimer < interval) return false;
            _periodTimer -= interval;
            return true;
        }
    }
}
```

### 4.5 `AttributeSet.cs`——效果管线（本期核心，~50 行）

类内新增字段与三个方法（`Revive` 改一行）：

```csharp
readonly List<ActiveEffect> _effects = new();

/// <summary>挂效果。Instant：逐 modifier Modify 完即走（不进列表）。
/// Duration：应用 modifiers 并记录落地 delta，进列表等 Tick 回退。
/// 目标已死：照挂（Defense buff 挂死人无意义但也无害；回池时 Revive 会清）。</summary>
public bool ApplyEffect(GameplayEffect effect, ChangeContext ctx)
{
    if (effect == null) return false;
    var active = new ActiveEffect(effect);
    foreach (var m in effect.modifiers)
        ApplyModifier(active, m, ctx);
    if (effect.durationType == EffectDurationType.Instant) return true;   // 即时效完即走
    _effects.Add(active);
    return true;
}

/// <summary>周期/到期推进。由宿主 RefreshFixedUpdate 转发，dt 用 FrameInterval（跟随实体 TimeScale）。
/// 惯例：先推进计时再处理到期——同帧挂同帧过期等价于没挂（Instant 语义兜底）。</summary>
public void TickEffects(float dt)
{
    for (int i = _effects.Count - 1; i >= 0; i--)      // 倒序：到期移除不打乱遍历
    {
        var e = _effects[i];
        var ctx = new ChangeContext(ChangeReason.Effect, null);
        if (e.ConsumePeriod(dt, out _))                 // 周期点：periodModifiers 逐跳即时生效
            foreach (var m in e.source.periodModifiers)
                Modify(m.type, m.magnitude, ctx);       // 不记录/不回退——每跳独立
        e.remaining -= dt;
        if (e.remaining <= 0f)
        {
            for (int k = e.applied.Count - 1; k >= 0; k--)          // 逆序回退落地 delta
                Modify(e.applied[k].type, -e.applied[k].delta, ctx);
            _effects.RemoveAt(i);
        }
    }
}

/// <summary>清空全部效果并回退（复活的「干净重生」语义）。Revive 里第一行调用。</summary>
public void ClearEffects()
{
    var ctx = new ChangeContext(ChangeReason.Effect, null);
    for (int i = _effects.Count - 1; i >= 0; i--)
        for (int k = _effects[i].applied.Count - 1; k >= 0; k--)
            Modify(_effects[i].applied[k].type, -_effects[i].applied[k].delta, ctx);
    _effects.Clear();
}

/// <summary>单个修饰落地：走 Modify 管线（clamp/判变全在），落地成功才记录 delta。</summary>
void ApplyModifier(ActiveEffect e, Modifier m, ChangeContext ctx)
{
    if (!_attrs.TryGetValue(m.type, out var attr)) return;
    float before = attr.currentValue;
    if (!Modify(m.type, m.magnitude, ctx)) return;      // 值没变（clamp 抵消）→ 无事发生不记录
    e.applied.Add((m.type, attr.currentValue - before));
}
```

`Revive`（AttributeSet.cs:95）第一行插 `_effects.Clear()` 还是 `ClearEffects()`？——**先回退再复活**：`ClearEffects()` 放方法第一行（死亡标记重置前回退，Defense 归 0 再谈满血）。类尾补一个只读视图供调试/将来 UI 用：

```csharp
public IReadOnlyList<ActiveEffect> ActiveEffects => _effects;
```

> 为什么回退用「逆 delta」不用「快照还原」：快照在两个效果叠同一属性时会互踩（A 回退把 B 的增益一起抹掉）；Additive 交换律保证逆序逐个 `-delta` 结果与施加顺序无关。

### 4.6 `HealthManageComponent.cs`——宿主转发（~10 行）

```csharp
// Init 里（cs:25 Define AttackPower 之后）：
_set.Define(AttributeType.Defense, cfg != null ? cfg.defense : 0f);

// 类内新增：
/// <summary>效果计时推进。Entity.FixedUpdate 驱动（Entity 没有 RefreshUpdate 调用链，
/// 别挂错）；FrameInterval 自带实体 TimeScale——子弹时间里 buff 同步变慢。</summary>
public override void RefreshFixedUpdate()
    => _set?.TickEffects(TimeScale);

/// <summary>挂效果（技能/调试/将来敌人 debuff 的统一入口）。</summary>
public bool ApplyEffect(GameplayEffect effect, Entity source)
    => _set != null && _set.ApplyEffect(effect, new ChangeContext(ChangeReason.Effect, source));

/// <summary>当前防御（减伤结算读这个）。</summary>
public float Defense => _set?.GetCurrentValue(AttributeType.Defense) ?? 0f;
```

（`TimeScale` 来自 EntityComponent.cs:31：有 Owner 用实体时间，无 Owner 退化全局——已写好不用动。）

### 4.7 `EntityCharacterConfig.cs`——加字段（零资产手术）

```csharp
public float attackDamage;          // 平A伤害数值
public float defense;               // 防御（0=无减免；一期资产没有该值，Unity 自动补 0）
```
现有 `PlayerCharacterData.asset` / 敌人配置**不用重做**：新增字段对旧 .asset 序列化自动补默认 0。想让玩家肉一点就给 PlayerCharacterData 填个 10。

### 4.8 `DamageComponent.cs`——减伤公式（:43 ApplyDamage 调用点前，2 行）

```csharp
// 减伤：平滑公式 dmg × K/(K+def)（def=50→67%，100→50%，300→25%，永远打不空）。
// 结算在战斗组件做（属性集保持纯数据），落地值经 ApplyDamage→delta→飘字自动显示减免后的数
float def = Owner.healthManageComponent != null ? Owner.healthManageComponent.Defense : 0f;
float actual = damage.damage * 100f / (100f + def);
if(Owner.healthManageComponent != null) Owner.healthManageComponent.ApplyDamage(actual, damage.origin);
```
（原 `damage.damage` 直传改成 `actual`；K=100 做 `const` 即可，将来想调手感再挪进 HitBoxConfig。）

### 4.9 `Editor/EffectSmokeTest.cs`（新菜单 `Attributes/Effect冒烟测试（纯逻辑）`）

仿 AttributeSmokeTest 风格（失败才红字、结尾汇总；AttributeSet 纯 C#，`TickEffects(dt)` 手喂假时间即可测到期）。用例：

1. **Instant**：`ApplyEffect`（Health+30, Defense+10）→ 值生效、`ActiveEffects` 计数 **0**（即时不留实例）
2. **Duration 应用**：铁壁形状（Defense+50, duration=5）→ Defense=50、计数 1
3. **到期回退**：`TickEffects(5f)` → Defense=0、计数 0、**恰好各发 2 次事件**（应用 1 + 回退 1）
4. **落地 delta 记录**：Defense+50 但先把 Defense Define 成上限 30 的场景 → 回退后精确归位（clamp 不漂移）
5. **重叠独立回退**：+30 与 +50 两个 Duration 效果叠 Defense → 80；先到期一个回 50、再到期归 0
6. **Period**：毒（duration=3, period=1, periodModifiers: Health-5）满血 100 → `Tick(1)`×3 → 85；`Tick(0.5)`×4（跨 3.5s）→ 停在 85 不多跳
7. **毒打死/免疫截停**：血量 8 时中毒 tick-5 → 死（跨 0 置位）；下一跳 -5 被死亡免疫拒绝、血仍 0
8. **Revive 清效果**：挂着 Defense buff 的实体 `Revive()` → Defense=0、计数 0、血回满
9. **值没变不发事件**：满血挂持续回血形状（periodModifiers Health+5）第一跳 → false、零事件

### 4.10 `UIDebugMenu.cs`——两个调试菜单（:73 HealPlayer 之后加）

```csharp
[MenuItem("UI/调试/铁壁（防+50 持续5秒）")]
public static void ApplyIronWall()
{
    if(!EnsurePlaying()) return;
    var fx = Resources.Load<GameplayEffect>("data/effect/IronWall");
    if(fx == null) { Debug.LogWarning("[UI] 缺 Resources/data/effect/IronWall.asset"); return; }
    Player.Instance?.healthManageComponent?.ApplyEffect(fx, null);
}
// 中毒同款：data/effect/Poison
```
（文件头补 `using Attributes;`。配合既有「玩家受伤10点」用：挂铁壁→受伤 → 飘字 7；过期→受伤 → 飘字 10。）

---

## 五、资产搭建（Resources/data/effect/）

1. Project 窗口右键 → Create → **MOMS → GameplayEffect**，改名 `IronWall`，拖进 `Assets/Resources/data/effect/`：
   - Duration Type: **Duration**；Duration: 5；Period: 0
   - Modifiers: 1 条（Type=Defense, Magnitude=**50**）；Period Modifiers 空
2. 同目录再建 `Poison`：Duration Type: Duration；Duration: 3；**Period: 1**
   - Modifiers 空；Period Modifiers: 1 条（Type=Health, Magnitude=**-5**）
3. （可选）`PlayerCharacterData.asset` 的 Defense 填 10——平时受伤就飘 9（10×100/110）

---

## 六、实施顺序（每步「写完即编译」）

1. **两个枚举**（4.1/4.2）+ `GameplayEffect.cs` + `ActiveEffect.cs` —— 纯新增，编译过即可
2. **AttributeSet 效果管线**（4.5）+ **EffectSmokeTest**（4.9）→ 跑 `Attributes/Effect冒烟测试（纯逻辑）` 全绿
   ——和一期同样的打法：先纯 C# 锁规则，再接线
3. **宿主转发**（4.6）+ **配置字段**（4.7）→ 编译绿，行为无变化（还没资产没公式）
4. **减伤公式**（4.8）+ **建两个 .asset**（§五）+ **调试菜单**（4.10）
5. 全绿 → 跑 §七 清单

> 第 4 步之前防御恒 0，公式等于没接——中间态无感，可放心分两天写。

---

## 七、端到端验证清单

1. **Effect 冒烟**：`Attributes/Effect冒烟测试（纯逻辑）` 9 条全绿（非 Play 也要绿）
2. **旧冒烟回归**：`Attributes/冒烟测试（纯逻辑）` 仍全绿（枚举追加不破坏一期）
3. **铁壁闭环**：挂铁壁 → 受伤10 → 飘 **7**、血条 -7；等 5 秒 → 再受伤 → 飘 **10**（Console 无红字）
4. **重叠叠加**：铁壁挂两次 → 受伤 → 飘 10×100/200=**5**；先到期一个后伤害变大（回退独立）
5. **中毒**：挂毒 → 每秒血条 -5、**不飘字**（Effect 不在飘字白名单）→ 3 秒停、Defense 全程不动
6. **毒打死**：把玩家打到 8 血以下再挂毒 → 毒死（死亡动画触发，OnDied 链零改动验证）；毒停
7. **复活清效果**：治疗拉活后 Defense 归 0、再受伤恢复飘 10
8. **子弹时间交互**（可选）：开子弹时间挂铁壁 → buff 明显变慢（FrameInterval 语义生效）
9. **敌人回归**：打怪掉血飘字数值与改前一致（敌人 Defense=0 → 公式原样输出）
10. **飘字不重复**：打怪每刀**只飘一个数字**（一期收尾删了 DamageComponent 直调，此处防回归）

---

## 八、设计决策记录

1. **Additive-only**：效果只做加法修饰。乘/覆盖需要完整聚合链（排序/重算/失效传播），等真需求（暴击、护甲穿透）再上——一期「不上 Tag」同款克制。
2. **回退记落地 delta，不做快照**：重叠效果互不干扰；clamp 后的落地值逆运算保证精确归位（冒烟用例 4 锁死）。
3. **计时挂 `RefreshFixedUpdate` + `FrameInterval`**：Entity 只有 FixedUpdate 驱动链（§2.3），`RefreshUpdate` 是死代码别用；实体 TimeScale 让子弹时间正确拖慢 buff——游戏效果跟游戏时间走。
4. **`modifiers` 与 `periodModifiers` 分成两列**：持续 stat（到期回退）与周期即时（每跳独立）是两种语义，混一列就得给 Modifier 加回退标志，配置面立刻复杂。铁壁/毒各占一列，配置零歧义。
5. **持续修饰禁放 Health**（SO 字段 Tooltip 写明）：到期回退可能把人「回退死」；持续回血/掉血走 periodModifiers（逐跳独立，毒打死人是合法游戏语义而非回退事故）。
6. **减伤公式放 DamageComponent 不放 AttributeSet**：减免是战斗规则，属性集保持纯数据；且 Entity.Hit 的 Block/Miss 分流在减免**之前**（Entity.cs:186-190），被挡的攻击不产生任何属性变化——层级正确。K=100 常量起步。
7. **`Revive()` 清效果**：池化复用「干净重生」——敌人带毒进池、复活又毒 3 秒是诡异状态；先回退再置满血，顺序写死在 AttributeSet.Revive 第一行。
8. **`Effect` 原因不飘字**：buff 起效/回退/毒跳都不是「打击反馈」，白名单不加它；将来要毒字/治疗跳字，往 FloatingTextManager 过滤器加一行即可（事件里信息全有）。

---

## 九、遗留与路标

1. **Cue 三期**（一期路标延续）：受击闪红/血粒子/相机抖动迁事件订阅侧——效果系统就位后，buff 视觉（铁壁特效）也是 Cue 的天然消费者。
2. **Modifier 聚合链**：乘法/覆盖/优先级——暴击率是第一候选消费者。
3. **堆叠规则**：同 SO 重复挂 = 刷新时长/叠加层数，GameplayEffect 加一个 `stackingMode` 即可起步。
4. **敌人 debuff 实战化**：敌人攻击命中玩家时 `ApplyEffect`（HitBoxConfig 加 effect 引用字段），本期调试菜单已验证全链。
5. **buff UI**：`ActiveEffects` 只读视图已留（§4.5），图标+剩余时间条等 HUD 需求来了再做。
6. **顺手项**：`EntityComponent.RefreshUpdate`（:38）全项目无人调用的死代码，哪天清理时删掉或在 Entity.Update 里接上，二选一，别让它半死不活。

# GAS 改造一期设计方案（Attribute 化：属性集 + 结构化事件）

> 分工延续：本文档给出全部设计与成员级骨架，代码由你手写、验证由你自跑。
> 所有 `文件:行号` 引用已对当前代码核实（2026-09-30）。
>
> **v2 修订**（2026-10-06，讨论后定案）：HealthManageComponent **原地改造**（类名/槽位/prefab 全不动，内部实现整体换血为 AttributeSet 宿主），不再新建 AttributeComponent、不再做 prefab 手术。旧 API 按腐化风险分拣：**写路径与瘦事件删除**（编译器强制迁移）、**语义事件与读路径保留**（Enemy/PlayerAnimator/Entity 零改动）。理由见 §九.1/.2。

---

## 一、目标与范围

**一句话**：把血量/攻击力从散落的字段收进统一属性系统（AttributeSet），数值修改全部经过一条带上下文的管线——表现层（血条/飘字）从"被调用"变成"订阅"。

| 本期做 | 本期不做（留后续） |
|---|---|
| AttributeType / Attribute / AttributeSet（纯 C# 核心） | Modifier 聚合链（加法/乘法/优先级）——Effect 二期的核心 |
| HealthManageComponent 原地改造（内部 = AttributeSet 宿主） | GameplayEffect SO（Instant/Duration 配置化）——Effect 二期 |
| 结构化事件 `Changed{owner,type,old,new,delta,ctx}` | GameplayTag 标签树（enum 够用，见 §九.7） |
| Base/Current 分离 + Instant/Duration 语义预埋（规则注释） | 网络同步/预测（单机不需要） |
| 飘字迁到事件订阅侧（FloatingTextManager），DamageComponent 瘦身 | 受击闪红/血粒子/Hit 动画 Cue 化——Cue 三期（本期只迁飘字） |
| 旧 API 分拣删留 + 3+3 处消费者迁移 + PlayerHealth 空壳删除 | 敌人头顶血条、死亡表现（既三期 UI 路标） |
| 纯 C# 冒烟测试菜单（不开 Play 就能验属性规则） | |

**验收标准**：打怪→掉血→飘字→打空→敌人回池、玩家受伤→血条→血空→死亡动画，全链行为与改造前一致；`DamageComponent.Hit` 里不再有任何数值与飘字代码；**两个 prefab 一个指头都不用碰**。

---

## 二、现状事实（设计依据，已逐条核实）

### 2.1 迁移面（好消息：完全封闭）

| 核查项 | 结果 |
|---|---|
| 挂 HealthManageComponent 的 prefab | **只有 Player.prefab 和 Enemy.prefab**（guid 比对；NPC.prefab 干净）|
| 场景散挂 | 无（BABEL1/BABEL2/1.unity 三个场景 grep 零命中）|
| `maxHealth` 读者 | 唯一：HealthManageComponent.LoadConfig（HealthManageComponent.cs:27）|
| `attackDamage` 读者 | 唯一：PlayerCombat（PlayerCombat.cs:27,39）|
| 配置资产值 | PlayerCharacterData.asset：`maxHealth: 100, attackDamage: 10` |

### 2.2 属性现状：三处散落、无事件上下文

- **HP**：HealthManageComponent（maxHP/currentHP 字段 + TakeDamage/Heal/SetHP/Revive + OnChanged/OnDied/OnRevived）
- **攻击力**：PlayerCombat 私有 `[SerializeField] float attackDamage`（PlayerCombat.cs:14）——Inspector 外没人读得到
- **OnChanged payload 太瘦**：只有 `(current, max)`——飘字拿不到伤害值/来源/类型，这就是二期飘字被迫焊在 DamageComponent.Hit 里的根因

### 2.3 消费者全集（迁移时一个不能漏）

| 消费者 | 位置 | 用的是 | v2 处置 |
|---|---|---|---|
| DamageComponent | DamageComponent.cs:45-48 | TakeDamage + FloatingText.Show | **迁**（写路径已删） |
| HudPanel | HudPanel.cs:25-38 | OnChanged + CurrentHP/MaxHP | **迁**（瘦事件已删，读路径保留） |
| UIDebugMenu | 两个调试菜单 | TakeDamage / SetHP | **迁**（写路径已删） |
| Enemy.Die | Enemy.cs:173,177（OnEnable/OnDisable 订退） | OnDied | **零改动**（语义事件保留） |
| PlayerAnimatorComponent | PlayerAnimatorComponent.cs:71-72 订 / :135-136 退 | OnDied | **零改动**（同上） |
| Enemy.OnSpawnFromPool | Enemy.cs:136 | Revive() | **零改动**（语义糖保留） |
| Entity.cs | :21,:99,:105 字段与缓存 | 组件引用 | **零改动**（类名不动） |

### 2.4 防护分流在管线之前（不用动）

`Entity.Hit`（Entity.cs:180-188）：IsBlock → Blocked、IsInvincible → Miss，都在进 DamageComponent 之前 return——格挡/无敌永远不会触发属性变化。**这套分流逻辑原样保留**，它就是将来 GAS 语义里"结算否决"，位置正确。

### 2.5 无关但同名的坑：PlayerHealth 空壳

`Modules/PlayerHealth.cs`（注释写着"管理血量"但 LoadConfig 是空方法）挂 PlayerModuleControlComponent.cs:12，纯摆设——本期删除（字段/Bind/LoadConfig 三行连带清理）。

### 2.6 二期成果（本期的消费者/验收工具）

- DamageComponent.cs:45-48：二期接的 `TakeDamage + FloatingTextManager.Show`——本期飘字迁走、伤害改带来源
- HudPanel（TryBind 订阅 OnChanged）、UIDebugMenu 三个调试菜单、Enemy.OnSpawnFromPool 的 Revive
- FloatingTextManager 是纯 C# IManager，Initialize/Deinitialize 是天然的订阅/退订位

---

## 三、总体架构

### 3.1 分层与数据流（改造后）

```
配置层   EntityCharacterConfig（SO，不动：maxHealth/attackDamage 仍是数据源）
            │ HealthManageComponent.Init() 灌值（Define）
核心层   AttributeSet（纯 C#）：Dictionary<AttributeType, Attribute>
            │  唯一修改入口：Modify/SetCurrent/SetBase → clamp → 发事件
            ├─ 实例事件 Changed(AttributeChangedEventArgs e)
            └─ 静态总线 AnyChanged（所有实体共享，全局表现层订阅一次）
宿主层   HealthManageComponent（原地改造 = 事实上的 ASC 单机版）
            ├─ 保留：OnDied/OnRevived 事件、CurrentHP/MaxHP/Ratio/IsDead/Revive()
            ├─ 新增：Set 属性、ApplyDamage/ApplyHeal（带来源）、AttackPower
            └─ 删除：TakeDamage/Heal/SetHP/OnChanged/LoadConfig/maxHP/currentHP 字段
消费层   DamageComponent（只发伤害，不碰表现）
         PlayerCombat（攻击力从属性读）
         HudPanel / FloatingTextManager（订阅事件总线）
         Enemy / PlayerAnimatorComponent（零改动，靠保留的语义事件）
```

### 3.2 文件总览

```
新增 Assets/Script/AttributeSystem/（namespace Attributes，Editor 除外）
├── Core/AttributeType.cs            enum { Health, MaxHealth, AttackPower }
├── Core/AttributeChange.cs          事件参数 struct + ChangeContext + ChangeReason
├── Core/Attribute.cs                单属性：baseValue/currentValue（纯数据+规则）
├── Core/AttributeSet.cs             容器 + 修改管线 + Changed/AnyChanged + 死亡标记
└── Editor/AttributeSmokeTest.cs     冒烟菜单（纯 C#，非 Play 可跑，仿 GuideDebugMenu）

原地改造（1 文件）
└── Script/Component/HealthManageComponent.cs   内部实现整体换血，类名/文件名/guid 不动

删除（1 文件）
└── Script/Modules/PlayerHealth.cs   空壳（PlayerModuleControlComponent 三行连带清理）

迁移（3 必改 + 3 升级，每处 1~6 行）
├── Script/Component/DamageComponent.cs       TakeDamage+Show → ApplyDamage(带来源)（必改）
├── UISystem/Integration/HudPanel.cs          OnChanged → Set.Changed 结构化订阅（必改）
├── UISystem/Editor/UIDebugMenu.cs            两菜单换新 API（必改）
├── Script/Modules/PlayerCombat.cs            attackDamage 退役，改读 AttackPower（升级）
├── UISystem/Integration/FloatingTextManager.cs  订阅 AnyChanged 总线（升级）
└── Script/Component/PlayerModuleControlComponent.cs  删 PlayerHealth 三行（清理）

零改动：Entity.cs、Enemy.cs、PlayerAnimatorComponent.cs、Player.prefab、Enemy.prefab
```

---

## 四、逐文件规格（核心层成员级骨架）

### 4.1 `Core/AttributeType.cs`

```csharp
namespace Attributes
{
    /// <summary>实体属性标识。起步三个，新属性加枚举值即可（二期 Effect 直接引用）。</summary>
    public enum AttributeType { Health, MaxHealth, AttackPower }
}
```

### 4.2 `Core/AttributeChange.cs`（事件参数——本期的核心产出）

```csharp
using UnityEngine;
namespace Attributes
{
    /// <summary>变化原因：表现层据此分色/过滤（飘字只认 Damage/Heal）</summary>
    public enum ChangeReason { Init, Damage, Heal, Revive, Debug, Set }

    /// <summary>变化上下文：谁改的、为什么改。二期升级为 GameplayEffectSpec 引用。</summary>
    public struct ChangeContext
    {
        public Entity source;   // 伤害来源等（可为 null：环境/调试）
        public ChangeReason reason;
        public ChangeContext(ChangeReason reason, Entity source = null)
        { this.reason = reason; this.source = source; }
    }

    /// <summary>属性变化事件参数——比旧 OnChanged(current,max) 多出的正是
    /// 二期飘字当初拿不到的东西：old/new/delta/来源/原因</summary>
    public struct AttributeChangedEventArgs
    {
        public Entity owner;
        public AttributeType type;
        public float oldValue, newValue, delta;
        public ChangeContext context;
    }
}
```

> `Entity` 在根命名空间，无需 using。struct 而非 class：高频事件零 GC。

### 4.3 `Core/Attribute.cs`（纯数据 + clamp 规则）

```csharp
namespace Attributes
{
    /// <summary>单属性。Base=永久值（升级/装备），Current=运行值（伤害/治疗/临时 buff）。
    /// GAS 语义预埋：Instant Effect 改 Current；Duration Effect 改 Current、到期回 Base（二期实现）；
    /// 只有"学习/装备"类永久变化才改 Base。</summary>
    public class Attribute
    {
        public float baseValue;
        public float currentValue;
        public float MinValue;   // Health=0；MaxHealth=1（防除零）；AttackPower=0
        public float Clamp(float v) => Mathf.Max(MinValue, v);
    }
}
```

### 4.4 `Core/AttributeSet.cs`（修改管线——所有数值变化的必经之路）

```csharp
using System.Collections.Generic;
using UnityEngine;
namespace Attributes
{
    public class AttributeSet
    {
        Entity _owner;
        readonly Dictionary<AttributeType, Attribute> _attrs = new();
        bool _diedFired;    // 死亡标记：只触发一次语义，见 Apply ②

        /// <summary>实例事件：本实体任一属性变化（值真变了才发，见 Apply ①）</summary>
        public event System.Action<AttributeChangedEventArgs> Changed;
        /// <summary>静态总线：全局表现层（飘字等）订阅一次即可监听所有实体</summary>
        public static event System.Action<AttributeChangedEventArgs> AnyChanged;

        public AttributeSet(Entity owner) => _owner = owner;

        /// <summary>定义属性（Init 时调用）。base 值同时灌入 current。
        /// 走 Define 不发事件——进场满血不该触发任何表现。</summary>
        public void Define(AttributeType type, float baseValue, float min = 0f)
        {
            _attrs[type] = new Attribute { baseValue = baseValue, currentValue = baseValue, MinValue = min };
        }

        public float GetCurrentValue(AttributeType type) => _attrs[type].currentValue;
        public float GetBaseValue(AttributeType type)    => _attrs[type].baseValue;

        // ── 三条修改路径（二期 Effect 也走这三条，不新增第四条）──

        /// <summary>增量修改 Current（伤害/治疗/临时效果）。死亡后免疫再伤害（old IsDead 语义）。
        /// ⚠️ 行为变化：旧 Heal 对死尸 return（只能 Revive），新版正增量放行——治疗可拉活
        ///（「药水复活」语义，动作游戏常见；若要禁止，把死亡免疫条件改成 _diedFired 全拒）。</summary>
        public bool Modify(AttributeType type, float delta, ChangeContext ctx)
        {
            if(!_attrs.TryGetValue(type, out var attr)) return false;
            if(type == AttributeType.Health && _diedFired && delta < 0f) return false;  // 死亡免疫
            return Apply(type, attr, attr.currentValue + delta, ctx);
        }

        /// <summary>直接设 Current（调试/特殊路径）。</summary>
        public bool SetCurrent(AttributeType type, float value, ChangeContext ctx)
        {
            if(!_attrs.TryGetValue(type, out var attr)) return false;
            return Apply(type, attr, value, ctx);
        }

        /// <summary>设 Base（装备改变上限等）。Current 保留但被新上限 clamp——
        /// 上限降了血跟着掉，符合直觉；上限升了血不自动涨（要涨请 Revive/Heal）。</summary>
        public bool SetBase(AttributeType type, float value, ChangeContext ctx)
        {
            if(!_attrs.TryGetValue(type, out var attr)) return false;
            attr.baseValue = attr.Clamp(value);
            bool changed = Apply(type, attr, attr.currentValue, ctx);   // 触发 clamp 检查与事件
            if(type == AttributeType.MaxHealth)   // 上限变了，Health 连带重 clamp（否则血量悬在新上限外）
            {
                var hp = _attrs[AttributeType.Health];
                changed |= Apply(AttributeType.Health, hp, hp.currentValue, ctx);
            }
            return changed;
        }

        // ── 管线核心：clamp → 判变 → 发事件 → 维护死亡标记 ──
        bool Apply(AttributeType type, Attribute attr, float newCurrent, ChangeContext ctx)
        {
            float v = attr.Clamp(newCurrent);
            if(type == AttributeType.Health)
                v = Mathf.Clamp(v, 0f, GetCurrentValue(AttributeType.MaxHealth));

            // ① 值没变不发事件：满血再治疗无事件 → 天然不飘字
            if(Mathf.Approximately(v, attr.currentValue)) return false;
            float old = attr.currentValue;
            attr.currentValue = v;

            var args = new AttributeChangedEventArgs
            { owner = _owner, type = type, oldValue = old, newValue = v, delta = v - old, context = ctx };
            Changed?.Invoke(args);
            AnyChanged?.Invoke(args);

            // ② 死亡标记：Health 跨过 0 线**双向**维护——
            //    跌破 0：置 true（Modify 从此拒负增量 = 死亡免疫，OnDied 由宿主推导只发一次）
            //    从 0 拉正（SetCurrent/治疗）：置 false——不重置的话「死而复生」后永远打不掉血
            if(type == AttributeType.Health)
            {
                if(old > 0f && v <= 0f) _diedFired = true;
                else if(old <= 0f && v > 0f) _diedFired = false;
            }
            return true;
        }

        /// <summary>复活语义：Current 回满（到 base）、死亡标记重置。血量变化自然发事件。</summary>
        public bool Revive(ChangeContext ctx)
        {
            if(!_attrs.TryGetValue(AttributeType.Health, out var attr)) return false;
            _diedFired = false;
            return Apply(AttributeType.Health, attr, attr.baseValue,
                new ChangeContext(ChangeReason.Revive, ctx.source));
        }
        public bool IsDead => _attrs.TryGetValue(AttributeType.Health, out var a) && a.currentValue <= 0f;
    }
}
```

> `OnDied` 事件不放 AttributeSet（纯数据层），由宿主组件从 Changed 推导——见 4.5。

### 4.5 `HealthManageComponent.cs`（原地改造——本方案的心脏）

**类名、文件名、namespace 一律不动**（prefab 按 guid+类名引用脚本，动即断）。改动是内部换血：

**删除**（编译器强制迁移的来源）：
- `[SerializeField] maxHP / currentHP` 两字段（prefab 上的序列化值自动 orphan，无损失——Init 从 config 灌值）
- `TakeDamage / Heal / SetHP` 三方法（写路径：缺 source，腐化源头）
- `OnChanged` 事件（瘦 payload，被结构化事件取代，**不转发**——逼 HudPanel 升级）
- `LoadConfig(EntityCharacterConfig)`（逻辑并入 Init）

**保留**（订阅侧零迁移）：
- `OnDied / OnRevived` 事件、`Revive()`、`CurrentHP / MaxHP / Ratio / IsDead`——语义成员不丢 context，永不腐化

**新增**：`Set` 属性、`ApplyDamage / ApplyHeal`（带来源）、`AttackPower`

```csharp
using System;
using UnityEngine;
using Attributes;
namespace Components
{
    /// <summary>
    /// 实体属性宿主（GAS 一期「Attribute 化」后的 ASC 单机版）。
    /// ⚠️ 名字是历史遗留：本类已不止管血量——AttackPower 等全部实体属性都在这
    ///（不改名是因为 prefab 按 guid+类名引用脚本，改名=全员手术，收益不成比例）。
    /// 旧写路径（TakeDamage/Heal/SetHP/OnChanged）已删：数值修改必须走 Set（带来源与原因）；
    /// 读路径与语义事件保留，Enemy/PlayerAnimator 等订阅侧零迁移。
    /// </summary>
    public class HealthManageComponent : EntityComponent
    {
        AttributeSet _set;
        public AttributeSet Set => _set;

        // ── 保留的语义事件（订阅侧零改动）──
        public event Action OnDied;
        public event Action OnRevived;

        public override void Init()
        {
            base.Init();
            _set = new AttributeSet(Owner);
            // 从配置灌值（旧 LoadConfig 并入）。min：MaxHealth≥1 防除零
            var cfg = Owner.characterData;
            float maxHP = cfg != null ? cfg.maxHealth : 100f;   // 无配置兜底=旧字段默认值
            _set.Define(AttributeType.MaxHealth, maxHP, min: 1f);
            _set.Define(AttributeType.Health, maxHP);
            _set.Define(AttributeType.AttackPower, cfg != null ? cfg.attackDamage : 1f);
            // OnDied 推导：从结构化事件里识别「血量跌破 0」这一次性语义
            _set.Changed += e =>
            {
                if(e.type == AttributeType.Health && e.oldValue > 0f && e.newValue <= 0f)
                    OnDied?.Invoke();
            };
        }

        // ── 新写路径（带来源——这是本期改造的_point_）──
        public void ApplyDamage(float amount, Entity source)
            => _set.Modify(AttributeType.Health, -amount, new ChangeContext(ChangeReason.Damage, source));
        public void ApplyHeal(float amount, Entity source)
            => _set.Modify(AttributeType.Health, amount, new ChangeContext(ChangeReason.Heal, source));

        // ── 保留的读路径/语义糖（转发 Set；_set 为 null = Init 前访问，返回安全默认）──
        public void Revive()
        {
            _set?.Revive(new ChangeContext(ChangeReason.Revive));
            OnRevived?.Invoke();
        }
        public float CurrentHP   => _set?.GetCurrentValue(AttributeType.Health) ?? 0f;
        public float MaxHP       => _set?.GetCurrentValue(AttributeType.MaxHealth) ?? 0f;
        public float Ratio       => MaxHP > 0f ? CurrentHP / MaxHP : 0f;
        public float AttackPower => _set?.GetCurrentValue(AttributeType.AttackPower) ?? 0f;
        public bool  IsDead      => _set != null && _set.IsDead;
    }
}
```

### 4.6 `Editor/AttributeSmokeTest.cs`（纯 C# 冒烟，非 Play 可跑）

仿 GuideDebugMenu 冒烟风格，菜单 `Attributes/冒烟测试（纯逻辑）`。Entity 是 MonoBehaviour 造不了实例——冒烟里 owner 传 `null`（AttributeSet 不解引用 owner，只在事件参数里携带；断言不依赖它）。用例：

1. `Define(MaxHealth=100, min1) + Define(Health=100)` → CurrentHP=100
2. `Modify(Health,-30)` → 70；事件恰 1 次：old=100 new=70 delta=-30
3. `Modify(Health,-1000)` → 0（clamp）；从 >0 跌到 ≤0 的 Changed 事件恰 1 次
4. `Modify(Health,-10)` → 返回 false，无事件（死亡免疫），值仍 0
5. `Revive()` → 100；再 `Modify(-1000)` → 再次跌破（标记已重置）
6. `SetBase(MaxHealth,50)` → MaxHealth 与 Health 各发 1 次事件（SetBase 连带重 clamp），Health=50
7. 满血 `Modify(Health,+10)` → 返回 false，**零事件**（Apply 规则①的活验收）
8. `AnyChanged` 静态计数 == 实例事件计数（总线转发无损）
9. 死后 `Modify(Health,+50)` → 成功拉活（行为变化⑤的显式断言——想改成禁止就改这条+管线条件）

---

## 五、消费者迁移表（3 必改 + 3 升级）

### ① `Script/Component/DamageComponent.cs`（必改——本期瘦身样板）

:45-48 四行（TakeDamage + LogWarning + 注释 + 飘字 Show）→ 换成：

```csharp
// 数值走属性管线（带来源——击杀统计/仇恨/表现上下文从此有数据可用）；
// 飘字已迁至 FloatingTextManager（订阅 AnyChanged 总线），本组件不再碰表现层
if(Owner.healthManageComponent != null)
    Owner.healthManageComponent.ApplyDamage(damage.damage, damage.origin);
else
    Debug.LogWarning($"[Damage] {Owner.name} 未挂 HealthManageComponent，伤害未落地");
```
（`using Managers.UI;` 可删——此文件不再碰表现层）

### ② `UISystem/Integration/HudPanel.cs`（必改——事件升级）

```csharp
void TryBind()
{
    var player = Player.Instance;
    if(player == null || player.healthManageComponent == null) return;
    player.healthManageComponent.Set.Changed += OnAttrChanged;   // 订结构化事件（旧 OnChanged 已删）
    _bound = true;
    RefreshBar();   // 立即刷一次（Define 不发事件，进场要手动刷真实值）
}
void OnAttrChanged(AttributeChangedEventArgs e)
{
    if(e.type != AttributeType.Health && e.type != AttributeType.MaxHealth) return;
    RefreshBar();
}
void RefreshBar()
{
    var a = Player.Instance.healthManageComponent;
    fill.fillAmount = a.MaxHP > 0f ? a.CurrentHP / a.MaxHP : 0f;
    if(hpText != null) hpText.SetText("{0:0}/{1:0}", a.CurrentHP, a.MaxHP);
}
// OnHide：退订 Set.Changed；_bound = false 无条件重置（二期修的教训保留）
```
（文件头加 `using Attributes;`；其余 TryBind/Update 骨架不动）

### ③ `UISystem/Editor/UIDebugMenu.cs`（必改——写路径已删）

- HurtPlayer：`TakeDamage(10f)` → `ApplyDamage(10f, null)`
- HealPlayer：`SetHP(float.MaxValue)` → `ApplyHeal(9999f, null)`（reason=Heal → 飘绿字，见 §八.7；clamp 落在 MaxHP）
- **注释更新**：原"故意不走 DamageComponent 所以不飘字"的说明作废——GAS 化后飘字订阅事件总线，任何来源的伤害都自动带飘字（这本身是验收点，见 §八.3）

### ④ `Script/Modules/PlayerCombat.cs`（升级——攻击力迁入属性）

- :14 删 `attackDamage` 字段；:26-27 LoadConfig 删 attackDamage 赋值行（attackForce 保留）
- :39 `GenerateHitbox(..., attackDamage, ...)` → `GenerateHitbox(..., Owner.healthManageComponent.AttackPower, ...)`

### ⑤ `UISystem/Integration/FloatingTextManager.cs`（升级——飘字归位表现层，本期的活验收）

Initialize 订阅静态总线，Deinitialize 退订：

```csharp
public IEnumerator Initialize()
{
    ...原有 prefab/canvas 加载...
    AttributeSet.AnyChanged += OnAnyAttrChanged;   // using Attributes;
    yield break;
}
public void Deinitialize()
{
    AttributeSet.AnyChanged -= OnAnyAttrChanged;   // 与订阅对称
}

void OnAnyAttrChanged(AttributeChangedEventArgs e)
{
    if(e.reason != ChangeReason.Damage && e.reason != ChangeReason.Heal) return;  // Init/Revive/Debug 不飘
    if(e.type != AttributeType.Health || e.owner == null) return;
    string text = Mathf.Abs(e.delta).ToString("0");          // 显示实际落地值（有防御后=减免后的数，GAS 语义的改进）
    Color color = e.delta < 0f ? Color.white : Color.green;  // 伤害白/治疗绿（顺手完成旧路标项）
    Show(text, e.owner.ChestPosition, color);
}
```

> `Entity` 在根命名空间，`e.owner.ChestPosition` 直接可用。被 Block/Miss 分流的（Entity.cs:180-188）不产生属性变化 → 天然不飘字，与二期行为一致。

### ⑥ `Script/Component/PlayerModuleControlComponent.cs`（清理）

- :12 删 `[SerializeField] public PlayerHealth Health = new();`
- :25 删 `Health.Bind(Owner);`
- :30 删 `Health.LoadConfig(characterData);`
- 删文件 `Modules/PlayerHealth.cs`

---

## 六、prefab：零手术（v2 的核心红利）

Player.prefab / Enemy.prefab **一个指头都不用碰**：

- Unity 按 `guid + fileID（类名）` 引用脚本——类名不动，引用照常解析
- 组件上序列化的 `maxHP/currentHP` 值随字段删除自动 orphan（Unity 静默丢弃，无警告无损失——Init 从 config 灌值，本来也不该信 prefab 上的散值）
- 唯一检查项：改造后首次进 Play，Console 无 missing script 警告、Inspector 上 HealthManageComponent 面板**不再显示** maxHP/currentHP（显示=字段没删干净）

---

## 七、实施顺序（每步「写完即编译」）

1. **核心层四文件**（4.1~4.4）+ **冒烟测试**（4.6）→ 跑 `Attributes/冒烟测试（纯逻辑）` 全绿
   ——纯新增零迁移，9 条断言把 clamp/死亡免疫/双向标记/事件次数/总线全部锁定，后面迁移全靠它兜底
2. **HealthManageComponent 原地改造**（4.5）
   ——此刻编译**爆红**：DamageComponent / HudPanel / UIDebugMenu 三处旧调用点全部报错，**正好当迁移清单**（这就是删写路径而不留 [Obsolete] 的原因：报错比警告更不可忽略）
3. 按 §五 ①②③ 修三处红 → 编译绿（半程可玩：此时飘字还在 DamageComponent 老位置吗？不——②已删飘字调用。**注意**：第 2 步后到第 4 步前，飘字暂时消失，属预期中间态）
4. §五 ④⑤⑥（PlayerCombat / FloatingTextManager 订阅 / 删空壳）→ 飘字以事件驱动回归
5. 全绿 → 跑 §八 清单
6. 确认无引用后删 `Modules/PlayerHealth.cs`（第 4 步已清引用，此步只是删文件）

> 第 3~4 步之间飘字短暂消失是唯一的中间态窗口——一个人开发半天量，不需要为它设计过渡。

---

## 八、端到端验证清单

1. **冒烟**：`Attributes/冒烟测试（纯逻辑）` → 全部通过 ✓（非 Play 也要绿）
2. **prefab 零手术验证**：进 Play 无 missing script 警告；Inspector 上 HealthManageComponent 无 maxHP/currentHP 字段
3. **打怪回归**：普攻命中 → 敌人掉血飘字（白，位置在敌人胸口）→ 攻击力数值与改前一致（PlayerCharacterData.attackDamage=10，从属性读出仍 10）
4. **事件驱动飘字的语义升级验证**：`UI/调试/玩家受伤10点` → 血条降 **且飘字出现（10，白）**——二期该菜单"不飘字"（直调绕过 DamageComponent），GAS 化后任何来源都走事件总线，**这是数据/表现分离生效的证据**，不是 bug
5. **死亡链 A（敌人）**：打空敌人 → 消失回池；再刷出的敌人满血（OnDied/Revive 保留成员的零改动验证）
6. **死亡链 B（玩家）**：玩家血空 → 死亡动画（PlayerDeathState，OnDied 保留成员的零改动验证）→ 血条停在 0
7. **治疗拉活**：玩家死后 `UI/调试/玩家治疗回满` → 血条回满、**绿字**飘出（reason=Heal）、之后再受伤正常（双向死亡标记的活验收——这条挂了就是 §4.4 Apply ② 没写对）
8. **死亡免疫**：玩家死后再点受伤菜单 → 血量/飘字/血条全无反应
9. **引导/对话回归**：横幅显隐、NPC 对话不受影响
10. **池化回归**：连续打空 3 只敌人，控制台无 NRE（OnEnable/OnDisable 订退照常）
11. **打击感回归**：击退距离、闪红、血粒子、Hit 动画与改造前观感一致（本期不许动它们）
12. **Hierarchy 卫生**：Play 全程无 FloatingText 泄漏（DamageNumber 自毁逻辑未动）

---

## 九、设计决策记录

1. **原地改造而非新建 AttributeComponent**（v2 定案）：prefab 按 guid+类名引用脚本，换组件=两个 prefab 手术+Entity 改名+全员迁移；原地换血=零手术、Enemy/PlayerAnimator/Entity 零改动。代价是名实不符（"血量组件"管全属性），用类头注释显式声明，将来真嫌碍眼再做一次纯卫生手术。
2. **旧 API 分拣删留，判定标准="是不是丢 context 的修改入口"**：写路径（TakeDamage/Heal/SetHP）与瘦事件（OnChanged(current,max)）是腐化源头，**删**（编译器强制迁移）；语义事件（OnDied/OnRevived）与读路径（CurrentHP/MaxHP/Revive）不丢信息、永不腐化，**留**（订阅侧零迁移）。不留 [Obsolete] 过渡期：单人+迁移面封闭，渐进的前提一条不成立。
3. **事件 struct 不改 class**：属性变化高频，零 GC；字段全只读语义（struct 拷贝天然隔离）。
4. **值没变不发事件**：满血治疗无事件 → 不飘字。旧 Heal 无条件发事件、表现无差别——新规则让"无意义事件"在源头消失。
5. **OnDied 是推导事件不是数据**：宿主从 Changed 里识别「Health old>0→new≤0」；`_diedFired` 跨 0 线双向维护（跌破置位/拉正复位——初版只在 Revive 复位，SetCurrent 拉活的实体会带残留标记永远打不掉血，自查修正）。
6. **飘字归 FloatingTextManager 订阅 AnyChanged**：数值与表现分离的第一刀——DamageComponent 从此只管击退/动画/粒子（那些 Cue 三期收）。**验收指标**：DamageComponent.cs 里不再有 FloatingTextManager 字样。
7. **不上 GameplayTag**：DamageType/ImpactType/HitResultType/ChangeReason 都是 enum，单机 2D 用标签树是负重；Effect 二期若出现"多维度互斥"需求再议。
8. **Reason 用 enum 不用 string**：白名单过滤（飘字只认 Damage/Heal）写成 switch 有编译器检查，string 拼写错静默失败。
9. **MaxHealth 的 clamp 语义**：上限降 → Current 跟降（SetBase 连带重 clamp）；上限升 → Current 不动。规则一条说清，二期 Duration buff（临时上限）直接复用。
10. **Entity 在事件参数里传引用而非坐标**：位置是表现偏好（胸口/头顶/随机偏移），数据层不该替表现层决定；Cue 侧自取 `owner.ChestPosition`。
11. **配置仍走 EntityCharacterConfig**：SO 数据源不动，HealthManageComponent.Init 是唯一灌值点——"配置→属性"单向，防两处漂移。

---

## 十、遗留与路标

1. **Effect 二期**：GameplayEffect SO（Modifier 列表 + Duration/Period）→ `ApplyEffect(target, effect, ctx)`；第一个消费者=防御力（真实用例验证 Modifier 链）；Instant 改 Current / Duration 到期回 Base 的语义已在本期 Attribute 注释预埋。
2. **Cue 三期**：受击闪红（Enemy.Flash 现在在 Hit 之前、Blocked 也闪——已知旧账）、血粒子、Hit 动画、相机抖动/帧冻结（HitBoxConfig 里参数早就备好）整体迁到事件订阅侧。
3. **玩家死亡表现**：死亡动画已通（PlayerDeathState），缺重生/结算——属战斗逻辑。
4. **敌人头顶血条**：等 AnyChanged + 世界坐标跟随 UI，可复用本期事件。
5. **攻击力之外的属性**：移速（Locomotion）、暴击率——加枚举值+Define 即可，管线零改动（这就是 AttributeSet 的意义）。
6. **可选卫生手术（远期）**：HealthManageComponent 改名 AttributeComponent——新组件+两 prefab 重挂+Entity 字段改名一次做完。纯卫生，功能零变化，闲了再说。
7. **UIDebugMenu 可加**：`Attributes/调试/查看实体属性`（列出目标 Entity 全属性 Base/Current）——迁移期排障利器，可选。

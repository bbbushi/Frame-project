# GAS 属性系统 — 设计与工作逻辑

> 代码：`Assets/Attribute/Core/`（AttributeSet、Attribute、GameplayEffect、ActiveEffect、AttributeChange）+ `Assets/Component/HealthManageComponent.cs`
> 定位：参考 UE GameplayAbilities 改造的一期+二期——一切数值修改的单管线，伤害/治疗/buff/复活全部汇流。
> 前置阅读：《实体与组件模型》（FixedFrameInterval、效果时钟驱动位）

---

## 一、设计思路（为什么这么设计）

**1. 单管线原则（本系统最重要的"为什么"）。**
全项目一切数值修改最终都走 `AttributeSet.Modify → Apply`，不存在第四条路径（GameplayEffect.cs:24 类注释明示）。收益：clamp/判变/事件/死亡标记只实现一次，且行为处处一致。DamageComponent 的减伤公式放战斗组件而非属性集（DamageComponent.cs:46-48），也是为了"属性集保持纯数据"。

**2. Base/Current 分离（GAS 语义预埋）。**
Base=永久值（升级/装备），Current=运行值（伤害/治疗/临时 buff）（Attribute.cs:14-17）。当前阶段 Instant Effect 也只改 Current；"学习/装备"类永久变化才改 Base。MinValue 承载下限（Health=0；MaxHealth=1 防除零；AttackPower=0），Clamp = `Mathf.Max(MinValue, v)`（Attribute.cs:28）。

**3. Additive-only 的 Modifier。**
`struct Modifier { AttributeType type; float magnitude; }`，正=增益负=减损（GameplayEffect.cs:19-23）。简化的关键收益：**加减可逆（逆运算=取负）**——这是"到期按 delta 回退"能成立的前提。

**4. SO 配置 / 运行时实例分离。**
GameplayEffect 是共享只读 SO；ActiveEffect 持运行时状态（remaining 倒计时、_periodTimer、applied 落地记录）（ActiveEffect.cs:5-8）——同一 SO 可同时挂多个实体互不干扰。

**5. Instant 通道 vs Duration+Period 双通道**（GameplayEffect.cs:26-41）：
- `modifiers`：持续型，挂上即生效，到期按记录 delta 回退。Tooltip 警告"别放 Health——到期回退可能把人'回退死'，持续回血/掉血走 periodModifiers"；
- `periodModifiers`：每 period 秒即时生效一次，"逐跳独立、不回退"（中毒/持续回血放这）；
- 回退按**落地 delta**而非配置值："防御下限 clamp 等管线行为不会造成漂移"（ActiveEffect.cs:16-19）。例：Defense=10 时挂 -30 的 Duration，被 min=0 clamp 成落地 -10，到期回 +10 精确归位（按配置回退会漂到 30）。EffectSmokeTest 用例四专测此点。

**6. 事件双通道 + 判变优化。**
实例事件 `Changed`（本实体，血条用）+ 静态总线 `AnyChanged`（全局表现层订阅一次监听所有实体，飘字用）（AttributeSet.cs:27-31）。判变：`Mathf.Approximately(v, currentValue)` 没变就 return false 不发事件——"满血再治疗无事件 → 天然不飘字"。

**7. 死亡语义：不存独立 bool 健康状态。**
`IsDead` 是纯推导（Health.currentValue <= 0，AttributeSet.cs:131）。`_diedField` 死亡标记只在 Apply 里检测 Health **跨 0 线双向**维护（:107-114）：跌破 0 置 true（此后 Modify 拒负增量=死亡免疫）；从 0 拉正置 false（不重置则"死而复生"后永远打不掉血）。行为变化点：旧版 Heal 对死尸 return，新版**正增量放行**——治疗可拉活（"药水复活"语义，:48-49），但不清效果；真复活走 `Revive`（ClearEffects → 死亡标记重置 → 血回 Base，:119-130）。

**8. dt 必须 FixedFrameInterval。**
`TimeScale` 是时间**倍率**（正常时恒 1.0），`FixedFrameInterval = fixedDeltaTime × LocalTimeScale` 才是帧**间隔**。HealthManageComponent.cs:57-58 记录事故：误传 TimeScale → 每物理帧推进 1 秒假时间 → 时钟快 ~50 倍 → 毒瞬间结算。且 LocalTimeScale 已含 GlobalTimeScale（Entity.cs:49-50），子弹时间/全局冻结下效果时钟随实体正确减速。

---

## 二、工作逻辑（怎么运转）

### Modify → Apply 管线分步（AttributeSet.cs:51-56 → 90-116）

```
Modify(type, delta, ctx)
 ├─ 属性未定义 → false
 ├─ type==Health && _diedField && delta<0 → false      [死亡免疫]
 └─ Apply(type, attr, currentValue + delta, ctx)
      ① v = attr.Clamp(newCurrent)                     [先下限]
        Health 特例：再 clamp 到 MaxHealth.current      [后上限，上限取运行值]
      ② Approximately(v, currentValue) → false         [判变：没变零事件]
      ③ 写入 → Changed + AnyChanged（args{owner,type,old,new,delta,context}）
      ④ Health 跨 0 双向维护 _diedField
```
判变在 clamp 之后——clamp 抵消的修改返回 false，ApplyModifier 据此**不记录落地 delta**（:187）。

另两条路径：`SetCurrent`（复活置满/调试，直接 Apply）；`SetBase`（装备改上限）——Base 先 clamp，然后 Current **跟降不跟升**：`Apply(type, attr, Mathf.Min(current, base))`（"Math.Min 才能触发判变"），MaxHealth 变化时 Health 连带 re-clamp（:67-80）。

### GameplayEffect 生效（ApplyEffect，:137-146）

```
ApplyEffect(effect, ctx)
 ├─ active = new ActiveEffect(effect)                  [remaining = duration]
 ├─ foreach modifiers → ApplyModifier → 走全管线；返回 true 才记录落地 delta 到 active.applied
 ├─ Instant → return true（完即走，不进列表）
 └─ Duration → _effects.Add(active)                    [等 Tick 回退]
 目标已死照挂（Revive 会清）
```

### TickEffects 驱动链（毒/buff 的心跳）

```
Entity.FixedUpdate → healthManageComponent.RefreshFixedUpdate (HealthManageComponent.cs:59)
  → _set.TickEffects(FixedFrameInterval)               [dt = 帧间隔]
TickEffects 内部（AttributeSet.cs:151-168）：
  倒序遍历 _effects（到期移除不打乱遍历）
  ① e.ConsumePeriod(dt) → 周期点：foreach periodModifiers → Modify（即时，不记录不回退）
  ② e.remaining -= dt；到期 → 逆序 foreach applied → Modify(type, -delta) → RemoveAt
```
顺序是**先推进周期再判到期**——"同帧挂同帧过期等价于没挂"（Instant 语义兜底）。`ConsumePeriod`（ActiveEffect.cs:26-34）固定步长累减，帧率无关不漂移。

### 死亡入口链与订阅关系

```
Health 跌破 0 → Apply ④ → Changed 事件
  → HealthManageComponent.Init 订阅的 lambda 识别 → OnDied（HealthManageComponent.cs:29-33）
  → Enemy：aiComponent.OnOwnerDied（死亡态）→ Die（回收进池）
  → Player：ActionMachine.ChangeState(Death)
immortal 沙包：Define(Health, maxHP, min: immortal ? 1 : 0) —— 下限锁 1 永不破 0 → 永不 OnDied

表现层订阅（纯事件解耦，互不调用）：
  飘字 FloatingTextManager → 静态总线 AnyChanged，白名单 reason ∈ {Damage, Heal}
  血条 HudPanel          → 实例事件 Changed，过滤 type ∈ {Health, MaxHealth}
```

### 一次完整"被打"的数据流（跨系统串接）

```
Hitbox.HitResultCheck → Hit(target) → Entity.Hit（格挡→Blocked / 无敌→Miss 分流）
 → DamageComponent.Hit：Interrupt + Hit 动画 + 面向攻击方 + ForceMove(hitRepel)
   + AddIgnore(hitRepel.length, All)【硬直门】(DamageComponent.cs:21-34)
 → 减伤 actual = damage × 100/(100+Defense) (DamageComponent.cs:46-48)
 → ApplyDamage → Modify(Health, -actual, Ctx(Damage, source)) → Apply 管线
 → HudPanel 血条 + FloatingTextManager 飘字（显示 |delta| = 防御后的落地值）
```

---

## 三、关键类型速查

| 类型 | 位置 | 职责 |
|---|---|---|
| AttributeType | Attribute/Core/Attribute.cs:5 | Health/MaxHealth/AttackPower/Defense——新属性加枚举值即可 |
| Attribute | Attribute.cs:18 | baseValue/currentValue/MinValue + Clamp |
| Modifier / EffectDurationType | GameplayEffect.cs:18 / :10 | additive-only 修改描述 / Instant·Duration |
| GameplayEffect (SO) | GameplayEffect.cs:26 | durationType/duration/period/modifiers/periodModifiers |
| ActiveEffect | ActiveEffect.cs:9 | 运行时实例：remaining/_periodTimer/applied + ConsumePeriod |
| ChangeReason / ChangeContext | AttributeChange.cs:6 / :10 | Init·Damage·Heal·Revive·Debug·Set·Effect / 谁+为什么 |
| AttributeChangedEventArgs | AttributeChange.cs:24 | owner/type/old/new/delta/context |
| AttributeSet | AttributeSet.cs:14 | 管线核心 + Changed/AnyChanged + ApplyEffect/TickEffects/Revive |
| HealthManageComponent | Component/HealthManageComponent.cs:7 | 属性集宿主：Init 灌值+OnDied 推导；ApplyDamage/Heal/ApplyEffect/Revive |
| EntityCharacterConfig | Config/EntityCharacterConfig.cs:6 | maxHealth/attackDamage/defense/immortal |
| 属性冒烟 / Effect冒烟 | Attribute/Editor/ | 各 9 用例，纯逻辑非 Play 可跑——设计语义的可执行文档 |

---

## 四、坑与约定

1. **持续修饰（modifiers）别放 Health**——到期回退可能把人"回退死"；持续回血/掉血一律走 periodModifiers（SO 字段 Tooltip 有警告）。
2. **TickEffects 的 dt 只能是 FixedFrameInterval**（毒时钟 50 倍案）；驱动位在 Entity.FixedUpdate 链第 5 步，接线勿动。
3. 表现层白名单：飘字只认 Damage/Heal，血条只认 Health/MaxHealth——新 reason（如 Effect）想上飘字要显式加白名单，这是有意设计。
4. 死后负增量被拒、正增量放行——"药水复活"不清效果；要干净重生走 `Revive`。
5. `GuideProgressData` 式的扩展红线在这里不存在，但 Modifier 是 additive-only：想要乘法/覆盖 buff 是二期之后的事，现在加就是破坏"取负可逆"地基。
6. 满血治疗不飘字是判变优化给的免费行为，别在表现层再造"值没变也弹"的逻辑。

## 五、与相邻模块的边界

- 上游《打击盒与动作系统》：伤害唯一落地口是 `ApplyDamage → Modify`，打击盒永不直接改血。
- 上游《实体与组件模型》：FixedFrameInterval 时钟 + RefreshFixedUpdate 驱动位。
- 下游《UI框架》：HudPanel/飘字是纯事件订阅者；`ChangeContext` 是逻辑→表现的唯一语义通道。

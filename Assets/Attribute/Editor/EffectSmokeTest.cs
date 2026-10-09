using System.Collections.Generic;
using Attributes;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Effect 冒烟测试（纯逻辑，不进 Play Mode）：Instant 即走 / Duration 挂载与到期回退 /
/// 落地 delta 防 clamp 漂移 / 重叠独立回退 / 周期跳 / 毒打死与死亡免疫截停 / Revive 清效果 / 满血零事件。
/// 不等真实时间——TickEffects(dt) 手喂假时间推进。SO 用 CreateInstance 现造（编辑态可用），
/// 跑完 DestroyImmediate 回收。AttributeSet 不解引用 owner——照旧传 null。
/// 9 条用例对应《GAS改造二期设计方案》§4.9。
/// </summary>
public static class EffectSmokeTest
{
    private static int _failed;
    private static readonly List<GameplayEffect> _created = new();

    [MenuItem("Attributes/Effect冒烟测试（纯逻辑）")]
    public static void SmokeTest()
    {
        _failed = 0;
        _created.Clear();
        var set = new AttributeSet(null);                    // owner=null，同 AttributeSmokeTest
        set.Define(AttributeType.MaxHealth, 100f, min: 1f);
        set.Define(AttributeType.Health, 100f);
        set.Define(AttributeType.AttackPower, 1f);
        set.Define(AttributeType.Defense, 0f);

        int events = 0;                                      // 全程事件计数：断言「该发才发」
        set.Changed += e => events++;
        try
        {
            // ── 用例一：Instant——值生效即走，不留实例；满血 Health+30 被 clamp 成零事件 ──
            int before = events;
            var fx1 = Instant(Mod(AttributeType.Health, 30f), Mod(AttributeType.Defense, 10f));
            Assert(set.ApplyEffect(fx1, Ctx()), "用例一 Instant ApplyEffect 返回 true");
            Assert(Eq(set.GetCurrentValue(AttributeType.Defense), 10f), "用例一 Defense+10 生效");
            Assert(Eq(set.GetCurrentValue(AttributeType.Health), 100f), "用例一 满血 Health+30 被 clamp（仍 100）");
            Assert(set.ActiveEffects.Count == 0, "用例一 Instant 不留实例（计数 0）");
            Assert(events - before == 1, $"用例一 恰 1 次事件（仅 Defense；满血无事件。实际 {events - before}）");
            set.Modify(AttributeType.Defense, -10f, Ctx());   // 还原：Instant 修改是永久的，不还原会污染后面所有 Defense 用例

            // ── 用例二：Duration 应用（铁壁形状）——Defense=50、计数 1、恰 1 次事件 ──
            before = events;
            var fx2 = Stat(5f, Mod(AttributeType.Defense, 50f));
            Assert(set.ApplyEffect(fx2, Ctx()) && Eq(set.GetCurrentValue(AttributeType.Defense), 50f),
                "用例二 铁壁挂上 Defense=50");
            Assert(set.ActiveEffects.Count == 1, "用例二 计数 1");
            Assert(events - before == 1, $"用例二 应用恰 1 次事件（实际 {events - before}）");

            // ── 用例三：到期回退——Defense=0、计数 0；与用例二合计每效果恰 2 次事件（应用 1+回退 1）──
            before = events;
            set.TickEffects(5f);
            Assert(Eq(set.GetCurrentValue(AttributeType.Defense), 0f) && set.ActiveEffects.Count == 0,
                "用例三 到期回退 Defense=0、计数 0");
            Assert(events - before == 1, $"用例三 回退恰 1 次事件（+用例二的应用 1 = 2 次/效果。实际 {events - before}）");

            // ── 用例四：落地 delta——-30 被 min0 clamp 成落地 -10，回退精确归位（按配置回退会漂到 30）──
            before = events;
            Assert(set.Modify(AttributeType.Defense, 10f, Ctx()) && Eq(set.GetCurrentValue(AttributeType.Defense), 10f),
                "用例四 先把 Defense 抬到 10");
            var fx4 = Stat(1f, Mod(AttributeType.Defense, -30f));
            Assert(set.ApplyEffect(fx4, Ctx()) && Eq(set.GetCurrentValue(AttributeType.Defense), 0f),
                "用例四 -30 被下限 clamp 到 0");
            set.TickEffects(1f);
            Assert(Eq(set.GetCurrentValue(AttributeType.Defense), 10f) && set.ActiveEffects.Count == 0,
                "用例四 回退精确归位 10（落地 delta 语义，无漂移）");
            set.Modify(AttributeType.Defense, -10f, Ctx());   // 还原到 0，给用例五一个干净起点（同上，永久修改要手动退）

            // ── 用例五：重叠独立回退——30(2s)+50(5s)=80；先到期回 50，再到期归 0 ──
            before = events;
            var fxA = Stat(2f, Mod(AttributeType.Defense, 30f));
            var fxB = Stat(5f, Mod(AttributeType.Defense, 50f));
            set.ApplyEffect(fxA, Ctx());
            set.ApplyEffect(fxB, Ctx());
            Assert(Eq(set.GetCurrentValue(AttributeType.Defense), 80f) && set.ActiveEffects.Count == 2,
                "用例五 叠加 30+50=80、计数 2");
            set.TickEffects(2f);                             // 只有 A 到期
            Assert(Eq(set.GetCurrentValue(AttributeType.Defense), 50f) && set.ActiveEffects.Count == 1,
                "用例五 A 到期回 50、B 未受影响");
            set.TickEffects(3f);                             // B 到期
            Assert(Eq(set.GetCurrentValue(AttributeType.Defense), 0f) && set.ActiveEffects.Count == 0,
                "用例五 B 到期归 0");
            Assert(events - before == 4, $"用例五 应用 2+回退 2=4 次事件（实际 {events - before}）");

            // ── 用例六：Period（毒 3 秒每秒-5）——挂载零事件，3 跳到 85，过期不再多跳 ──
            before = events;
            var fx6 = Dot(3f, 1f, Mod(AttributeType.Health, -5f));
            set.ApplyEffect(fx6, Ctx());
            Assert(events == before, "用例六 挂载零事件（periodModifiers 不在挂载时生效）");
            set.TickEffects(1f); set.TickEffects(1f); set.TickEffects(1f);
            Assert(Eq(set.GetCurrentValue(AttributeType.Health), 85f),
                $"用例六 3 跳后 85（实际 {set.GetCurrentValue(AttributeType.Health)}）");
            Assert(set.ActiveEffects.Count == 0 && events - before == 3, $"用例六 恰 3 跳 3 事件、效果已到期（实际 {events - before}）");
            before = events;
            set.TickEffects(0.5f); set.TickEffects(0.5f); set.TickEffects(0.5f); set.TickEffects(0.5f);
            Assert(events == before && Eq(set.GetCurrentValue(AttributeType.Health), 85f), "用例六 已过期不再多跳");

            // ── 用例七：毒打死/免疫截停——8 血两跳毒死；下一跳被死亡免疫拒绝、零事件 ──
            before = events;
            Assert(set.SetCurrent(AttributeType.Health, 8f, Ctx()), "用例七 血量设为 8");
            var fx7 = Dot(5f, 1f, Mod(AttributeType.Health, -5f));
            set.ApplyEffect(fx7, Ctx());
            set.TickEffects(1f);
            Assert(Eq(set.GetCurrentValue(AttributeType.Health), 3f) && !set.IsDead, "用例七 第一跳 8→3 仍活");
            set.TickEffects(1f);
            Assert(set.IsDead && Eq(set.GetCurrentValue(AttributeType.Health), 0f), "用例七 第二跳 3→0 毒死（跨 0 置位）");
            Assert(set.ActiveEffects.Count == 1, "用例七 效果仍挂着（死亡不移除，等 Revive 清）");
            before = events;
            set.TickEffects(1f);
            Assert(events == before && Eq(set.GetCurrentValue(AttributeType.Health), 0f),
                "用例七 下一跳被死亡免疫拒绝、零事件");

            // ── 用例八：Revive 清效果——带毒+铁壁的尸体复活：效果全清、血回满 ──
            var fx8 = Stat(10f, Mod(AttributeType.Defense, 50f));
            set.ApplyEffect(fx8, Ctx());
            Assert(Eq(set.GetCurrentValue(AttributeType.Defense), 50f) && set.ActiveEffects.Count == 2,
                "用例八 死人身上再挂铁壁：Defense=50、计数 2");
            before = events;
            Assert(set.Revive(Ctx()), "用例八 Revive 返回 true");
            Assert(Eq(set.GetCurrentValue(AttributeType.Health), 100f) && !set.IsDead, "用例八 血回满、死亡标记复位");
            Assert(Eq(set.GetCurrentValue(AttributeType.Defense), 0f) && set.ActiveEffects.Count == 0,
                "用例八 效果全清（Defense 归 0、计数 0）");
            Assert(events - before == 2, $"用例八 恰 2 次事件（铁壁回退+复活回满。实际 {events - before}）");

            // ── 用例九：值没变不发事件——满血挂持续回血，第一跳无事发生 ──
            before = events;
            var fx9 = Dot(3f, 1f, Mod(AttributeType.Health, 5f));
            set.ApplyEffect(fx9, Ctx());
            set.TickEffects(1f);
            Assert(Eq(set.GetCurrentValue(AttributeType.Health), 100f) && events == before,
                "用例九 满血第一跳 +5 无事发生（零事件）");
            Assert(set.ActiveEffects.Count == 1, "用例九 效果仍在（没变≠移除）");
        }
        finally
        {
            foreach (var fx in _created) Object.DestroyImmediate(fx);    // 现造的 SO 要回收
        }

        if (_failed == 0) Debug.Log("[Effect冒烟] 全部通过 ✓");
        else Debug.LogError($"[Effect冒烟] {_failed} 项失败，见上方红字");
    }

    // ── 辅助：现造 SO 配置（三种形状对齐 IronWall/毒的用法）──
    private static Modifier Mod(AttributeType type, float magnitude) => new Modifier { type = type, magnitude = magnitude };
    private static GameplayEffect Instant(params Modifier[] mods) => Build(EffectDurationType.Instant, 0f, 0f, mods, null);
    private static GameplayEffect Stat(float duration, params Modifier[] mods) => Build(EffectDurationType.Duration, duration, 0f, mods, null);
    private static GameplayEffect Dot(float duration, float period, params Modifier[] mods) => Build(EffectDurationType.Duration, duration, period, null, mods);

    private static GameplayEffect Build(EffectDurationType kind, float duration, float period, Modifier[] mods, Modifier[] periodMods)
    {
        var fx = ScriptableObject.CreateInstance<GameplayEffect>();
        fx.durationType = kind;
        fx.duration = duration;
        fx.period = period;
        if (mods != null) fx.modifiers.AddRange(mods);
        if (periodMods != null) fx.periodModifiers.AddRange(periodMods);
        _created.Add(fx);
        return fx;
    }

    private static ChangeContext Ctx() => new ChangeContext(ChangeReason.Effect);
    private static bool Eq(float a, float b) => Mathf.Approximately(a, b);

    private static void Assert(bool cond, string label)
    {
        if (!cond) { _failed++; Debug.LogError($"[Effect冒烟] 失败：{label}"); }
    }
}

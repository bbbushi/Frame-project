using System;
using Attributes;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 属性系统冒烟测试（纯逻辑，不进 Play Mode）：
/// clamp / 死亡免疫 / 双向死亡标记 / 事件次数与 payload / 静态总线转发 / 死后治疗拉活。
/// Entity 是 MonoBehaviour，编辑态造不出实例——AttributeSet 不解引用 owner（只在事件参数里携带），
/// 所以 owner 一律传 null，断言不依赖它。9 条用例对应《GAS改造一期设计方案》§4.6。
/// </summary>
public static class AttributeSmokeTest
{
    private static int _failed;

    [MenuItem("Attributes/冒烟测试（纯逻辑）")]
    public static void SmokeTest()
    {
        _failed = 0;
        var set = new AttributeSet(null);                    // owner=null，见类头注释
        set.Define(AttributeType.MaxHealth, 100f, min: 1f);
        set.Define(AttributeType.Health, 100f);

        int instanceEvents = 0, busEvents = 0;               // 用例八的对照计数
        AttributeChangedEventArgs last = default;            // 最近一次事件 payload
        set.Changed += e => { instanceEvents++; last = e; };
        Action<AttributeChangedEventArgs> bus = e => busEvents++;
        AttributeSet.AnyChanged += bus;
        try
        {
            // ── 用例一：Define 初始值（base 同时灌入 current，不发事件）──
            Assert(Eq(set.GetCurrentValue(AttributeType.Health), 100f), "用例一 Define 后血量=100");

            // ── 用例二：Modify -30 → 返回值 / 事件恰 1 次 / payload 三元组 ──
            int before = instanceEvents;
            Assert(set.Modify(AttributeType.Health, -30f, Ctx(ChangeReason.Damage)), "用例二 Modify(-30) 返回 true");
            Assert(Eq(set.GetCurrentValue(AttributeType.Health), 70f),
                $"用例二 血量=70（实际 {set.GetCurrentValue(AttributeType.Health)}）");
            Assert(instanceEvents - before == 1, $"用例二 事件恰 1 次（实际 {instanceEvents - before}）");
            Assert(Eq(last.oldValue, 100f) && Eq(last.newValue, 70f) && Eq(last.delta, -30f),
                $"用例二 payload old/new/delta（实际 {last.oldValue}/{last.newValue}/{last.delta}）");
            Assert(last.context.reason == ChangeReason.Damage, "用例二 payload reason=Damage");

            // ── 用例三：巨额伤害 clamp 到 0，恰 1 次「跌破」事件 ──
            before = instanceEvents;
            Assert(set.Modify(AttributeType.Health, -1000f, Ctx(ChangeReason.Damage))
                && Eq(set.GetCurrentValue(AttributeType.Health), 0f), "用例三 巨额伤害 clamp 到 0");
            Assert(instanceEvents - before == 1 && Eq(last.oldValue, 70f) && Eq(last.newValue, 0f),
                $"用例三 跌破事件恰 1 次 old=70→new=0（实际 {instanceEvents - before} 次 {last.oldValue}→{last.newValue}）");
            Assert(set.IsDead, "用例三 IsDead=true");

            // ── 用例四：死亡免疫——死后再受伤 false 且零事件 ──
            before = instanceEvents;
            Assert(!set.Modify(AttributeType.Health, -10f, Ctx(ChangeReason.Damage)), "用例四 死后 Modify(-10) 返回 false");
            Assert(instanceEvents == before && Eq(set.GetCurrentValue(AttributeType.Health), 0f),
                "用例四 零事件、血量仍 0");

            // ── 用例五：Revive 回满 + 死亡标记已重置（能再次被打死）──
            Assert(set.Revive(Ctx(ChangeReason.Revive)) && Eq(set.GetCurrentValue(AttributeType.Health), 100f),
                "用例五 Revive 回满到 100");
            Assert(set.Modify(AttributeType.Health, -1000f, Ctx(ChangeReason.Damage))
                && Eq(set.GetCurrentValue(AttributeType.Health), 0f), "用例五 复活后可再次跌破（标记已重置）");
            set.Revive(Ctx(ChangeReason.Revive));            // 回满，给用例六铺状态

            // ── 用例六：SetBase(MaxHealth,50) → 血量连带降到 50，两条属性各 1 次事件 ──
            before = instanceEvents;
            Assert(set.SetBase(AttributeType.MaxHealth, 50f, Ctx(ChangeReason.Set))
                && Eq(set.GetCurrentValue(AttributeType.MaxHealth), 50f), "用例六 上限降到 50");
            Assert(Eq(set.GetCurrentValue(AttributeType.Health), 50f),
                $"用例六 血量连带降到 50（实际 {set.GetCurrentValue(AttributeType.Health)}）");
            Assert(instanceEvents - before == 2, $"用例六 MaxHealth+Health 各 1 次事件（实际 {instanceEvents - before}）");

            // ── 用例七：满血治疗——false 且零事件（Apply 规则①：值没变不发）──
            before = instanceEvents;
            Assert(!set.Modify(AttributeType.Health, 10f, Ctx(ChangeReason.Heal)), "用例七 满血 Modify(+10) 返回 false");
            Assert(instanceEvents == before, "用例七 零事件");

            // ── 用例八：静态总线转发无损——全程两计数相等 ──
            Assert(instanceEvents == busEvents && busEvents > 0,
                $"用例八 总线转发无损（instance={instanceEvents} bus={busEvents}）");

            // ── 用例九：死后治疗拉活（药水复活语义）+ 拉活后可再受伤 ──
            set.Modify(AttributeType.Health, -1000f, Ctx(ChangeReason.Damage));   // 先打死
            Assert(set.Modify(AttributeType.Health, 50f, Ctx(ChangeReason.Heal))
                && Eq(set.GetCurrentValue(AttributeType.Health), 50f) && !set.IsDead,
                "用例九 死后 Modify(+50) 拉活成功");
            Assert(set.Modify(AttributeType.Health, -10f, Ctx(ChangeReason.Damage))
                && Eq(set.GetCurrentValue(AttributeType.Health), 40f),
                "用例九 拉活后可再受伤（双向标记复位）");
        }
        finally
        {
            // 静态事件必须退订：不退的话第二次跑 / 进 Play 会双倍计数
            AttributeSet.AnyChanged -= bus;
        }

        if (_failed == 0) Debug.Log("[Attributes冒烟] 全部通过 ✓");
        else Debug.LogError($"[Attributes冒烟] {_failed} 项失败，见上方红字");
    }

    // ── 辅助 ──
    private static ChangeContext Ctx(ChangeReason reason) => new ChangeContext(reason);
    private static bool Eq(float a, float b) => Mathf.Approximately(a, b);

    private static void Assert(bool cond, string label)
    {
        if (!cond) { _failed++; Debug.LogError($"[Attributes冒烟] 失败：{label}"); }
    }
}

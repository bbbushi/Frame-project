using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Guide;

/// <summary>
/// 引导系统调试菜单：
/// - 冒烟测试（纯逻辑，不进 Play Mode）：线性推进 / nextStepId 跳转 / 命令回滚 / 中止路径 / 同引擎二次启动
/// - 开始基础引导 / 重置进度（Play Mode 用）
/// </summary>
public static class GuideDebugMenu
{
    private static int _guideStarted, _stepStarted, _stepCompleted, _guideFinished, _guideAborted, _failed;
    private static readonly List<string> _cmdLog = new();

    [MenuItem("Guide/重置进度")]
    public static void ResetProgress() => Managers.Guide.GuideManager.ResetProgress();

    [MenuItem("Guide/开始基础引导")]
    public static void StartBasics() => Managers.Guide.GuideManager.StartGuide("guide_basics");

    [MenuItem("Guide/冒烟测试（纯逻辑）")]
    public static void SmokeTest()
    {
        _failed = 0;
        var engine = new GuideEngine();
        engine.GuideStarted += _ => _guideStarted++;
        engine.StepStarted += _ => _stepStarted++;
        engine.StepCompleted += _ => _stepCompleted++;
        engine.GuideFinished += _ => _guideFinished++;
        engine.GuideAborted += _ => _guideAborted++;

        // ── 用例一：3 步线性引导；同一引擎跑两轮（专抓 _elapsed 未归零类 bug：二刷瞬间跳步）──
        var linear = CreateGuide("smoke_linear", g =>
        {
            g.Add(AutoStep("s1", 0.1f));
            g.Add(AutoStep("s2", 0.1f));
            g.Add(AutoStep("s3", 0.1f));
        });
        for (int round = 1; round <= 2; round++)
        {
            ResetCounters();
            engine.Start(linear, 0);
            for (int i = 0; i < 50; i++) engine.Tick(0.05f);
            Assert(_guideStarted == 1, $"用例一 第{round}轮 GuideStarted=1（实际 {_guideStarted}）");
            Assert(_stepStarted == 3, $"用例一 第{round}轮 StepStarted=3（实际 {_stepStarted}）");
            Assert(_stepCompleted == 3, $"用例一 第{round}轮 StepCompleted=3（实际 {_stepCompleted}）");
            Assert(_guideFinished == 1, $"用例一 第{round}轮 GuideFinished=1（实际 {_guideFinished}）");
            Assert(_guideAborted == 0, $"用例一 第{round}轮 GuideAborted=0（实际 {_guideAborted}）");
            Assert(!engine.IsRunning, $"用例一 第{round}轮 结束后 IsRunning=false");
        }

        // ── 用例二：nextStepId 跳过中间步骤 + __end__ 提前结束 + 命令 Execute/Revert 配对 ──
        ResetCounters();
        _cmdLog.Clear();
        var jump = CreateGuide("smoke_jump", g =>
        {
            var s0 = AutoStep("s0", 0.05f);
            s0.nextStepId = "s2";                            // 跳过 s1
            s0.onEnterCommands.Add(new ProbeCommand("A"));
            g.Add(s0);

            var s1 = AutoStep("s1", 0.05f);                  // 不应被进入
            s1.onEnterCommands.Add(new ProbeCommand("B"));
            g.Add(s1);

            var s2 = AutoStep("s2", 0.05f);
            s2.nextStepId = GuideStep.EndStepId;             // 显式提前结束
            s2.onEnterCommands.Add(new ProbeCommand("C"));
            g.Add(s2);
        });
        engine.Start(jump, 0);
        for (int i = 0; i < 50; i++) engine.Tick(0.05f);
        Assert(_stepStarted == 2, $"用例二 只进入 2 步（实际 {_stepStarted}）");
        Assert(_guideFinished == 1, $"用例二 GuideFinished=1（实际 {_guideFinished}）");
        Assert(string.Join(",", _cmdLog) == "+A,-A,+C,-C",
            $"用例二 命令序 = +A,-A,+C,-C（实际 {string.Join(",", _cmdLog)}）");

        // ── 用例三：中途 Stop(false) 中止 → 已进入步骤的命令仍被回滚 ──
        ResetCounters();
        _cmdLog.Clear();
        engine.Start(jump, 0);
        for (int i = 0; i < 50; i++)
        {
            engine.Tick(0.05f);
            if (engine.CurrentStep != null && engine.CurrentStep.stepId == "s2") break;   // 进入 s2 后手动中止
        }
        engine.Stop(false);
        Assert(_guideAborted == 1, $"用例三 GuideAborted=1（实际 {_guideAborted}）");
        Assert(_cmdLog.Contains("-C"), $"用例三 中止时 -C 被回滚（实际 {string.Join(",", _cmdLog)}）");

        Object.DestroyImmediate(linear);
        Object.DestroyImmediate(jump);

        if (_failed == 0) Debug.Log("[Guide冒烟] 全部通过 ✓");
        else Debug.LogError($"[Guide冒烟] {_failed} 项失败，见上方红字");
    }

    // ── 辅助 ──
    private static GuideData CreateGuide(string id, System.Action<List<GuideStep>> build)
    {
        var guide = ScriptableObject.CreateInstance<GuideData>();
        guide.guideId = id;
        guide.autoStart = false;
        build(guide.steps);
        return guide;
    }

    private static GuideStep AutoStep(string id, float autoSeconds)
        => new GuideStep
        {
            stepId = id,
            text = $"冒烟步骤 {id}",
            completeCondition = new AutoCompleteCondition { duration = autoSeconds }
        };

    private static void ResetCounters()
        => _guideStarted = _stepStarted = _stepCompleted = _guideFinished = _guideAborted = 0;

    private static void Assert(bool cond, string label)
    {
        if (!cond) { _failed++; Debug.LogError($"[Guide冒烟] 失败：{label}"); }
    }

    /// 测试命令：Execute/Revert 各往 _cmdLog 记一条，用于断言配对与顺序
    private class ProbeCommand : IGuideStepCommand
    {
        private readonly string _tag;
        public ProbeCommand(string tag) => _tag = tag;
        public void Execute(GuideCommandContext ctx) => _cmdLog.Add($"+{_tag}");
        public void Revert(GuideCommandContext ctx) => _cmdLog.Add($"-{_tag}");
    }
}

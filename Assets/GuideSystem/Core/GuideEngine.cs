using System;
using System.Collections.Generic;
using UnityEngine;
namespace Guide
{
    public class GuideEngine
    {

        //----------------------只读数据----------------------
        public bool IsRunning{get; private set;}
        public GuideData CurrentGuide => _guide;
        public GuideStep CurrentStep => IsRunning ? CurrentGuide.steps[CurrentStepIndex] : null;
        public int CurrentStepIndex => _index;

        //-------------------------------

        //------------------事件------------------
        public event Action<GuideData> GuideStarted;
        public event Action<GuideStepContext> StepStarted;
        public event Action<GuideStepContext> StepCompleted;
        public event Action<GuideData> GuideFinished;
        public event Action<GuideData> GuideAborted;

        //-----------------------------------------

        //-------------------------------运行数据-------------------------------
        private Dictionary<string,int>_stepIndexMap;
        private Dictionary<string,object> _currentScratch;
        private GuideData _guide;
        private int _index;
        private float _elapsed;
        private GuideConditionContext _conditionContext;
        //-------------------------------------------------


        /// <summary>
        /// 进入指定的步骤：初始化保存原始数据，更新当前步骤索引，更新guide命令执行上下文
        /// -> 激活当前步骤指令 -> 广播步骤开始事件 -> 更新判定上下文。
        /// </summary>
        /// <param name="index"></param>
        private void EnterStep(int index)
        {
            _currentScratch = new();
            _index = index;
            _elapsed = 0f;
            var step = _guide.steps[index];
            var ctx = new GuideCommandContext
            {
                Step = BuildContext(),
                Scratch = _currentScratch
            };
            // 顺序执行进入命令；SerializeReference 引用会静默丢失，逐个判空防 NRE
            for (int i = 0; i < step.onEnterCommands.Count; i++)
            {
                var cmd = step.onEnterCommands[i];
                if (cmd == null)
                {
                    Debug.LogError($"[Guide] 步骤 {step.stepId} 的命令[{i}] 为 null（引用丢失？），已跳过");
                    continue;
                }
                cmd.Execute(ctx);
            }
            StepStarted?.Invoke(BuildContext());
            if (step.completeCondition == null)
                Debug.LogError($"[Guide] 步骤 {step.stepId} 缺少完成条件，将空等到超时");
            _conditionContext = new GuideConditionContext
            {
                Guide = CurrentGuide,
                Step = step,
                ElapsedSeconds = _elapsed,
                DeltaTime = 0f
            };
        }
        /// <summary>
        /// 退出当前步骤：执行退出命令，清理临时数据，更新判定上下文。
        /// </summary>
        private void ExitStep()
        {
            
            var step = _guide.steps[_index];
            var ctx = new GuideCommandContext
            {
                Step = BuildContext(),
                Scratch = _currentScratch
            };
            // 执行退出命令
            for(int i = step.onEnterCommands.Count - 1; i >= 0; i--)
            {
                var cmd = step.onEnterCommands[i];
                if(cmd != null)
                {
                    cmd.Revert(ctx);
                }
            }
            _currentScratch = null;
            _conditionContext = null;
        }
        /// <summary>
        /// 完成当前步骤：广播步骤完成事件，退出当前步骤，进入下一个步骤
        /// ：如果下一个步骤不存在，则查找下下一步，如果下下一步也不存在，则结束引导。
        /// </summary>
        public void CompleteCurrentStep()
        {
            
            StepCompleted?.Invoke(BuildContext());
            ExitStep();
            string nextId = CurrentStep.nextStepId;
            int next;
            if (string.IsNullOrEmpty(nextId)) next = _index + 1;   // 空 = 顺序
            else if (nextId == GuideStep.EndStepId) next = -1;   // 显式结束
            else if (!_stepIndexMap.TryGetValue(nextId, out next))   next = -1;           // 兜底
            if (next < 0 || next >= _guide.steps.Count) { IsRunning = false; GuideFinished?.Invoke(_guide); }
            else EnterStep(next);
        }
        /// <summary>
        /// 启动引导：校验步骤配置（stepId 非空且唯一、nextStepId 可解析），
        /// 建立跳转索引后触发 GuideStarted 并进入起始步骤。
        /// 配置损坏时只 LogError 不启动，不拖垮调用方。
        /// </summary>
        /// <param name="guide"></param>
        /// <param name="startIndex"></param>
        public void Start(GuideData guide, int startIndex)
        {
            if (guide == null || guide.steps == null || guide.steps.Count == 0)
            {
                Debug.LogError("[Guide] Start 失败：引导为 null 或没有步骤");
                return;
            }
            if (IsRunning) Stop(false);

            _guide = guide;
            _stepIndexMap = new();
            bool valid = true;

            // 第一遍：建 stepId → index 索引，校验 stepId 非空且唯一
            for (int i = 0; i < guide.steps.Count; i++)
            {
                var step = guide.steps[i];
                if (string.IsNullOrEmpty(step.stepId))
                {
                    Debug.LogError($"[Guide] 引导 {guide.guideId} 步骤[{i}] stepId 为空");
                    valid = false;
                    continue;
                }
                if (_stepIndexMap.ContainsKey(step.stepId))
                {
                    Debug.LogError($"[Guide] 引导 {guide.guideId} stepId 重复: {step.stepId}");
                    valid = false;
                    continue;
                }
                _stepIndexMap[step.stepId] = i;
            }

            // 第二遍：校验 nextStepId（索引建好后才能解析前向跳转）
            foreach (var step in guide.steps)
            {
                if (string.IsNullOrEmpty(step.nextStepId) || step.nextStepId == GuideStep.EndStepId) continue;
                if (!_stepIndexMap.ContainsKey(step.nextStepId))
                {
                    Debug.LogError($"[Guide] 引导 {guide.guideId} 步骤 {step.stepId} 的 nextStepId 指向了不存在的步骤 {step.nextStepId}");
                    valid = false;
                }
            }

            if (!valid) return;

            if (startIndex < 0 || startIndex >= guide.steps.Count)
            {
                Debug.LogWarning($"[Guide] 引导 {guide.guideId} 起始步骤 {startIndex} 越界，从头开始");
                startIndex = 0;
            }

            GuideStarted?.Invoke(guide);
            IsRunning = true;
            EnterStep(startIndex);
        }
        /// <summary>
        /// 构建当前步骤的UI上下文快照
        /// </summary>
        /// <returns></returns>
        private GuideStepContext BuildContext() => new GuideStepContext()
        {
            GuideId = CurrentGuide.guideId,
            StepId = CurrentStep.stepId,
            Text = CurrentStep.text,
            TargetKey = CurrentStep.targetKey,
            ConditionText = CurrentStep.completeCondition?.Describe() ?? "无条件",
            StepIndex = CurrentStepIndex,
            StepCount = CurrentGuide.steps.Count
        };
        /// <summary>
        /// 更新引导状态：处理每个帧的逻辑，包括时间流逝、条件检查和步骤切换：
        /// 如果当前步骤超时，则根据超时动作进行处理。
        /// </summary>
        /// <param name="deltaTime"></param>
        public void Tick(float deltaTime)
        {
            if(!IsRunning) return;   
            _elapsed+=deltaTime;
            _conditionContext.DeltaTime = deltaTime;
            _conditionContext.ElapsedSeconds = _elapsed;
            if(CurrentStep.timeoutSeconds > 0 && _elapsed >= CurrentStep.timeoutSeconds)
            {
                if(CurrentStep.timeoutAction == GuideTimeOutAction.AbortGuide)
                {
                    Stop(false);
                }
                else
                {
                    CompleteCurrentStep();
                }
                return;
            }
            if (CurrentStep.completeCondition == null) return;    // 可选：首次 LogError 提示防死锁
            if (CurrentStep.completeCondition.IsSatisfied(_conditionContext)) CompleteCurrentStep();
        }
        /// <summary>
        /// 停止当前引导：根据参数决定是否标记当前步骤为完成，然后停止引导。
        /// </summary>
        /// <param name="markCompleted"></param>
        public void Stop(bool markCompleted)
        {
            if(!IsRunning) return;
            if(markCompleted) StepCompleted?.Invoke(BuildContext());
            ExitStep();
            IsRunning = false;
            if(markCompleted) GuideFinished?.Invoke(CurrentGuide);
            else GuideAborted?.Invoke(CurrentGuide);   
        }
    }
}
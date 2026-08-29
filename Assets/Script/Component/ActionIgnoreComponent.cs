using ActComponents;
using System.Collections.Generic;
using UnityEngine;
namespace Components
{
    public class ActionIgnoreComponent : EntityComponent
    {
        public LinkedList<ActionIgnore> actionIgnores;

        public void RefreshActionIgnore()
        {
            for (var node = actionIgnores.First; node != null;)
            {
                //先保存下一个节点：Remove 会把节点的 Next 置空，移除后再取就丢了
                var next = node.Next;

                //动作忽略标签自减
                node.Value.timer -= FixedFrameInterval;

                //移除到期的忽略标签
                if (node.Value.timer <= 0)
                    actionIgnores.Remove(node);

                node = next;
            }
        }
        public bool IsIgnore(ActionIgnoreTag tag)
        {
            foreach (ActionIgnore ignore in actionIgnores)
            {
                if (ignore.mask.ContainTag(tag))
                    return true;
            }
            return false;
        }
        public void AddIgnore(float time, params ActionIgnoreTag[] actionIgnoreTags)
        {
            ActionIgnoreMask mask = ActionIgnoreMask.GetMask(actionIgnoreTags);
            bool hasIgnore = false;
            foreach (ActionIgnore ignore in actionIgnores)
            {
                if (ignore.mask == mask)
                {
                    //时间选一个更长的
                    if (ignore.timer <= time)
                        ignore.timer = time;
                    //不再生成新的Ignore
                    hasIgnore = true;
                }
            }
            if (!hasIgnore)
                actionIgnores.AddFirst(new ActionIgnore(mask, time));
        }
        
        public override void Init()
        {
            base.Init();
            actionIgnores = new LinkedList<ActionIgnore>();
        }
    }
}
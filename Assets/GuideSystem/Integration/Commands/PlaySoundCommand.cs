using System;
using Guide;
namespace Managers.Guide
{
    /// 步骤开始时播放一条提示音（sfxId 对应 SFXManagerConfig 里的音效配置）。
    /// 播音效无需回滚，Revert 留空
    [Serializable]
    public class PlaySoundCommand : IGuideStepCommand
    {
        public string sfxId;   // SFXManagerConfig 中配置的音效 id
        public void Execute(GuideCommandContext ctx)
            => GameManager.Get<SFXManager>()?.PlaySFX(sfxId);
        public void Revert(GuideCommandContext ctx) { }
    }
}

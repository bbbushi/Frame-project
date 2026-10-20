using System;
using UnityEngine;
using Managers;
using Config;

namespace Modules
{
    /// <summary>
    /// 子弹时间模块 — 右键开关全局慢动作（SlowScale 因子）。
    /// 处决链（瞄准/标记/ExecuteChain）待后续迭代补回，参考 git 7e29547^ 旧实现。
    /// </summary>
    [Serializable]
    public class PlayerBulletTime : PlayerModule
    {
        [SerializeField] private float bulletTimeScale = 0.2f;
        // 过渡时长沿用旧实现硬编码：进 0.15s / 出 0.3s
        private const float EnterDuration = 0.15f;
        private const float ExitDuration = 0.3f;

        public bool IsActive { get; private set; }

        /// <summary>加载子弹时间配置</summary>
        public void LoadConfig(PlayerControllerData cfg)
        {
            if (cfg == null) return;
            bulletTimeScale = cfg.bulletTimeScale;
        }

        public void Toggle()
        {
            if (IsActive) CancelBulletTime();
            else EnterBulletTime();
        }

        public void EnterBulletTime()
        {
            IsActive = true;
            TimeManager.SetSlowScaleSmooth(bulletTimeScale, EnterDuration);
        }

        public void CancelBulletTime()
        {
            if (!IsActive) return;
            IsActive = false;
            TimeManager.SetSlowScaleSmooth(1f, ExitDuration);
        }
    }
}

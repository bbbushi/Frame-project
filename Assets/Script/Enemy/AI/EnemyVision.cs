using Config;
using UnityEngine;

namespace enemy.ai
{
    /// <summary>
    /// 感知器：前向视野盒 + 视线校验 + 目击记忆。纯 C#，只认 (Entity, Config)，
    /// 不知道状态机存在——可脱离游戏单测。
    /// </summary>
    public class EnemyVision
    {
        public Entity Target { get; private set; }             // 记忆目标：LOS 断了不清空，由 Chase 决定何时 Forget
        public Vector3 LastKnownPosition { get; private set; }
        public bool HasLineOfSightNow { get; private set; }

        readonly Entity owner;
        readonly EnemyAIConfig cfg;
        float pollTimer;

        public EnemyVision(Entity owner, EnemyAIConfig cfg)
        {
            this.owner = owner;
            this.cfg = cfg;
        }

        /// <summary>宿主每物理帧喂 dt；内部按 perceptionInterval 轮询，省物理查询</summary>
        public void Tick(float dt)
        {
            pollTimer -= dt;
            if (pollTimer > 0f) return;
            pollTimer = cfg.perceptionInterval;

            // 前向视野盒（中心沿面朝方向偏移半个视野宽 → 背后自然看不见）
            Vector2 center = (Vector2)owner.transform.position
                + new Vector2(owner.locomotionComponent.FacingDirection * cfg.visionRange * 0.5f, 0.5f);
            var hit = Physics2D.OverlapBox(center, new Vector2(cfg.visionRange, cfg.visionHeight), 0f,
                LayerMask.GetMask("player"));
            if (hit != null)
            {
                var player = hit.GetComponentInParent<Entity>();
                if (player != null && owner.HasLineOfSight(player))   // Entity.HasLineOfSight 现成
                {
                    Target = player;
                    LastKnownPosition = player.transform.position;
                    HasLineOfSightNow = true;
                    return;
                }
            }
            HasLineOfSightNow = false;   // Target 保留——记忆期由 Chase 用 LastKnownPosition 追最后目击点
        }

        public void Forget() => Target = null;

        /// <summary>池化重置：清记忆、清视线、下帧立即轮询</summary>
        public void Reset() { Forget(); HasLineOfSightNow = false; pollTimer = 0f; }
    }
}

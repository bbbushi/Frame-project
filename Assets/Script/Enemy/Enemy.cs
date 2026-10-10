using UnityEngine;
using ActComponents;
using Managers;
using enemy.ai;
namespace enemy
{
    /// <summary>
    /// 简易测试敌人 — 挂到带 Collider2D + SpriteRenderer 的 GameObject 上即可用。
    /// 实现 IPoolable 以支持 ObjectPoolManager 池化复用。
    /// </summary>
    public class Enemy : Entity, IPoolable
    {
        public EnemyAIComponent aiComponent;    // enemy.ai 宿主，prefab 根上挂 EnemyAIComponent
        protected override void Awake()
        {
            base.Awake();
            aiComponent = GetComponentInChildren<EnemyAIComponent>();
        }
        protected override void Start()
        {
            base.Start();
            if (aiComponent != null) aiComponent.Init();    // 装配状态机（在所有组件 Init 之后）
        }
        protected override void Update()
        {
            base.Update();
            
        }
        protected override void FixedUpdate()
        {
            base.FixedUpdate();
            if (aiComponent != null) aiComponent.RefreshFixedUpdate();    // AI 决策在组件刷新之后
        }
        private void Die()
        {
            // 优先走对象池回收（非池对象时 Release 内部回退为 Destroy，行为与直接销毁等价）
            if (ObjectPoolManager.Instance != null)
                ObjectPoolManager.Instance.Release(gameObject);
            else
                Destroy(gameObject);
        }

        /// <summary>从池中取出时重置战斗状态（IPoolable）</summary>
        public void OnSpawnFromPool()
        {
            if (healthManageComponent != null) healthManageComponent.Revive(); // 回满血
            if (aiComponent != null) aiComponent.ResetAI();   // 状态回根、重锚 HomeAnchor、清目标记忆
            ClearBattleTimers();
            if (spriteRenderer != null) spriteRenderer.color = Color.white;
        }

        /// <summary>回收到池时清理状态（IPoolable）</summary>
        public void OnReturnToPool()
        {
            StopAllCoroutines(); // 终止 Flash 协程等
            ClearBattleTimers();
            if (rb != null) rb.velocity = Vector2.zero; // 清残留物理速度
            if (spriteRenderer != null) spriteRenderer.color = Color.white;
        }

        private void Flash()
        {
            if (spriteRenderer != null)
                StartCoroutine(FlashRoutine());
        }

        private System.Collections.IEnumerator FlashRoutine()
        {
            spriteRenderer.color = Color.red;
            yield return new WaitForSeconds(0.1f);
            spriteRenderer.color = Color.white;
        }
        public override HitResult Hit(Damage damage)
        {
            Flash();
            return base.Hit(damage);
        }
        // 池化对象的事件订阅标准位：每次从池中取出（OnEnable）都重新订，回池（OnDisable）退订——
        // 放 Start/Awake 的话，池化复用第二只起就收不到事件了
        private void OnEnable()
        {
            if (healthManageComponent != null)
            {
                if (aiComponent != null) healthManageComponent.OnDied += aiComponent.OnOwnerDied; // 先进死亡态（停走/播倒地）
                healthManageComponent.OnDied += Die;                                              // 再回收进池
            }
        }
        private void OnDisable()
        {
            if (healthManageComponent != null)
            {
                if (aiComponent != null) healthManageComponent.OnDied -= aiComponent.OnOwnerDied;
                healthManageComponent.OnDied -= Die;
            }
        }
    }
}


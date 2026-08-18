using UnityEngine;
using ActComponents;
using Managers;
namespace enemy
{
    /// <summary>
    /// 简易测试敌人 — 挂到带 Collider2D + spriteRendererRenderer 的 GameObject 上即可用于子弹时间标记测试。
    /// 实现 IPoolable 以支持 ObjectPoolManager 池化复用。
    /// </summary>
    public class Enemy : Entity, IPoolable
    {
        
        private GameObject markIndicator;
        private Timer RemTimer;
        public bool playerInRange;
        protected override void Awake()
        {
            base.Awake();
            
            TimerEvent tickEvent = () => { Move(); };
            TimerEvent timerOutEvent = () => { };
            RemTimer = new Timer(this, TimerType.fixedDelta, timerOutEvent, tickEvent, false, 0.1f);
        }
        protected override void Start()
        {
            base.Start();
            Wait();
        }
        protected override void Update()
        {
            base.Update();
            
        }
        protected override void FixedUpdate()
        {
            base.FixedUpdate();
            // Detection();
        }
        private bool IsplayerInRange() => Physics2D.OverlapBox(transform.position, new Vector2(5f, 2f), 0f, LayerMask.GetMask("player")) != null;
        private Transform GetPlayerTransform() => Physics2D.OverlapBox(transform.position, new Vector2(5f, 2f), 0f, LayerMask.GetMask("player"))?.transform;
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(transform.position, new Vector3(5f, 2f, 0f));
        }
        private void Wait()
        {
            
            anim.SetBool("idle", true);
            RemTimer.Cancel();
            locomotionComponent.Stop();
        }
        // private void Detection()
        // {
        //     if (detection.IsOnGround && detection.IsFacingWall)
        //     {
        //         locomotionComponent.Flip();
        //         locomotionComponent.ApplyHorizontal(locomotionComponent.FacingDirection);
        //     }
        //     if (IsplayerInRange())
        //     {
        //         var player = GetPlayerTransform();
        //         if (player == null) return;
        //         if(locomotionComponent.FacingDirection * (player.position.x - transform.position.x) < 0)
        //         {
        //             locomotionComponent.Flip();
        //         }
                
        //         playerInRange = true;
        //         if(!IsIgnore(ActionIgnoreTag.Move) && !IsIgnore(ActionIgnoreTag.All))
        //         {
        //             RemTimer.Set(3f);
        //             Move();
        //         }
                
        //     }
        //     else
        //     {
        //         playerInRange = false;
        //         if(RemTimer.Time < 0.1f)
        //         {
        //             anim.SetBool("Is Move", false);
        //             Wait();
        //         }
        //     }
        // }
        private void Move()
        {
            
            // anim.SetBool("idle", false);
            // anim.SetBool("Is Move", true);
            // locomotionComponent.ApplyHorizontal(locomotionComponent.FacingDirection);
            
            
        }
        /// <summary>显示标记编号（子弹时间瞄准时调用）</summary>
        public void ShowMark(int index)
        {
            ClearMark();

            markIndicator = new GameObject("Mark");
            markIndicator.transform.SetParent(transform);
            markIndicator.transform.localPosition = new Vector3(0, 1.2f, 0);
            var text = markIndicator.AddComponent<TextMesh>();
            text.text = index.ToString();
            text.fontSize = 64;
            text.characterSize = 0.15f;
            text.anchor = TextAnchor.MiddleCenter;
            text.color = Color.yellow;
            text.fontStyle = FontStyle.Bold;
        }

        /// <summary>清除标记图标</summary>
        public void ClearMark()
        {
            if (markIndicator != null)
            {
                Destroy(markIndicator);
                markIndicator = null;
            }
        }

        private void Die()
        {
            RemTimer.Destroy();
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
            ClearMark();
            ClearBattleTimers();
            if (spriteRenderer != null) spriteRenderer.color = Color.white;
        }

        /// <summary>回收到池时清理状态（IPoolable）</summary>
        public void OnReturnToPool()
        {
            ClearMark();
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
    }
}


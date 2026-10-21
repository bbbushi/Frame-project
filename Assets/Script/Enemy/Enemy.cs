using UnityEngine;
using ActComponents;
using Managers;
using enemy.ai;
namespace enemy
{
    /// <summary>
    /// 简易测试敌人 — 挂到带 Collider2D + SpriteRenderer 的 GameObject 上即可用。
    /// 实现 IPoolable 以支持 ObjectPoolManager 池化复用、IMarkable 支持子弹时间标记。
    /// </summary>
    public class Enemy : Entity, IPoolable, IMarkable
    {
        public EnemyAIComponent aiComponent;    // enemy.ai 宿主，prefab 根上挂 EnemyAIComponent
        private GameObject _markLabel;          // 子弹时间头顶编号（ShowMark 创建）

        /// <summary>可被子弹时间标记（后续可加死亡/无敌态判断）</summary>
        public bool CanBeMarked => true;

        /// <summary>头顶显示标记编号 — 独立于敌人的世界对象（敌人转身是 localScale 翻转，挂子物体会镜像数字），位置由 Update 同步</summary>
        public void ShowMark(int index)
        {
            ClearMark();
            if (spriteRenderer == null) return;
            var go = new GameObject("MarkLabel");
            var tmp = go.AddComponent<TMPro.TextMeshPro>();
            tmp.text = index.ToString();
            tmp.fontSize = 4;
            tmp.alignment = TMPro.TextAlignmentOptions.Center;
            tmp.color = Color.yellow;
            // TMP 3.0.6 只有 int 版排序 API（无 sortingLayerName 字符串版）
            tmp.sortingLayerID = UnityEngine.SortingLayer.NameToID("BulletTimeTop");
            tmp.sortingOrder = 200;
            _markLabel = go;
            SyncMarkPosition();
        }

        public void ClearMark()
        {
            if (_markLabel != null) Destroy(_markLabel);
            _markLabel = null;
        }

        private void SyncMarkPosition()
        {
            if (_markLabel == null || spriteRenderer == null) return;
            Bounds b = spriteRenderer.bounds;
            _markLabel.transform.position = new Vector3(b.center.x, b.max.y + 0.3f, b.center.z);
        }
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
            SyncMarkPosition();    // 标记编号跟随头顶（独立对象，不随转身翻转）
        }
        protected override void FixedUpdate()
        {
            base.FixedUpdate();
            if (aiComponent != null) aiComponent.RefreshFixedUpdate();    // AI 决策在组件刷新之后
        }
        private void Die()
        {
            ClearMark();   // 编号图标是独立对象，不随本体销毁——死亡入口统一清除
            // 优先走对象池回收（非池对象时 Release 内部回退为 Destroy，行为与直接销毁等价）
            if (ObjectPoolManager.Instance != null)
                ObjectPoolManager.Instance.Release(gameObject);
            else
                Destroy(gameObject);
        }

        private void OnDestroy()
        {
            ClearMark();   // 兜底：非 Die 路径的销毁（场景卸载/手动 Destroy）也不留编号
        }

        /// <summary>从池中取出时重置战斗状态（IPoolable）</summary>
        public void OnSpawnFromPool()
        {
            healthManageComponent?.Revive(); // 回满血
            aiComponent?.ResetAI();          // 状态回根、重锚 HomeAnchor、清目标记忆
            ClearBattleTimers();
            ClearMark();                      // 清残留的子弹时间标记
            if (spriteRenderer != null) spriteRenderer.color = Color.white;
        }

        /// <summary>回收到池时清理状态（IPoolable）</summary>
        public void OnReturnToPool()
        {
            StopAllCoroutines(); // 终止 Flash 协程等
            ClearBattleTimers();
            ClearMark();                      // 清子弹时间标记图标
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


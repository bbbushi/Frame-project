using UnityEngine;
namespace Components
{
    public class DetectionComponent : EntityComponent 
    {
        
        [SerializeField]private Collider2D WallDetectionCollider;
        [SerializeField]private Collider2D GroundDetectionCollider; 
        [SerializeField]private Collider2D moveCollider;
        [SerializeField]private Collider2D hitCollider;
        [SerializeField]private Collider2D platformSensor;
        public GameObject hitboxPrefab;
        [SerializeField]private Transform attackSocket;
        public Transform GetAttackSocket() => attackSocket;
        public bool IsOnGround => GroundDetectionCollider != null && GroundDetectionCollider.IsTouchingLayers(LayerMask.GetMask("Ground"));
        public bool IsFacingWall => WallDetectionCollider != null && WallDetectionCollider.IsTouchingLayers(LayerMask.GetMask("Wall"));

        /// <summary>悬崖前探：身前 distance 处的脚下还有没有地面（判「再走会掉」，IsOnGround 是「已经掉了」）。
        /// 用向下射线而非 OverlapPoint 探点——胶囊底在地表 y-0.5，探点压在地表平面上永远 miss
        /// （实测案：每帧掉头来回抽搐）。Ground/Platform 都算落脚面，巡逻不走下平台边缘</summary>
        public bool IsGroundAhead(float distance = 0.5f)
        {
            float facing = Owner.locomotionComponent.FacingDirection;
            Vector2 origin = (Vector2)Owner.transform.position + new Vector2(facing * distance, -0.2f);
            return Physics2D.Raycast(origin, Vector2.down, 0.8f,
                LayerMask.GetMask("Ground", "Platform")).collider != null;
        }
        public bool isTouchingPlatform => platformSensor != null && platformSensor.IsTouchingLayers(LayerMask.GetMask("Platform"));
        public override void RefreshFixedUpdate()
        {
            base.RefreshFixedUpdate();
            RefreshPlatformPenetrate();
            
        }
        /// <summary>
        /// 是否可以穿透平台（Platform），用于跳跃时允许穿越平台，或者在强制穿越计时器内允许穿透所有平台
        /// </summary>
        public bool CanPenetratePlatform
        {
            get
            {
                return platformSensor.gameObject.layer == LayerMask.NameToLayer("CharacterIgnorePlatform");
            }
            protected set
            {
                if (value)
                {
                    platformSensor.gameObject.layer = LayerMask.NameToLayer("CharacterIgnorePlatform");
                }
                else
                {
                    platformSensor.gameObject.layer = LayerMask.NameToLayer("Character");
                }
            }
        }
        float platformPenetrateTimer;
        public void SetPlatformPenetrateTime(float time)
        {
            platformPenetrateTimer = time;
            CanPenetratePlatform = true;
        }

        //平台穿越
        public void RefreshPlatformPenetrate()
        {
            bool canPenetrate = false;
            //强制穿越计时器，在计时器内允许穿透所有平台
            platformPenetrateTimer -= FixedFrameInterval;
            if (platformPenetrateTimer > 0)
                canPenetrate = true;
            //速度朝上时，允许穿透所有平台
            if (Owner.rb.velocity.y > 1f)
                canPenetrate = true;
            //Platform Sensor与平台相交时，始终允许穿越平台
            if (isTouchingPlatform)
                canPenetrate = true;

            if (canPenetrate)
                CanPenetratePlatform = true;
            else
                CanPenetratePlatform = false;
        }
        #if UNITY_EDITOR
        protected virtual void OnDrawGizmos()
        {
            
            if (hitCollider != null)
            {
                Gizmos.color = Color.blue;
                Gizmos.DrawWireCube(hitCollider.bounds.center, hitCollider.bounds.size * 0.98f);
            }
            if(WallDetectionCollider != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireCube(WallDetectionCollider.bounds.center, WallDetectionCollider.bounds.size * 0.98f);
            }
            if(GroundDetectionCollider != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireCube(GroundDetectionCollider.bounds.center, GroundDetectionCollider.bounds.size * 0.98f);
            }
            if(platformSensor != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireCube(platformSensor.bounds.center, platformSensor.bounds.size * 0.98f);
            }
        }
        #endif
        
        
    }    
}

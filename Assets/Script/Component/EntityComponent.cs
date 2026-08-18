using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SocialPlatforms;
using Managers;
namespace Components
{
    public class EntityComponent : MonoBehaviour
    {
        Entity owner;
        bool ownerMissingWarned; // 仅首次警告，避免每帧刷屏
        protected virtual Entity Owner
        {
            get
            {
                if (owner == null)
                {
                    owner = transform.GetComponentInParent<Entity>();
                    if (owner == null && !ownerMissingWarned)
                    {
                        ownerMissingWarned = true;
                        Debug.LogWarning($"[EntityComponent] 未在父级找到 Entity（{gameObject.name}），时间属性退化为全局时间");
                    }
                }
                return owner;
            }
        }


        // Owner 缺失时降级到全局时间，而非 NRE
        public float TimeScale => Owner != null ? Owner.TimeScale : TimeManager.GlobalTimeScale;
        //帧间隔
        public float FixedFrameInterval => Owner != null ? Owner.FixedFrameInterval : Time.fixedDeltaTime;

        public float FrameInterval => Owner != null ? Owner.FrameInterval : Time.deltaTime;

        public virtual void Init() { }
        public virtual void RefreshUpdate() { }
        public virtual void RefreshFixedUpdate() { }
        public virtual void Interrupt() { }

    }
}


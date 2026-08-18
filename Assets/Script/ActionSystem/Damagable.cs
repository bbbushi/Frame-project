using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace ActComponents
{
    /// <summary>
    /// 可受击接口
    /// </summary>
    public interface iDamagable
    {
        public HitResult Hit(Damage damage);

        public Faction Faction { get; }
        public Vector2 HitboxCenter { get; }
    }


    public class DamagableBox : MonoBehaviour, iDamagable
    {
        public Vector3 Position => transform.position;

        public HitResult Hit(Damage damage)
        {
            return null;
        }

        public Faction Faction
        {
            get;
        }

        public Vector2 HitboxCenter { get => Position; }

        public float GetDistance(Entity character)
        {
            return GetDistance(character.ChestPosition);
        }
        public float GetDistance(Vector3 target)
        {
            return (target - Position).magnitude;
        }
    }
}
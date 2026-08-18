using UnityEngine;
namespace Config
{
    [CreateAssetMenu(fileName = "NewHitBoxData", menuName = "Game/HitBox Data")]
    
    public class HitBoxConfig : ScriptableObject
    {
        #region 常量数据
        //————通用————
        //是否有友军伤害
        public bool hasFriendlyDamage;       
        //最大持续时间
        public float maxExistTime;
        //————伤害与冲击力————

        //击退冲量
        public float impulse;
        //伤害类型
        public DamageType damageType = DamageType.Melee;
        //冲击力类别
        public ImpactType impactType = ImpactType.None;
        //冲击力方式（平行、离心）
        public ImpulseType impulseType = ImpulseType.Parallel;

        //————打击反馈————
        //相机抖动幅度
        public float cameraShakeMagnitude = 0;
        //相机抖动次数
        public int cameraShakeRepeat = 0;
        //相机抖动时长
        public float cameraShakeTime = 0;
        //帧冻结时长
        public float frameFreezeLength = 0;
        #endregion
        
    }
}
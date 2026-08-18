using System.Collections.Generic;
using UnityEngine;
namespace Config{
    /// <summary>
    /// SFXManager 配置（ScriptableObject）。
    /// 在 Unity Editor 中创建资产并放入 Assets/Resources/ 目录，
    /// SFXManager 初始化时会自动加载。
    /// </summary>
    [CreateAssetMenu(fileName = "SFXManagerConfig", menuName = "Game/SFX Manager Config")]
    public class SFXManagerConfig : ScriptableObject
    {
        [Header("Audio Source Pool")]
        [Min(1)] public int initialPoolSize = 8;
        [Min(1)] public int maxPoolSize = 24;
        [Range(0f, 1f)] public float spatialBlend = 0f;

        [Header("Lifecycle")]
        public bool dontDestroyOnLoad = true;

        [Header("SFX Bank")]
        public List<SFXBankEntry> sfxBank = new();

        [Header("Footstep")]
        public LayerMask footstepGroundMask = -1;
        [Min(0.01f)] public float footstepRayDistance = 0.3f;
        public List<FootstepSurfaceEntry> footstepSurfaces = new();
        public AudioClip[] defaultFootstepClips;
        [Range(0f, 1f)] public float defaultFootstepMinVolume = 0.9f;
        [Range(0f, 1f)] public float defaultFootstepMaxVolume = 1f;
        [Range(-3f, 3f)] public float defaultFootstepMinPitch = 0.95f;
        [Range(-3f, 3f)] public float defaultFootstepMaxPitch = 1.05f;

        [Header("Debug")]
        public bool verboseLog;
        public bool drawFootstepRay;
    }
}
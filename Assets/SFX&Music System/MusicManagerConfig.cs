using UnityEngine;

/// <summary>
/// MusicManager 配置（ScriptableObject）。
/// 在 Unity Editor 中创建资产并放入 Assets/Resources/ 目录，
/// MusicManager 初始化时会自动加载。
/// </summary>
[CreateAssetMenu(fileName = "MusicManagerConfig", menuName = "Game/Music Manager Config")]
public class MusicManagerConfig : ScriptableObject
{
    [Header("Music Clips")]
    public AudioClip normalMusic;
    public AudioClip startMusic;
    public AudioClip fightMusic;

    [Header("Cross-fade")]
    [Min(0.01f)] public float defaultFadeDuration = 0.5f;
}

using System;
using UnityEngine;

[Serializable]
public class SFXBankEntry
{
    public string id;
    public AudioClip[] clips;
    [Range(0f, 1f)] public float minVolume = 1f;
    [Range(0f, 1f)] public float maxVolume = 1f;
    [Range(-3f, 3f)] public float minPitch = 1f;
    [Range(-3f, 3f)] public float maxPitch = 1f;

    public AudioClip GetRandomClip()
    {
        if (clips == null || clips.Length == 0) return null;
        return clips[UnityEngine.Random.Range(0, clips.Length)];
    }

    public float GetRandomVolume() =>
        UnityEngine.Random.Range(Mathf.Min(minVolume, maxVolume), Mathf.Max(minVolume, maxVolume));

    public float GetRandomPitch() =>
        UnityEngine.Random.Range(Mathf.Min(minPitch, maxPitch), Mathf.Max(minPitch, maxPitch));
}

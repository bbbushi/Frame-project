using UnityEngine;
public static class FlowSpawn
{
    /// <summary>
    /// sceneLoaded 后调用：场景里有 SpawnPoint 就把玩家校准过去（玩家 prefab 化前的兼容落法：
    /// 场景继续摆玩家，SpawnPoint 负责"摆哪都行，反正会被校正"）
    /// </summary>
    public static void Calibrate()
    {
        var sp = Object.FindFirstObjectByType<SpawnPoint>();
        var player = Object.FindFirstObjectByType<PlayerSystem.Player>();
        if (sp != null && player != null)
            player.transform.SetPositionAndRotation(sp.transform.position, sp.transform.rotation);
    }
}

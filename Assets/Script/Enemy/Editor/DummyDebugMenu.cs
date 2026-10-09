using UnityEditor;
using UnityEngine;
using Config;
using PlayerSystem;
using enemy;

/// <summary>
/// 沙包调试菜单：在玩家面前生成不倒翁敌人（血量下限锁 1，打死不掉），专测打击手感。
/// 配置是运行时内存实例（血 9999 / 防 0，裸伤害看得清）——不建资产，Play 停止即随场景销毁。
/// </summary>
public static class DummyDebugMenu
{
    const string EnemyPrefabPath = "Assets/Asset/Prefab/Enemy.prefab";

    [MenuItem("AI/沙包/玩家前方生成（不倒翁）")]
    public static void SpawnDummy()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[沙包] 请先进 Play Mode 再使用此菜单");
            return;
        }
        if (Player.Instance == null)
        {
            Debug.LogWarning("[沙包] 场景里没有 Player");
            return;
        }
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabPath);
        if (prefab == null)
        {
            Debug.LogWarning($"[沙包] 找不到 {EnemyPrefabPath}（路径改了的话同步菜单里的常量）");
            return;
        }

        // 菜单回调属于编辑器上下文——Instantiate 延迟一帧回玩家循环（飘字案同款防线，见 UIDebugMenu.ShowNextFrame）
        GameManager.Instance.StartCoroutine(SpawnNextFrame(prefab));
    }

    private static System.Collections.IEnumerator SpawnNextFrame(GameObject prefab)
    {
        yield return null;    // 等一帧，回到玩家循环
        var player = Player.Instance;
        if (player == null) yield break;

        // 玩家面朝方向前方 3 单位落地
        Vector3 pos = player.transform.position + new Vector3(3f * player.locomotionComponent.FacingDirection, 0f, 0f);
        GameObject go = Object.Instantiate(prefab, pos, Quaternion.identity);
        go.name = "TrainingDummy";

        // 不倒翁配置（内存实例）：血 9999、防 0。immortal → Health 的 min 锁 1 → 永不触发 OnDied
        // （赋值在 Instantiate 与 Start 之间——HealthManageComponent.Init 在 Start 里读，读得到）
        var cfg = ScriptableObject.CreateInstance<EntityCharacterConfig>();
        cfg.maxHealth = 9999f;
        cfg.attackDamage = 1f;
        cfg.defense = 0f;
        cfg.immortal = true;
        Enemy enemyComp = go.GetComponent<Enemy>();
        if (enemyComp != null) enemyComp.characterData = cfg;

        Debug.Log($"[沙包] 已在 {pos} 生成不倒翁（血锁 1 永不死 / 防 0）——随便打，停 Play 自动消失");
    }
}

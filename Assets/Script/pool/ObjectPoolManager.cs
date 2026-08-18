using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace Managers
{

/// <summary>
/// 运行时对象池管理器 — 独立 MonoBehaviour 单例，不经过 MOMS。
/// 池内实例挂载到运行时创建的根 GameObject 下。
/// </summary>
public class ObjectPoolManager : Singleton<ObjectPoolManager>
{

    [System.Serializable]
    public class EnemyPoolInfo
    {
        public string enemyType; // 敌人类型标识，如 "Spider", "Goblin", "Dragon"
        public GameObject prefab; // 对应的敌人预制体
        public int poolSize = 10; // 初始池大小
        public int maxSize = 50; // 池内最大闲置实例数，超出部分回收时直接销毁
    }

    // 运行时从配置加载；若为空会记录错误
    public List<EnemyPoolInfo> enemyPoolInfos = new();

    // 类型标识 → 对象池（取实例时正向查找）
    private readonly Dictionary<string, ObjectPool<GameObject>> _objectPools = new();
    // 实例 → 类型标识（回收时反向查找，避免调用方传类型名）
    private readonly Dictionary<GameObject, string> _spawnedToType = new();
    private GameObject _root;

    protected override void Awake()
    {
        base.Awake();
        // 创建根对象用于挂载池内实例
        _root = new GameObject("ObjectPoolRoot");
        DontDestroyOnLoad(_root);

        // 如果没有配置，通过 Resources 尝试加载（可选拓展点）
        if (enemyPoolInfos == null || enemyPoolInfos.Count == 0)
        {
            Debug.LogWarning("ObjectPoolManager: 没有找到池配置 (enemyPoolInfos)。请通过代码或资源填充。初始化将继续，但池为空。");
        }

        foreach (var info in enemyPoolInfos)
        {
            if (info == null || info.prefab == null || string.IsNullOrEmpty(info.enemyType))
                continue;

            var pool = new ObjectPool<GameObject>(
                () => CreateNewEnemy(info.prefab),
                obj => { },
                obj => ReturnToPoolInternal(obj, info.enemyType),
                actionOnDestroy: obj =>
                {
                    // 池满丢弃或 Clear/Dispose 时同样清理类型映射，防止 _spawnedToType 残留
                    _spawnedToType.Remove(obj);
                    Destroy(obj);
                },
                collectionCheck: false,
                defaultCapacity: info.poolSize,
                maxSize: Mathf.Max(1, info.maxSize) // Unity ObjectPool 要求 maxSize >= 1
            );

            _objectPools[info.enemyType] = pool;
        }
    }

    protected override void OnDestroy()
    {
        foreach (var kv in _objectPools)
        {
            // 尝试清空 pools (ObjectPool 没有 Clear API)，让 GC 回收
        }

        _objectPools.Clear();
        _spawnedToType.Clear();

        if (_root != null)
        {
            Destroy(_root);
            _root = null;
        }

        base.OnDestroy();
    }

    private GameObject CreateNewEnemy(GameObject prefab)
    {
        var obj = Instantiate(prefab);
        obj.SetActive(false);
        if (_root != null)
            obj.transform.SetParent(_root.transform, false);
        return obj;
    }

    /// <summary>回收对象到池中的内部实现：调用 IPoolable 回调、重置 Transform、取消激活并清理类型映射</summary>
    private void ReturnToPoolInternal(GameObject obj, string enemyType)
    {
        IPoolable poolable = obj.GetComponent<IPoolable>();
        if (poolable != null)
        {
            poolable.OnReturnToPool();
        }

        obj.transform.rotation = Quaternion.identity;
        obj.transform.position = Vector3.zero;
        if (_root != null)
            obj.transform.SetParent(_root.transform, false);
        obj.SetActive(false);

        // 移除映射
        if (_spawnedToType.ContainsKey(obj))
            _spawnedToType.Remove(obj);
    }

    // 从池中获取敌人
    public GameObject GetEnemy(string enemyType, Vector3 position, Quaternion rotation)
    {
        if (!_objectPools.TryGetValue(enemyType, out var pool))
        {
            Debug.LogError($"没有找到类型为 {enemyType} 的对象池！");
            return null;
        }

        GameObject obj = pool.Get();

        // 记录类型映射，便于 Release 时查找
        _spawnedToType[obj] = enemyType;

        // 重置敌人状态（重要！）
        obj.transform.position = position;
        obj.transform.rotation = rotation;

        obj.SetActive(true);

        IPoolable poolable = obj.GetComponent<IPoolable>();
        poolable?.OnSpawnFromPool();

        return obj;
    }

    // 通过类型回收敌人到池中（兼容旧 API）
    public void ReturnEnemy(GameObject enemy, string enemyType)
    {
        if (enemy == null) return;
        if (!_objectPools.TryGetValue(enemyType, out var pool))
        {
            Debug.LogError($"无法回收类型 {enemyType}，该类型不存在池子");
            Destroy(enemy);
            return;
        }

        pool.Release(enemy);
    }

    // 新增统一 Release 方法：根据实例查找其类型并回收
    public void Release(GameObject enemy)
    {
        if (enemy == null) return;
        if (_spawnedToType.TryGetValue(enemy, out var type))
        {
            if (_objectPools.TryGetValue(type, out var pool))
            {
                pool.Release(enemy);
                return;
            }
        }

        // 回退：直接销毁
        Destroy(enemy);
    }
}
}
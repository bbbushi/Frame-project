using UnityEngine;
using UnityEngine.Pool;

public class ShadowPool : MonoBehaviour
{
    public static ShadowPool Instance;

    private ObjectPool<GameObject> _shadowPool; // 影子对象池
    [SerializeField] private GameObject _shadowPrefab; // 影子预制体
    [SerializeField] private int _shadowCount = 10; // 池中影子对象的数量
    [SerializeField] private int _shadowMaxSize = 50; // 池上限，超出部分回收时直接销毁

    void Awake()
    {
        Instance = this;
        //初始化对象池
    }

    private void Start()
    {
        _shadowPool = new ObjectPool<GameObject>(
            () =>
            {
                var obj = GameObject.Instantiate(_shadowPrefab, this.transform);
                obj.SetActive(false);
                return obj;
            },
            shadow => shadow.SetActive(true),
            shadow => shadow.SetActive(false),
            defaultCapacity: _shadowCount,
            maxSize: Mathf.Max(1, _shadowMaxSize) // Unity ObjectPool 要求 maxSize >= 1
        );

        for (int i = 0; i < _shadowCount; i++)
        {
            var obj = _shadowPool.Get();
            _shadowPool.Release(obj);
        }
    }

    public void ReturnPool(GameObject shadow)
    {
        if (_shadowPool == null || shadow == null) return;
        _shadowPool.Release(shadow);
    }

    /// <summary>生成残影：从池中取出，Shadow 组件在 OnEnable 中自动同步玩家状态并淡出回收</summary>
    public GameObject GetShadow(Transform playerTransform)
    {
        if (_shadowPool == null || playerTransform == null) return null;
        return _shadowPool.Get();
    }
}
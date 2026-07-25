using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    // 所有要注册的Manager类型（可从 Inspector 赋值或通过代码自动扫描）
    [SerializeField] private List<Type> managerTypes; 
    
    private Dictionary<Type, IManager> managers = new Dictionary<Type, IManager>();
    private List<IManager> initializationOrder = new List<IManager>();
    private List<IUpdatable> updatableManagers = new List<IUpdatable>();
    
    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        
        // 如果没有手动指定managerTypes，可以通过反射扫描所有实现了IManager的类
        if (managerTypes == null || managerTypes.Count == 0)
        {
            managerTypes = ScanAllManagers();
        }
        
        StartCoroutine(InitializeAll());
    }
    
    private List<Type> ScanAllManagers()
    {
        // 扫描当前程序集中所有非抽象、实现了IManager的类
        var types = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(asm => asm.GetTypes())
            .Where(t => typeof(IManager).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
            .ToList();
        return types;
    }
    
    private IEnumerator InitializeAll()
    {
        // 1. 创建所有Manager实例并存入字典
        foreach (var type in managerTypes)
        {
            try
            {
                var manager = (IManager)Activator.CreateInstance(type);
                managers[type] = manager;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"创建 Manager 失败: {type.Name} —— {ex.Message}");
            }
        }

        // 2. 拓扑排序得到初始化顺序（复用已创建的实例读取依赖，避免二次 Activator.CreateInstance）
        var sortedTypes = TopologicalSort();
        initializationOrder.Clear();
        foreach (var type in sortedTypes)
        {
            if (managers.TryGetValue(type, out var mgr))
                initializationOrder.Add(mgr);
        }

        // 3. 按顺序执行初始化（异步支持），单个失败不影响其余
        foreach (var manager in initializationOrder)
        {
            yield return InitializeManagerSafe(manager);

            if (manager is IUpdatable updatable)
                updatableManagers.Add(updatable);
        }

        Debug.Log($"Manager 初始化完成：{initializationOrder.Count} 个已加载");
    }

    /// <summary> 安全初始化一个Manager，捕获协程每个 yield 点的异常 </summary>
    private IEnumerator InitializeManagerSafe(IManager manager)
    {
        var enumerator = manager.Initialize();
        while (true)
        {
            try
            {
                if (!enumerator.MoveNext())
                    yield break;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"初始化 Manager 失败: {manager.Name} —— {ex.Message}");
                yield break;
            }
            yield return enumerator.Current;
        }
    }

    // 拓扑排序算法实现，确保依赖关系正确处理。直接从 managers 字典读取 Dependencies，不再重复创建实例。
    private List<Type> TopologicalSort()
    {
        var types = managerTypes;
        var graph = new Dictionary<Type, List<Type>>();
        var inDegree = new Dictionary<Type, int>();

        // 初始化所有节点的入度为0
        foreach (var type in types)
        {
            graph[type] = new List<Type>();
            inDegree[type] = 0;
        }

        // 构建依赖图：若 A 依赖 B，则添加边 B -> A。直接从已创建的 managers 实例读取 Dependencies。
        foreach (var type in types)
        {
            if (!managers.TryGetValue(type, out var manager)) continue;

            foreach (var dep in manager.Dependencies)
            {
                if (!types.Contains(dep))
                {
                    Debug.LogError($"依赖错误：{type.Name} 依赖 {dep.Name}，但 {dep.Name} 未在managerTypes列表中");
                    continue;
                }
                graph[dep].Add(type);
                inDegree[type]++;
            }
        }
        
        // Kahn算法拓扑排序
        var queue = new Queue<Type>();
        foreach (var kv in inDegree)
        {
            if (kv.Value == 0) queue.Enqueue(kv.Key);
        }
        
        var result = new List<Type>();
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            result.Add(current);
            foreach (var neighbor in graph[current])
            {
                inDegree[neighbor]--;
                if (inDegree[neighbor] == 0)
                    queue.Enqueue(neighbor);
            }
        }
        
        if (result.Count != types.Count)
        {
            Debug.LogError("检测到循环依赖！排序失败。");
            // 返回原顺序作为后备
            return types;
        }
        
        return result;
    }
    
    public T GetManager<T>() where T : class, IManager
    {
        var type = typeof(T);
        if (managers.TryGetValue(type, out var manager))
            return manager as T;
        Debug.LogError($"Manager {type.Name} 未注册");
        return null;
    }

    /// <summary> 便捷静态访问器：GameManager.Get&lt;PlayerManager&gt;() </summary>
    public static T Get<T>() where T : class, IManager => Instance?.GetManager<T>();
    
    private void Update()
    {
        float dt = Time.deltaTime;
        foreach (var updatable in updatableManagers)
            updatable.OnUpdate(dt);
    }
    
    private void OnDestroy()
    {
        // 逆序销毁（后初始化的先销毁）
        for (int i = initializationOrder.Count - 1; i >= 0; i--)
        {
            initializationOrder[i].Deinitialize();
        }
    }
}
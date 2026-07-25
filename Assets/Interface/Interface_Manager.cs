using System;
using System.Collections;
using System.Collections.Generic;

public interface IManager
{
    string Name { get; }
    /// <summary> 依赖的其他Manager类型列表 </summary>
    List<Type> Dependencies { get; }
    IEnumerator Initialize();
    void Deinitialize();
}

public interface IUpdatable
{
    void OnUpdate(float deltaTime);
}
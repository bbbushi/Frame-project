using System;
using System.Collections.Generic;
using UnityEngine;
namespace Managers.GameFlow
{
    [CreateAssetMenu(menuName = "MOMS/SceneRegistry", fileName = "SceneRegistry")]
    public class SceneRegistry : ScriptableObject
    {
        [Serializable] public class SceneEntry
        {
            public string sceneId;        // 场景文件名（LoadScene 用，如 "BABEL1"）
            public string displayName;    // Loading 屏显示名
            public bool isMainMenu;       // 主菜单场景标记（Routine 落定状态用）
        }
        public List<SceneEntry> scenes = new();
    }

}

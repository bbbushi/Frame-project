using UnityEngine;
using GameFlowSystem;

namespace GameFlowSystem
{
    /// <summary>
    /// 主菜单按钮转发器：UGUI OnClick 只能绑实例方法，静态门面（GameFlowManager.StartLevel）须借它中转。
    /// 挂在主菜单场景任意常驻对象上，Inspector 填 startSceneId（须与 SceneRegistry 登记一致），
    /// 按钮 OnClick 拖本对象 → 选 StartGame。
    /// </summary>
    public class MainMenuHook : MonoBehaviour
    {
        [SerializeField] private string startSceneId = "BABEL1";   // 起始关卡（sceneId = 场景文件名）

        /// <summary>开始游戏（绑按钮 OnClick）</summary>
        public void StartGame() => GameFlowManager.StartLevel(startSceneId);

        /// <summary>退出游戏（绑按钮 OnClick；编辑器里 Quit 无效，打包后有效）</summary>
        public void QuitGame() => Application.Quit();
    }
}

using UnityEngine;
using System.Collections.Generic;
namespace Guide
{
    /// <summary>
    /// 引导数据：ScriptableObject 形式的引导配置文件
    /// - 由设计师在编辑器中创建、编辑、保存
    /// - 由引导系统在运行时加载、实例化、执行
    /// - 由引导系统在运行时读取、修改、存档
    /// - 由引导系统在运行时广播事件、触发表现层
    /// - 由引导系统在运行时销毁、卸载
    /// - 由引导系统在运行时支持断点续传、重播
    /// </summary>
    [CreateAssetMenu(fileName = "Guide_New", menuName = "Data/Guide/GuideData")]
    public class GuideData : ScriptableObject
    {
        [Tooltip("引导唯一 ID（全局唯一即可，如 guide_001）")]
        public string guideId;
        [Tooltip("库加载后自动开始（未完成时）")]
        public bool autoStart;
        public List<GuideStep> steps = new();
    }
    
}


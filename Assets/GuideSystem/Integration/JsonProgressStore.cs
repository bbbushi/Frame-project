using Guide;
using SavingSystemTutorial;
using System.IO;
using UnityEngine;
namespace Managers.Guide
{
    /// persistentDataPath/guide_progress.json
    
    /// <summary>
    /// 使用 JSON 格式存储引导进度的类。
    /// </summary>
    public class JsonProgressStore : IGuideProgressStore
    {
        private const string FileName = "guide_progress";
        public GuideProgressData Load()
        {
            // 先预检文件存在：SavingSystem 缺文件时打的是红色 LogError，首次运行会误报
            string path = Path.Combine(Application.persistentDataPath, FileName + ".json");
            if (!File.Exists(path)) return new GuideProgressData();
            return SavingSystem.LoadFromJson<GuideProgressData>(FileName) ?? new GuideProgressData();
        }
        public void Save(GuideProgressData progress)
        {
            SavingSystem.SavingByJson(FileName, progress);
        }
        public void Clear()
        {
            SavingSystem.DeleteSaveFile(FileName);
        }
    }
}
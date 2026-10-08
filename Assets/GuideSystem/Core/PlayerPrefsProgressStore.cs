using UnityEngine;
namespace Guide
{
    /// 可移植默认实现：PlayerPrefs（key="Guide.Progress"，JsonUtility 存字符串）
    public class PlayerPrefsProgressStore : IGuideProgressStore
    {
        public GuideProgressData Load()
        {
            var json = PlayerPrefs.GetString("Guide.Progress", "");
            if (string.IsNullOrEmpty(json))
                return new GuideProgressData();
            return JsonUtility.FromJson<GuideProgressData>(json);
            
        }
        public void Save(GuideProgressData data)
        {
            PlayerPrefs.SetString("Guide.Progress", JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }
        public void Clear()
        {
            PlayerPrefs.DeleteKey("Guide.Progress");
            PlayerPrefs.Save();
        }
    }
}
using UnityEngine;
using System.IO;
using System.Collections.Generic;
namespace SavingSystemTutorial
{
    public static class SavingSystem
    {
        public static void SavingByJson(string saveFileName, object data)
        {
            var jsonfile = JsonUtility.ToJson(data, true);   
            string path = Path.Combine(Application.persistentDataPath, saveFileName + ".json");
            try
            {
                File.WriteAllText(path, jsonfile);
            }
            catch (System.Exception e)
            {
                Debug.LogError("Error while saving data: " + e.Message);
            }
        
        }
        public static T LoadFromJson<T>(string saveFileName)
        {
            try
            {
                var json = File.ReadAllText(Path.Combine(Application.persistentDataPath, saveFileName + ".json"));
                var data = JsonUtility.FromJson<T>(json);
                return data;
            }
            catch (System.Exception e)
            {
                Debug.LogError("Error while loading data: " + e.Message);
                return default(T);
            }
        }
        public static void DeleteSaveFile(string saveFileName)
        {
            string path = Path.Combine(Application.persistentDataPath, saveFileName + ".json");
            try
            {
                File.Delete(path);
            }
            catch (System.Exception e)
            {
                Debug.LogError("Error while deleting save file: " + e.Message);
            }
        
        }
        public static void SavingByPlayerPrefs(string saveFileName, object data)
        {
            var jsonfile = JsonUtility.ToJson(data, true);
            PlayerPrefs.SetString(saveFileName, jsonfile);
            PlayerPrefs.Save();
        }
        public static T LoadFromPlayerPrefs<T>(string saveFileName)
        {
            try
            {
                var json = PlayerPrefs.GetString(saveFileName, "");
                if (string.IsNullOrEmpty(json))
                {
                    Debug.LogWarning("No data found in PlayerPrefs for key: " + saveFileName);
                    return default(T);
                }
                var data = JsonUtility.FromJson<T>(json);
                return data;
            }
            catch (System.Exception e)
            {
                Debug.LogError("Error while loading data from PlayerPrefs: " + e.Message);
                return default(T);
            }
        }
        public static void DeleteSaveFileFromPlayerPrefs(string saveFileName)
        {
            try
            {
                PlayerPrefs.DeleteKey(saveFileName);
                PlayerPrefs.Save();
            }
            catch (System.Exception e)
            {
                Debug.LogError("Error while deleting save file from PlayerPrefs: " + e.Message);
            }
        }
    }
}

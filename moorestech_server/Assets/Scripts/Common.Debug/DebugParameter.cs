using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Common.Debug
{
    public static class DebugParameters
    {
        private static readonly string CachePath = Path.GetFullPath("../cache");
        private const string BoolFileName = "BoolDebugParameters.json";
        private const string IntFileName = "IntDebugParameters.json";
        private const string StringFileName = "StringDebugParameters.json";

        private static Dictionary<string, bool> BoolDebugParameters { get; set; } = new();
        private static Dictionary<string, int> IntDebugParameters { get; set; } = new();
        private static Dictionary<string, string> StringDebugParameters { get; set; } = new();

        #region Public Accessors

        public static bool GetValueOrDefaultBool(string key, bool defaultValue)
        {
            Load();
            return BoolDebugParameters.GetValueOrDefault(key, defaultValue);
        }

        public static bool TryGetBool(string key, out bool value)
        {
            Load();
            return BoolDebugParameters.TryGetValue(key, out value);
        }

        public static void SaveBool(string key, bool value)
        {
            Load();
            BoolDebugParameters[key] = value;
            Save();
        }

        public static bool RemoveBool(string key)
        {
            Load();
            var result = BoolDebugParameters.Remove(key);
            Save();
            return result;
        }

        public static bool ExistsBool(string key)
        {
            Load();
            return BoolDebugParameters.ContainsKey(key);
        }

        public static int GetValueOrDefaultInt(string key, int defaultValue)
        {
            Load();
            return IntDebugParameters.GetValueOrDefault(key, defaultValue);
        }

        public static bool TryGetInt(string key, out int value)
        {
            Load();
            return IntDebugParameters.TryGetValue(key, out value);
        }

        public static void SaveInt(string key, int value)
        {
            Load();
            IntDebugParameters[key] = value;
            Save();
        }

        public static bool RemoveInt(string key)
        {
            Load();
            var result = IntDebugParameters.Remove(key);
            Save();
            return result;
        }

        public static bool ExistsInt(string key)
        {
            Load();
            return IntDebugParameters.ContainsKey(key);
        }

        public static string GetValueOrDefaultString(string key, string defaultValue)
        {
            Load();
            return StringDebugParameters.GetValueOrDefault(key, defaultValue);
        }

        public static bool TryGetString(string key, out string value)
        {
            Load();
            return StringDebugParameters.TryGetValue(key, out value);
        }

        public static void SaveString(string key, string value)
        {
            Load();
            StringDebugParameters[key] = value;
            Save();
        }

        public static bool RemoveString(string key)
        {
            Load();
            var result = StringDebugParameters.Remove(key);
            Save();
            return result;
        }

        public static bool ExistsString(string key)
        {
            Load();
            return StringDebugParameters.ContainsKey(key);
        }

        #endregion

        #region File Operations

        private static void Save()
        {
            EnsureCacheDirectoryExists();

            // Save bool parameters
            var boolDict = new SerializableDictionary<string, bool>(BoolDebugParameters);
            File.WriteAllText(GetFilePath(BoolFileName), JsonUtility.ToJson(boolDict));

            // Save int parameters
            var intDict = new SerializableDictionary<string, int>(IntDebugParameters);
            File.WriteAllText(GetFilePath(IntFileName), JsonUtility.ToJson(intDict));

            // Save string parameters
            var stringDict = new SerializableDictionary<string, string>(StringDebugParameters);
            File.WriteAllText(GetFilePath(StringFileName), JsonUtility.ToJson(stringDict));
        }

        private static void Load()
        {
            BoolDebugParameters = LoadDictionary<string, bool>(GetFilePath(BoolFileName));
            IntDebugParameters = LoadDictionary<string, int>(GetFilePath(IntFileName));
            StringDebugParameters = LoadDictionary<string, string>(GetFilePath(StringFileName));
        }

        private static Dictionary<TKey, TValue> LoadDictionary<TKey, TValue>(string filePath)
        {
            if (!File.Exists(filePath)) return new Dictionary<TKey, TValue>();

            var json = File.ReadAllText(filePath);
            var dict = JsonUtility.FromJson<SerializableDictionary<TKey, TValue>>(json);
            return dict?.ToDictionary() ?? new Dictionary<TKey, TValue>();
        }

        private static void EnsureCacheDirectoryExists()
        {
            if (!Directory.Exists(CachePath))
            {
                Directory.CreateDirectory(CachePath);
            }
        }

        private static string GetFilePath(string fileName)
        {
            return Path.Combine(CachePath, fileName);
        }

        #endregion
    }

    /// <summary>
    /// Dictionary を JSON シリアライズできるようにするためのクラス
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    [Serializable]
    public class SerializableDictionary<TKey, TValue> : ISerializationCallbackReceiver
    {
        [SerializeField]
        private List<TKey> keys = new();

        [SerializeField]
        private List<TValue> values = new();

        // 実際の Dictionary データ。シリアライズ時・デシリアライズ時に keys/values と相互変換。
        private Dictionary<TKey, TValue> dictionary = new();

        public SerializableDictionary() { }

        public SerializableDictionary(Dictionary<TKey, TValue> dict)
        {
            dictionary = dict;
        }

        /// <summary>
        /// Dictionary 形式に変換して取得
        /// </summary>
        public Dictionary<TKey, TValue> ToDictionary()
        {
            return dictionary;
        }

        #region ISerializationCallbackReceiver implements

        public void OnBeforeSerialize()
        {
            // JSON シリアライズされる前に、Dictionary の情報を keys/values に詰め込む
            keys.Clear();
            values.Clear();

            foreach (var kvp in dictionary)
            {
                keys.Add(kvp.Key);
                values.Add(kvp.Value);
            }
        }

        public void OnAfterDeserialize()
        {
            // JSON デシリアライズ後に、keys/values から Dictionary を再構成する
            dictionary = new Dictionary<TKey, TValue>();
            for (int i = 0; i < Mathf.Min(keys.Count, values.Count); i++)
            {
                dictionary[keys[i]] = values[i];
            }
        }

        #endregion
    }
}

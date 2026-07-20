using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Common.Debug
{
    /// <summary>
    /// クライアントとサーバーの両方から参照する共有デバッグキー
    /// Shared debug keys referenced by both the client and the server
    /// </summary>
    public static class DebugParameterKeys
    {
        // ブロック設置を無料化する（建設コストを消費しない）
        // Make block placement free (do not consume construction cost)
        public const string FreeBlockPlacement = "FreeBlockPlacement";
    }

    public static class DebugParameters
    {
        private static Dictionary<string, bool> BoolDebugParameters { get; set; } = new();
        private static Dictionary<string, int> IntDebugParameters { get; set; } = new();
        private static Dictionary<string, string> StringDebugParameters { get; set; } = new();

        #region Public Accessors

        public static bool GetValueOrDefaultBool(string key, bool defaultValue = false)
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
            Directory.CreateDirectory(DebugParametersCacheDirectory.Resolve());

            // Save bool parameters
            var boolDict = new SerializableDictionary<string, bool>(BoolDebugParameters);
            File.WriteAllText(GetFilePath(DebugParametersCacheDirectory.BoolFileName), JsonUtility.ToJson(boolDict));

            // Save int parameters
            var intDict = new SerializableDictionary<string, int>(IntDebugParameters);
            File.WriteAllText(GetFilePath(DebugParametersCacheDirectory.IntFileName), JsonUtility.ToJson(intDict));

            // Save string parameters
            var stringDict = new SerializableDictionary<string, string>(StringDebugParameters);
            File.WriteAllText(GetFilePath(DebugParametersCacheDirectory.StringFileName), JsonUtility.ToJson(stringDict));
        }

        private static void Load()
        {
            BoolDebugParameters = LoadDictionary<string, bool>(GetFilePath(DebugParametersCacheDirectory.BoolFileName));
            IntDebugParameters = LoadDictionary<string, int>(GetFilePath(DebugParametersCacheDirectory.IntFileName));
            StringDebugParameters = LoadDictionary<string, string>(GetFilePath(DebugParametersCacheDirectory.StringFileName));
        }

        private static Dictionary<TKey, TValue> LoadDictionary<TKey, TValue>(string filePath)
        {
            if (!File.Exists(filePath)) return new Dictionary<TKey, TValue>();

            var json = File.ReadAllText(filePath);
            var dict = JsonUtility.FromJson<SerializableDictionary<TKey, TValue>>(json);
            return dict?.ToDictionary() ?? new Dictionary<TKey, TValue>();
        }

        // 静的初期化時にキャッシュせずアクセス毎に解決する。環境変数設定より前に初期化されると切替が効かないため
        // Resolve per access instead of caching at static init, since initializing before the env var is set would defeat the switch
        private static string GetFilePath(string fileName)
        {
            return Path.Combine(DebugParametersCacheDirectory.Resolve(), fileName);
        }

        #endregion
    }
}

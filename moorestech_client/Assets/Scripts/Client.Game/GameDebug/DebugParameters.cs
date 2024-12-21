using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Client.Game.GameDebug
{
    /// <summary>
    /// デバッグ用のパラメータを管理するクラス
    /// </summary>
    public static class DebugParameters
    {
        static DebugParameters()
        {
            Load();
        }
        
        public static Dictionary<string, bool> BoolDebugParameters { get; private set; } = new();
        public static Dictionary<string, int> IntDebugParameters { get; private set; } = new();
        public static Dictionary<string, string> StringDebugParameters { get; private set; } = new();

        #region Public Accessors

        public static bool GetBool(string key, bool defaultValue)
        {
            return BoolDebugParameters.GetValueOrDefault(key, defaultValue);
        }

        public static void SaveBool(string key, bool value)
        {
            BoolDebugParameters[key] = value;
            Save();
        }

        public static int GetInt(string key, int defaultValue)
        {
            return IntDebugParameters.GetValueOrDefault(key, defaultValue);
        }

        public static void SaveInt(string key, int value)
        {
            IntDebugParameters[key] = value;
            Save();
        }

        public static string GetString(string key, string defaultValue)
        {
            return StringDebugParameters.GetValueOrDefault(key, defaultValue);
        }

        public static void SaveString(string key, string value)
        {
            StringDebugParameters[key] = value;
            Save();
        }

        #endregion

        #region Save / Load
        
        public const string BoolDebugParametersKey = "DebugParameters_Bool";
        public const string IntDebugParametersKey = "DebugParameters_Int";
        public const string StringDebugParametersKey = "DebugParameters_String";

        /// <summary>
        /// デバッグパラメータを PlayerPrefs に保存します。
        /// </summary>
        private static void Save()
        {
            // bool
            var boolDict = new SerializableDictionary<string, bool>(BoolDebugParameters);
            string boolJson = JsonUtility.ToJson(boolDict);
            PlayerPrefs.SetString(BoolDebugParametersKey, boolJson);

            // int
            var intDict = new SerializableDictionary<string, int>(IntDebugParameters);
            string intJson = JsonUtility.ToJson(intDict);
            PlayerPrefs.SetString(IntDebugParametersKey, intJson);

            // string
            var stringDict = new SerializableDictionary<string, string>(StringDebugParameters);
            string stringJson = JsonUtility.ToJson(stringDict);
            PlayerPrefs.SetString(StringDebugParametersKey, stringJson);

            PlayerPrefs.Save();
        }

        /// <summary>
        /// 保存された PlayerPrefs からデバッグパラメータを読み込みます。
        /// </summary>
        private static void Load()
        {
            // bool
            string boolJson = PlayerPrefs.GetString(BoolDebugParametersKey, "");
            if (!string.IsNullOrEmpty(boolJson))
            {
                var boolDict = JsonUtility.FromJson<SerializableDictionary<string, bool>>(boolJson);
                if (boolDict != null)
                {
                    BoolDebugParameters = boolDict.ToDictionary();
                }
            }

            // int
            string intJson = PlayerPrefs.GetString(IntDebugParametersKey, "");
            if (!string.IsNullOrEmpty(intJson))
            {
                var intDict = JsonUtility.FromJson<SerializableDictionary<string, int>>(intJson);
                if (intDict != null)
                {
                    IntDebugParameters = intDict.ToDictionary();
                }
            }

            // string
            string stringJson = PlayerPrefs.GetString(StringDebugParametersKey, "");
            if (!string.IsNullOrEmpty(stringJson))
            {
                var stringDict = JsonUtility.FromJson<SerializableDictionary<string, string>>(stringJson);
                if (stringDict != null)
                {
                    StringDebugParameters = stringDict.ToDictionary();
                }
            }
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

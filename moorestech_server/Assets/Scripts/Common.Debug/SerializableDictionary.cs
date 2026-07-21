using System;
using System.Collections.Generic;
using UnityEngine;

namespace Common.Debug
{
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

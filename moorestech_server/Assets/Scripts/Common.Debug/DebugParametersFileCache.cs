using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Common.Debug
{
    /// <summary>
    /// 1ディレクトリ分のデバッグ設定JSONを読み込んだ結果。書き込みは読み込み元と同じディレクトリへ戻す
    /// The debug parameter JSON loaded from one directory; writes go back to the directory it was loaded from
    /// </summary>
    internal class DebugParametersFileCache
    {
        public readonly string DirectoryPath;
        public readonly Dictionary<string, bool> Bools;
        public readonly Dictionary<string, int> Ints;
        public readonly Dictionary<string, string> Strings;

        private DebugParametersFileCache(string directoryPath, Dictionary<string, bool> bools, Dictionary<string, int> ints, Dictionary<string, string> strings)
        {
            DirectoryPath = directoryPath;
            Bools = bools;
            Ints = ints;
            Strings = strings;
        }

        public static DebugParametersFileCache Load(string directoryPath)
        {
            var bools = LoadDictionary<string, bool>(Path.Combine(directoryPath, DebugParametersCacheDirectory.BoolFileName));
            var ints = LoadDictionary<string, int>(Path.Combine(directoryPath, DebugParametersCacheDirectory.IntFileName));
            var strings = LoadDictionary<string, string>(Path.Combine(directoryPath, DebugParametersCacheDirectory.StringFileName));
            return new DebugParametersFileCache(directoryPath, bools, ints, strings);
        }

        public void Save()
        {
            Directory.CreateDirectory(DirectoryPath);
            File.WriteAllText(Path.Combine(DirectoryPath, DebugParametersCacheDirectory.BoolFileName), JsonUtility.ToJson(new SerializableDictionary<string, bool>(Bools)));
            File.WriteAllText(Path.Combine(DirectoryPath, DebugParametersCacheDirectory.IntFileName), JsonUtility.ToJson(new SerializableDictionary<string, int>(Ints)));
            File.WriteAllText(Path.Combine(DirectoryPath, DebugParametersCacheDirectory.StringFileName), JsonUtility.ToJson(new SerializableDictionary<string, string>(Strings)));
        }

        private static Dictionary<TKey, TValue> LoadDictionary<TKey, TValue>(string filePath)
        {
            if (!File.Exists(filePath)) return new Dictionary<TKey, TValue>();

            var json = File.ReadAllText(filePath);
            var dict = JsonUtility.FromJson<SerializableDictionary<TKey, TValue>>(json);
            return dict?.ToDictionary() ?? new Dictionary<TKey, TValue>();
        }
    }
}

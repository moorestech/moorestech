using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Game.SaveLoad.Snapshot
{
    // 2つのスナップショットJSONを深く比較し、不一致のパスを返す。setting は実時刻を含むため除外する
    // Deep-compares two snapshot JSONs and lists differing paths; "setting" is excluded because it carries wall-clock times
    public static class SnapshotJsonComparer
    {
        public const string ExcludedTopLevelKey = "setting";

        // 差が大量に出たときの出力爆発を防ぐ上限。発散源の特定にはこの件数で足りる
        // Caps the output when everything differs; this many paths suffice to locate the divergence
        private const int MaxDifferences = 50;

        public static SnapshotComparison Compare(string expectedJson, string actualJson)
        {
            var expected = JObject.Parse(expectedJson);
            var actual = JObject.Parse(actualJson);
            expected.Remove(ExcludedTopLevelKey);
            actual.Remove(ExcludedTopLevelKey);

            var differences = new List<string>();
            Walk(expected, actual, string.Empty, differences);
            return new SnapshotComparison(differences);
        }

        private static void Walk(JToken expected, JToken actual, string path, List<string> differences)
        {
            if (differences.Count >= MaxDifferences) return;
            if (expected.Type != actual.Type)
            {
                differences.Add($"{path}: 型が違う expected={expected.Type} actual={actual.Type}");
                return;
            }

            switch (expected)
            {
                case JObject expectedObject:
                    WalkObject(expectedObject, (JObject)actual, path, differences);
                    return;
                case JArray expectedArray:
                    WalkArray(expectedArray, (JArray)actual, path, differences);
                    return;
                default:
                    if (!JToken.DeepEquals(expected, actual)) differences.Add($"{path}: expected={expected} actual={actual}");
                    return;
            }
        }

        private static void WalkObject(JObject expected, JObject actual, string path, List<string> differences)
        {
            foreach (var property in expected.Properties())
            {
                if (!actual.TryGetValue(property.Name, out var actualValue))
                {
                    differences.Add($"{ChildPath(path, property.Name)}: actual に無い");
                    continue;
                }
                Walk(property.Value, actualValue, ChildPath(path, property.Name), differences);
            }

            foreach (var property in actual.Properties())
            {
                if (!expected.ContainsKey(property.Name)) differences.Add($"{ChildPath(path, property.Name)}: expected に無い");
            }
        }

        private static void WalkArray(JArray expected, JArray actual, string path, List<string> differences)
        {
            if (expected.Count != actual.Count)
            {
                differences.Add($"{path}: 要素数が違う expected={expected.Count} actual={actual.Count}");
                return;
            }
            for (var i = 0; i < expected.Count; i++) Walk(expected[i], actual[i], $"{path}[{i}]", differences);
        }

        private static string ChildPath(string path, string name)
        {
            return path.Length == 0 ? name : $"{path}.{name}";
        }
    }
}

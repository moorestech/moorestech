using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Game.SaveLoad.Snapshot
{
    // 2つのスナップショットJSONを深く比較し、不一致のパスを返す。除外は実時刻フィールドだけに絞る
    // Deep-compares two snapshot JSONs and lists differing paths; only wall-clock fields are excluded
    public static class SnapshotJsonComparer
    {
        // 実時刻由来で再生しても一致しない値だけを外す。スポーン地点や世界作成日時はロードが復元すべき決定的な値なので検査する
        // Excludes only wall-clock values that replay cannot reproduce; spawn point and world creation time stay under inspection because load must restore them
        private static readonly HashSet<string> ExcludedFieldPaths = new()
        {
            "setting.TotalPlayTimeSeconds",
            "setting.LastSessionStartDateTime",
        };

        // 差が大量に出たときの出力爆発を防ぐ上限。発散源の特定にはこの件数で足りる
        // Caps the output when everything differs; this many paths suffice to locate the divergence
        private const int MaxDifferences = 50;

        public static SnapshotComparison Compare(string expectedJson, string actualJson)
        {
            var expected = JObject.Parse(expectedJson);
            var actual = JObject.Parse(actualJson);

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
                var childPath = ChildPath(path, property.Name);
                if (ExcludedFieldPaths.Contains(childPath)) continue;
                if (!actual.TryGetValue(property.Name, out var actualValue))
                {
                    differences.Add($"{childPath}: actual に無い");
                    continue;
                }
                Walk(property.Value, actualValue, childPath, differences);
            }

            foreach (var property in actual.Properties())
            {
                var childPath = ChildPath(path, property.Name);
                if (ExcludedFieldPaths.Contains(childPath)) continue;
                if (!expected.ContainsKey(property.Name)) differences.Add($"{childPath}: expected に無い");
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
